using System.Text.Json;
using PxApi.Utilities;

namespace PxApi.Services;

/// <summary>Provider-neutral business observations for one incoming HTTP request.</summary>
public sealed class QueryObservation
{
    private readonly Dictionary<string, object?> fields = new(StringComparer.Ordinal);
    private readonly object gate = new();

    /// <summary>Whether sanitized search text may be retained.</summary>
    public bool CaptureSearchText { get; }

    /// <summary>Whether bounded discovery result identifiers may be retained.</summary>
    public bool CaptureResultIds { get; }

    /// <summary>Cells prepared for a data response, not yet successfully written.</summary>
    public long? PreparedCells { get; set; }

    /// <summary>Whether response result execution has begun.</summary>
    public bool ResponseExecutionStarted { get; set; }

    /// <summary>Whether the original MVC response execution failed.</summary>
    public bool ResponseFailed { get; set; }

    /// <summary>Whether an application owner already reported the exception.</summary>
    public bool ExceptionReported { get; set; }

    /// <summary>Whether the tracked business action ran, distinguishing feature gating from resource rejection.</summary>
    public bool ActionEntered { get; set; }

    /// <summary>Whether a business or lifecycle reason has already been recorded.</summary>
    public bool HasErrorCode { get { lock (gate) return fields.ContainsKey(LoggerConsts.Query.Fields.ErrorCode); } }

    /// <summary>Selection detail policy: off, counts, or codes.</summary>
    internal LoggerConsts.Query.DetailMode SelectionDetailMode { get; }

    /// <summary>Initializes bounded privacy settings for one request.</summary>
    public QueryObservation(IConfiguration configuration)
    {
        string? mode = configuration[LoggerConsts.Query.Configuration.SelectionDetailMode];
        SelectionDetailMode = mode switch
        {
            LoggerConsts.Query.Values.Off => LoggerConsts.Query.DetailMode.Off,
            LoggerConsts.Query.Values.Codes => LoggerConsts.Query.DetailMode.Codes,
            _ => LoggerConsts.Query.DetailMode.Counts
        };
        CaptureSearchText = configuration.GetValue(LoggerConsts.Query.Configuration.CaptureSearchText, false);
        CaptureResultIds = configuration.GetValue(LoggerConsts.Query.Configuration.CaptureResultIds, false);
        Set(LoggerConsts.Query.Fields.DataCacheOutcome, LoggerConsts.Query.DataCacheOutcome.NotUsed);
        Set(LoggerConsts.Query.Fields.SelectionDetailMode, SelectionDetailMode);
    }

    /// <summary>Gets the observation attached to a tracked request, if any.</summary>
    public static QueryObservation? Get(HttpContext context) => context.Features.Get<QueryObservation>();

    /// <summary>Sets a fixed contract field; strings are bounded and log-injection safe.</summary>
    public void Set(string name, object? value)
    {
        if (value is Enum contractValue) value = LoggerConsts.Query.Value(contractValue);
        lock (gate)
        {
            if (value is string text && !name.EndsWith(LoggerConsts.Query.Fields.JsonSuffix, StringComparison.Ordinal))
            {
                fields[name] = Bound(text, 128, out bool truncated);
                if (truncated) fields[name + LoggerConsts.Query.Fields.TruncatedSuffix] = true;
            }
            else if (value is not null)
            {
                fields[name] = value;
            }
        }
    }

    /// <summary>Captures approved search text or a policy-disabled marker.</summary>
    public void SearchText(string sanitizedText)
    {
        Set(LoggerConsts.Query.Fields.SearchTextMode, CaptureSearchText ? LoggerConsts.Query.SearchTextMode.Sanitized : LoggerConsts.Query.SearchTextMode.Off);
        if (!CaptureSearchText) return;
        Set(LoggerConsts.Query.Fields.SearchText, Bound(InputSanitizer.SanitizeInput(sanitizedText, 128), 128, out bool truncated));
        Set(LoggerConsts.Query.Fields.SearchTextTruncated, truncated || sanitizedText.Length > 128);
    }

    /// <summary>Captures bounded, optional result exposures, preserving order.</summary>
    public void ResultIds(string name, IEnumerable<string> ids)
    {
        if (!CaptureResultIds) return;
        List<string> captured = [];
        bool truncated = false;
        foreach (string id in ids)
        {
            string bounded = Bound(id, 128, out bool idTruncated);
            if (idTruncated || captured.Count == 100)
            {
                truncated = true;
                break;
            }
            captured.Add(bounded);
        }
        Set(name + LoggerConsts.Query.Fields.JsonSuffix, JsonSerializer.Serialize(captured));
        Set(name + LoggerConsts.Query.Fields.TruncatedSuffix, truncated);
    }

    /// <summary>Returns a snapshot of structured state without removing captured detail.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Complete()
    {
        lock (gate)
        {
            fields[LoggerConsts.Query.Fields.OriginalFormat] = LoggerConsts.Query.CompletionEventName;
            return [.. fields];
        }
    }

    /// <summary>Bounds untrusted text and neutralizes control characters.</summary>
    public static string Bound(string text, int limit, out bool truncated)
    {
        truncated = text.Length > limit;
        int length = Math.Min(text.Length, limit);
        if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
        return string.Concat(text.Take(length).Select(character => char.IsControl(character) || character is '\u2028' or '\u2029' ? ' ' : character));
    }

    /// <summary>Records a typed reason from the application's fixed reason-code vocabulary.</summary>
    internal void Reject(LoggerConsts.Query.ErrorCode errorCode) => Set(LoggerConsts.Query.Fields.ErrorCode,
        Enum.IsDefined(errorCode) ? errorCode : LoggerConsts.Query.ErrorCode.Unknown);

    /// <summary>Accumulates internal cache lookups safely across concurrent enrichment.</summary>
    internal void CacheLookup(LoggerConsts.Query.Cache cache, bool hit)
    {
        (string hitsKey, string missesKey) = cache switch
        {
            LoggerConsts.Query.Cache.Metadata => (LoggerConsts.Query.Fields.MetadataCacheHits, LoggerConsts.Query.Fields.MetadataCacheMisses),
            LoggerConsts.Query.Cache.FileList => (LoggerConsts.Query.Fields.FileListCacheHits, LoggerConsts.Query.Fields.FileListCacheMisses),
            _ => throw new ArgumentOutOfRangeException(nameof(cache))
        };
        string key = hit ? hitsKey : missesKey;
        lock (gate)
        {
            fields.TryAdd(hitsKey, 0L);
            fields.TryAdd(missesKey, 0L);
            fields[key] = fields.TryGetValue(key, out object? count) ? (long)count! + 1 : 1L;
        }
    }
}