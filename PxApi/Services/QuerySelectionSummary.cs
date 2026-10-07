using PxApi.Utilities;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Models.QueryFilters;

namespace PxApi.Services;

/// <summary>Describes actual resolved maps without reapplying filters or expanding cells.</summary>
public static class QuerySelectionSummary
{
    private const int CodeLimit = 10;
    private const int DimensionLimit = 64;
    private static readonly ConditionalWeakTable<IReadOnlyDimension, TimeOrdering> timeOrderings = new();

    /// <summary>Records table metadata without claiming that a data selection was applied.</summary>
    public static void Metadata(QueryObservation observation, IReadOnlyMatrixMetadata metadata)
    {
        observation.Set(LoggerConsts.Query.Fields.DimensionCount, metadata.Dimensions.Count);
        observation.Set(LoggerConsts.Query.Fields.TableCells, TableCells(metadata));
        List<Dictionary<string, object?>> entries = [];
        foreach (IReadOnlyDimension dimension in metadata.Dimensions.Take(DimensionLimit))
        {
            Dictionary<string, object?> entry = new()
            {
                [LoggerConsts.Query.DimensionFields.Code] = QueryObservation.Bound(dimension.Code, 128, out bool codeTruncated),
                [LoggerConsts.Query.DimensionFields.TotalCount] = dimension.Values.Count
            };
            if (codeTruncated) entry[LoggerConsts.Query.DimensionFields.CodeTruncated] = true;
            entries.Add(entry);
        }
        observation.Set(LoggerConsts.Query.Fields.DimensionsJson, JsonSerializer.Serialize(entries));
        observation.Set(LoggerConsts.Query.Fields.DimensionsTruncated, entries.Count < metadata.Dimensions.Count);
    }

    /// <summary>Records intent, counts and classes from the resolved data map.</summary>
    public static void Data(QueryObservation observation, IReadOnlyMatrixMetadata metadata,
        Dictionary<string, Filter> filters, MatrixMap map)
    {
        Dictionary<string, Filter> intent = new(filters, StringComparer.OrdinalIgnoreCase);
        long requestedCells = map.GetSize();
        long? tableCells = TableCells(metadata);
        observation.Set(LoggerConsts.Query.Fields.RequestedCells, requestedCells);
        observation.Set(LoggerConsts.Query.Fields.TableCells, tableCells);
        observation.Set(LoggerConsts.Query.Fields.DimensionCount, metadata.Dimensions.Count);
        observation.Set(LoggerConsts.Query.Fields.ExplicitFilterDimensionCount, filters.Count);
        observation.Set(LoggerConsts.Query.Fields.DefaultedDimensionCount, metadata.Dimensions.Count - filters.Count);
        observation.Set(LoggerConsts.Query.Fields.VaryingDimensionCount, map.DimensionMaps.Count(dimension => dimension.ValueCodes.Count > 1));
        observation.Set(LoggerConsts.Query.Fields.SelectionClassificationVersion, 1);
        observation.Set(LoggerConsts.Query.Fields.SelectionCodeLimit, CodeLimit);
        List<Dictionary<string, object?>> entries = [];
        bool detailTruncated = false;
        for (int index = 0; index < metadata.Dimensions.Count; index++)
        {
            IReadOnlyDimension dimension = metadata.Dimensions[index];
            IDimensionMap selected = map.DimensionMaps[index];
            intent.TryGetValue(dimension.Code, out Filter? filter);
            if (observation.SelectionDetailMode == LoggerConsts.Query.DetailMode.Off || entries.Count == DimensionLimit) continue;
            Dictionary<string, object?> entry = CreateEntry(observation, dimension, selected, filter);
            if (observation.SelectionDetailMode == LoggerConsts.Query.DetailMode.Codes && selected.ValueCodes.Count > CodeLimit) detailTruncated = true;
            entries.Add(entry);
        }
        bool summariesTruncated = entries.Count < metadata.Dimensions.Count && observation.SelectionDetailMode != LoggerConsts.Query.DetailMode.Off;
        observation.Set(LoggerConsts.Query.Fields.DimensionsJson, JsonSerializer.Serialize(entries));
        observation.Set(LoggerConsts.Query.Fields.DimensionsTruncated, summariesTruncated);
        observation.Set(LoggerConsts.Query.Fields.SelectionDetailsTruncated, detailTruncated || summariesTruncated);
    }

    private static Dictionary<string, object?> CreateEntry(QueryObservation observation, IReadOnlyDimension dimension, IDimensionMap selected, Filter? filter)
    {
        Dictionary<string, object?> entry = new()
        {
            [LoggerConsts.Query.DimensionFields.Code] = QueryObservation.Bound(dimension.Code, 128, out bool codeTruncated),
            [LoggerConsts.Query.DimensionFields.FilterType] = filter?.ParamName ?? LoggerConsts.Query.Values.DefaultFilter,
            [LoggerConsts.Query.DimensionFields.IsDefaulted] = filter is null,
            [LoggerConsts.Query.DimensionFields.SelectedCount] = selected.ValueCodes.Count,
            [LoggerConsts.Query.DimensionFields.TotalCount] = dimension.Values.Count,
            [LoggerConsts.Query.DimensionFields.SelectionClasses] = Classify(dimension, selected)
        };
        if (observation.SelectionDetailMode == LoggerConsts.Query.DetailMode.Codes)
        {
            bool capture = selected.ValueCodes.Count <= CodeLimit;
            entry[LoggerConsts.Query.DimensionFields.CodeCaptureStatus] = LoggerConsts.Query.Value(capture ? LoggerConsts.Query.CodeCaptureStatus.Complete : LoggerConsts.Query.CodeCaptureStatus.OverLimit);
            if (capture) entry[LoggerConsts.Query.DimensionFields.SelectedCodes] = selected.ValueCodes.ToArray();
        }
        if (codeTruncated) entry[LoggerConsts.Query.DimensionFields.CodeTruncated] = true;
        AddIntent(entry, filter);
        AddLatest(entry, dimension, selected);
        return entry;
    }

    private static void AddIntent(Dictionary<string, object?> entry, Filter? filter)
    {
        switch (filter)
        {
            case CodeFilter codes:
                entry[LoggerConsts.Query.DimensionFields.RequestedCodeCount] = codes.FilterStrings.Count;
                entry[LoggerConsts.Query.DimensionFields.HasWildcard] = codes.FilterStrings.Any(term => term.Contains('*'));
                break;
            case FirstFilter first: entry[LoggerConsts.Query.DimensionFields.RequestedCount] = first.Count; break;
            case LastFilter last: entry[LoggerConsts.Query.DimensionFields.RequestedCount] = last.Count; break;
            case FromFilter from: entry[LoggerConsts.Query.DimensionFields.HasWildcard] = from.FilterString.Contains('*'); break;
            case ToFilter to: entry[LoggerConsts.Query.DimensionFields.HasWildcard] = to.FilterString.Contains('*'); break;
        }
    }

    private static void AddLatest(Dictionary<string, object?> entry, IReadOnlyDimension dimension, IDimensionMap selected)
    {
        if (dimension.Type == DimensionType.Time && selected.ValueCodes.Count == dimension.Values.Count)
            timeOrderings.GetValue(dimension, ValidateAnnualOrdering);
        int? latestIndex = timeOrderings.TryGetValue(dimension, out TimeOrdering? ordering) ? ordering.LatestIndex : null;
        if (latestIndex.HasValue)
            entry[LoggerConsts.Query.DimensionFields.IsLatest] = selected.ValueCodes.Count == 1 && selected.ValueCodes[0] == dimension.ValueCodes[latestIndex.Value];
    }

    private static List<string> Classify(IReadOnlyDimension dimension, IDimensionMap selected)
    {
        int count = selected.ValueCodes.Count;
        int total = dimension.Values.Count;
        List<string> classes = [];
        bool prefix = count > 0;
        bool suffix = count > 0;
        IReadOnlyList<string> fullCodes = dimension.ValueCodes;
        IReadOnlyList<string> selectedCodes = selected.ValueCodes;
        for (int index = 0; index < count; index++)
        {
            prefix &= selectedCodes[index] == fullCodes[index];
            suffix &= selectedCodes[index] == fullCodes[total - count + index];
        }
        if (prefix) classes.Add(LoggerConsts.Query.Values.FromStart);
        if (suffix) classes.Add(LoggerConsts.Query.Values.FromEnd);
        if (count == total) classes.Add(LoggerConsts.Query.Values.All);
        if (count == 1 && MetaFiltering.TryGetDefaultValueCode(dimension, out string? defaultCode) && selected.ValueCodes[0] == defaultCode)
            classes.Add(LoggerConsts.Query.Values.DefaultValue);
        if (classes.Count == 0) classes.Add(LoggerConsts.Query.Values.Other);
        return classes;
    }

    private static long? TableCells(IReadOnlyMatrixMetadata metadata)
    {
        try
        {
            long cells = 1;
            foreach (IReadOnlyDimension dimension in metadata.Dimensions) cells = checked(cells * dimension.Values.Count);
            return cells;
        }
        catch (OverflowException) { return null; }
    }

    private static TimeOrdering ValidateAnnualOrdering(IReadOnlyDimension dimension)
    {
        int direction = 0;
        int previous = 0;
        IReadOnlyList<string> codes = dimension.ValueCodes;
        for (int index = 0; index < dimension.Values.Count; index++)
        {
            string code = codes[index];
            if (code.Length != 4 || !int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out int year) || year < 1)
                return new(null);
            if (index > 0)
            {
                int difference = Math.Sign(year - previous);
                if (difference == 0 || direction != 0 && difference != direction) return new(null);
                direction = difference;
            }
            previous = year;
        }
        if (dimension.Values.Count == 0) return new(null);
        return new(direction < 0 ? 0 : dimension.Values.Count - 1);
    }

    private sealed record TimeOrdering(int? LatestIndex);
}