using PxApi.Utilities;
using Microsoft.Extensions.Configuration;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.Models.QueryFilters;
using PxApi.Services;
using PxApi.UnitTests.Utils;
using System.Text.Json;
using System.Diagnostics;

namespace PxApi.UnitTests.Services;

[TestFixture]
public class QuerySelectionSummaryTests
{
    [TestCase("first", 1, "from_start")]
    [TestCase("first", 3, "from_start")]
    [TestCase("last", 1, "from_end")]
    [TestCase("last", 3, "from_end")]
    [TestCase("from", 3, "from_end")]
    [TestCase("to", 3, "from_start")]
    public void Data_ResolvedEdges_ClassifyIndependentlyOfSyntax(string type, int count, string expectedClass)
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([5], ["en"]);
        Filter filter = type switch
        {
            "first" => new FirstFilter(count),
            "last" => new LastFilter(count),
            "from" => new FromFilter("dim0-val2"),
            _ => new ToFilter { FilterString = "dim0-val2" }
        };
        Dictionary<string, object?> fields = Capture(metadata, new() { ["dim0"] = filter });
        JsonElement dimension = Dimensions(fields)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dimension.GetProperty("filter_type").GetString(), Is.EqualTo(type));
            Assert.That(dimension.GetProperty("selected_count").GetInt32(), Is.EqualTo(count));
            Assert.That(Classes(dimension), Does.Contain(expectedClass));
        }
    }

    [TestCase(1)]
    [TestCase(5)]
    public void Data_FullSelection_HasAllOverlappingClasses(int count)
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([count], ["en"]);
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["dim0"] = new CodeFilter(["*"]) }))[0];

        Assert.That(Classes(dimension), Is.EquivalentTo(new[] { "from_start", "from_end", "all" }));
    }

    [Test]
    public void Data_NoncontiguousEndpoints_AreOther()
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([5], ["en"]);
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["dim0"] = new CodeFilter(["dim0-val0", "dim0-val4"]) }))[0];

        Assert.That(Classes(dimension), Is.EqualTo(new[] { "other" }));
    }

    [Test]
    public void Data_CodeAndLast_ShareCountsAndClassButNotIntent()
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([5], ["en"]);
        Dictionary<string, object?> code = Capture(metadata, new() { ["dim0"] = new CodeFilter(["dim0-val4"]) });
        Dictionary<string, object?> last = Capture(metadata, new() { ["dim0"] = new LastFilter(1) });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(code["requested_cells"], Is.EqualTo(last["requested_cells"]));
            Assert.That(Dimensions(code)[0].GetProperty("filter_type").GetString(), Is.EqualTo("code"));
            Assert.That(Dimensions(last)[0].GetProperty("filter_type").GetString(), Is.EqualTo("last"));
            Assert.That(Classes(Dimensions(code)[0]), Is.EqualTo(Classes(Dimensions(last)[0])));
        }
    }

    [Test]
    public void Data_ExplicitAndImplicitDefaults_PreserveIntentAndOverallCounts()
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([3, 4, 2], ["en"]);
        Dictionary<string, object?> implicitSelection = Capture(metadata, []);
        Dictionary<string, object?> explicitSelection = Capture(metadata, new() { ["DIM0"] = new CodeFilter(["dim0-val0"]) });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(implicitSelection["requested_cells"], Is.EqualTo(explicitSelection["requested_cells"]));
            Assert.That(implicitSelection["requested_cells"], Is.EqualTo(4L));
            Assert.That(implicitSelection["table_cells"], Is.EqualTo(24L));
            Assert.That(implicitSelection["defaulted_dimension_count"], Is.EqualTo(3));
            Assert.That(explicitSelection["explicit_filter_dimension_count"], Is.EqualTo(1));
            Assert.That(Dimensions(explicitSelection)[0].GetProperty("is_defaulted").GetBoolean(), Is.False);
        }
    }

    [TestCase(1, "complete")]
    [TestCase(10, "complete")]
    [TestCase(11, "over_limit")]
    public void Data_CodeCapture_IncludesOnlyCompleteSmallSelections(int count, string status)
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([count], ["en"]);
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["dim0"] = new CodeFilter(["*"]) }, "codes"))[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dimension.GetProperty("code_capture_status").GetString(), Is.EqualTo(status));
            Assert.That(dimension.TryGetProperty("selected_codes", out JsonElement codes), Is.EqualTo(count <= 10));
            if (count <= 10)
                Assert.That(codes.EnumerateArray().Select(code => code.GetString()),
                    Is.EqualTo(Enumerable.Range(0, count).Select(index => "dim0-val" + index)));
            Assert.That(dimension.GetProperty("requested_code_count").GetInt32(), Is.EqualTo(1));
            Assert.That(dimension.GetProperty("has_wildcard").GetBoolean(), Is.True);
        }
    }

    [Test]
    public void Data_LongEscapedCode_IsCapturedCompletely()
    {
        string longCode = new('"', 2000);
        IReadOnlyMatrixMetadata metadata = CustomMetadata([longCode]);
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["time"] = new CodeFilter(["*"]) }, "codes"))[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dimension.GetProperty("code_capture_status").GetString(), Is.EqualTo("complete"));
            Assert.That(dimension.GetProperty("selected_codes").EnumerateArray().Select(code => code.GetString()), Is.EqualTo(new[] { longCode }));
        }
    }

    [TestCase("counts")]
    [TestCase("codes")]
    [TestCase("off")]
    public void Data_AllDetailModes_PreserveCellCountsAndClassificationVersion(string mode)
    {
        IReadOnlyMatrixMetadata metadata = CustomMetadata(["2023", "2024", "2025"]);
        Dictionary<string, object?> fields = Capture(metadata, new() { ["time"] = new LastFilter(1) }, mode);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["requested_cells"], Is.EqualTo(1L));
            Assert.That(fields["table_cells"], Is.EqualTo(3L));
            Assert.That(fields["selection_classification_version"], Is.EqualTo(1));
        }
    }

    [Test]
    public void Data_ManyDimensions_BoundsSummariesAndPreservesCounts()
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata(Enumerable.Repeat(1, 100).ToArray(), ["en"]);
        Dictionary<string, object?> fields = Capture(metadata, [], "codes");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["dimensions_truncated"], Is.EqualTo(true));
            Assert.That(fields["dimension_count"], Is.EqualTo(100));
            Assert.That(fields["requested_cells"], Is.EqualTo(1L));
            Assert.That(Dimensions(fields), Has.Length.EqualTo(64));
            Assert.That(Dimensions(fields).Select(entry => entry.GetProperty("code").GetString()),
                Is.EqualTo(Enumerable.Range(0, 64).Select(index => "dim" + index)));
        }
    }

    [TestCase("counts")]
    [TestCase("off")]
    public void Data_PrivacyModes_DoNotCaptureCodes(string mode)
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([5], ["en"]);
        Dictionary<string, object?> fields = Capture(metadata, [], mode);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((string)fields["dimensions_json"]!, Does.Not.Contain("selected_codes"));
            Assert.That(fields["selection_detail_mode"], Is.EqualTo(mode));
            Assert.That((string)fields["dimensions_json"]!, Does.Not.Contain("code_capture_status"));
        }
    }

    [TestCase(false, "2025")]
    [TestCase(true, "2025")]
    public void Data_ValidatedAscendingOrDescendingAnnualTime_IdentifiesNewest(bool descending, string newest)
    {
        string[] years = descending ? ["2025", "2024", "2023"] : ["2023", "2024", "2025"];
        IReadOnlyMatrixMetadata metadata = CustomMetadata(years);
        Capture(metadata, new() { ["time"] = new CodeFilter(["*"]) });
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["time"] = new CodeFilter([newest]) }))[0];
        JsonElement older = Dimensions(Capture(metadata, new() { ["time"] = new CodeFilter(["2024"]) }))[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dimension.GetProperty("is_latest").GetBoolean(), Is.True);
            Assert.That(older.GetProperty("is_latest").GetBoolean(), Is.False);
            Assert.That(Classes(dimension), Does.Contain(descending ? "from_start" : "from_end"));
        }
    }

    [TestCase("2025", "2023", "2024")]
    [TestCase("period-a", "period-b", "period-c")]
    public void Data_UnknownChronology_DoesNotClaimLatest(string first, string second, string third)
    {
        IReadOnlyMatrixMetadata metadata = CustomMetadata([first, second, third]);
        Capture(metadata, new() { ["time"] = new CodeFilter(["*"]) });
        JsonElement dimension = Dimensions(Capture(metadata, new() { ["time"] = new LastFilter(1) }))[0];

        Assert.That(dimension.TryGetProperty("is_latest", out _), Is.False);
    }

    [Test]
    public void Data_EliminationDefault_IsDistinctFromFirstFallback()
    {
        Dimension dimension = new("dimension", MatrixMetadataUtils.CreateMultilanguageString("dimension", ["en"]),
            new() { ["ELIMINATION"] = new StringProperty("dimension-val1") }, MatrixMetadataUtils.CreateDimensionValues("dimension", 3, ["en"]), DimensionType.Other);
        IReadOnlyMatrixMetadata metadata = new MatrixMetadata("en", ["en"], [dimension], []);

        JsonElement entry = Dimensions(Capture(metadata, []))[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Classes(entry), Is.EqualTo(new[] { "default_value" }));
            Assert.That(entry.GetProperty("is_defaulted").GetBoolean(), Is.True);
        }
    }

    [Test]
    public void Metadata_RecordsTotalsWithoutSelectionClaims()
    {
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        QuerySelectionSummary.Metadata(observation, MatrixMetadataUtils.CreateMetadata([3, 4], ["en"]));
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();
        JsonElement entry = Dimensions(fields)[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["table_cells"], Is.EqualTo(12L));
            Assert.That(entry.GetProperty("total_count").GetInt32(), Is.EqualTo(3));
            Assert.That(entry.TryGetProperty("selected_count", out _), Is.False);
            Assert.That(fields.ContainsKey("defaulted_dimension_count"), Is.False);
        }
    }

    [Test]
    public async Task Observation_ConcurrentCacheLookups_AreMeasuredExactly()
    {
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        await Task.WhenAll(Enumerable.Range(0, 1000).Select(index => Task.Run(() => observation.CacheLookup(LoggerConsts.Query.Cache.Metadata, index % 2 == 0))));
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["metadata_cache_hits"], Is.EqualTo(500L));
            Assert.That(fields["metadata_cache_misses"], Is.EqualTo(500L));
            Assert.That(fields.ContainsKey("file_list_cache_hits"), Is.False);
        }
    }

    [TestCase((int)LoggerConsts.Query.ErrorCode.DatabaseNotFound, "database_not_found")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.UnsupportedContentType, "unsupported_content_type")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.Unknown, "unknown")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidFilter, "invalid_filter")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidModel, "invalid_model")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidLanguage, "invalid_language")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidParameters, "invalid_parameters")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidPaging, "invalid_paging")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.InvalidSearch, "invalid_search")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.TableNotFound, "table_not_found")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.NotFound, "not_found")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.FeatureDisabled, "feature_disabled")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.TooManyCells, "too_many_cells")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.SearchUnavailable, "search_unavailable")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.DataUnavailable, "data_unavailable")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.MetadataUnavailable, "metadata_unavailable")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.Unavailable, "unavailable")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.SerializationFailed, "serialization_failed")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.ClientDisconnected, "client_disconnected")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.CancellationUnknown, "cancellation_unknown")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.Unauthorized, "unauthorized")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.Forbidden, "forbidden")]
    [TestCase((int)LoggerConsts.Query.ErrorCode.UnsupportedFormat, "unsupported_format")]
    [TestCase(int.MaxValue, "unknown")]
    public void Observation_TypedRejection_PreservesContractValue(int reason, string expected)
    {
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        observation.Reject((LoggerConsts.Query.ErrorCode)reason);

        Dictionary<string, object?> fields = observation.Complete().ToDictionary();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["error_code"], Is.EqualTo(expected));
            Assert.That(observation.HasErrorCode, Is.True);
        }
    }

    [TestCase(typeof(LoggerConsts.Query.Fields))]
    [TestCase(typeof(LoggerConsts.Query.DimensionFields))]
    [TestCase(typeof(LoggerConsts.Query.Configuration))]
    public void Observation_ContractKeys_AreUnique(Type contract)
    {
        string[] keys = [.. contract.GetFields().Select(field => (string)field.GetRawConstantValue()!)];
        Assert.That(keys, Is.Unique);
    }

    [Test]
    public void Observation_LoggedTokens_UseLowerSnakeCase()
    {
        string[] tokens = [.. new[] { typeof(LoggerConsts), typeof(LoggerConsts.Audit), typeof(LoggerConsts.Audit.Fields), typeof(LoggerConsts.Query.Fields),
                typeof(LoggerConsts.Query.DimensionFields), typeof(LoggerConsts.Query.Values) }
            .SelectMany(contract => contract.GetFields())
            .Where(field => field.Name != nameof(LoggerConsts.Query.Fields.OriginalFormat)
                && field.Name != nameof(LoggerConsts.Query.Fields.JsonSuffix)
                && field.Name != nameof(LoggerConsts.Query.Fields.TruncatedSuffix))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(typeof(LoggerConsts.Query).GetNestedTypes().Where(type => type.IsEnum)
                .SelectMany(type => Enum.GetValues(type).Cast<Enum>()).Select(LoggerConsts.Query.Value))
            .Append(LoggerConsts.Query.CompletionEventName)];

        Assert.That(tokens, Is.All.Matches("^[a-z][a-z0-9]*(_[a-z0-9]+)*$"));
    }

    [Test]
    public void Observation_ScopeAndQueryKeys_AreUnique()
    {
        string[] keys = [.. typeof(PxApi.Utilities.LoggerConsts).GetFields()
            .Where(field => field.Name != nameof(PxApi.Utilities.LoggerConsts.NOT_FOUND_PLACEHOLDER)
                && field.Name != nameof(PxApi.Utilities.LoggerConsts.UNKNOWN_PLACEHOLDER)
                && field.Name != nameof(PxApi.Utilities.LoggerConsts.DB_ID))
            .Concat(typeof(LoggerConsts.Query.Fields).GetFields())
            .Concat(typeof(LoggerConsts.Audit.Fields).GetFields())
            .Select(field => field.GetRawConstantValue() as string
                ?? throw new InvalidOperationException($"Logging constant '{field.Name}' must be a non-null string."))];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(keys, Is.Unique);
            Assert.That(PxApi.Utilities.LoggerConsts.DB_ID, Is.EqualTo("database_id"));
            Assert.That(LoggerConsts.Query.Fields.DatabaseId, Is.EqualTo(PxApi.Utilities.LoggerConsts.DB_ID));
            Assert.That(PxApi.Utilities.LoggerConsts.PX_FILE, Is.EqualTo("px_file"));
            Assert.That(LoggerConsts.Query.Fields.TableId, Is.EqualTo("table_id"));
        }
    }

    [Test]
    public void Observation_Complete_PreservesCapturedJsonAndReturnsStableSnapshots()
    {
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        string details = JsonSerializer.Serialize(new[] { new string('"', 8000) });
        observation.Set(LoggerConsts.Query.Fields.RequestedCells, 100L);
        observation.Set(LoggerConsts.Query.Fields.DimensionsJson, details);
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["requested_cells"], Is.EqualTo(100L));
            Assert.That(fields["dimensions_json"], Is.EqualTo(details));
            Assert.That(fields.ContainsKey("dimensions_truncated"), Is.False);
            Assert.That(fields.ContainsKey("selection_details_truncated"), Is.False);
            Assert.That(fields["{OriginalFormat}"], Is.EqualTo("query_completed"));
            Assert.That(observation.Complete().ToDictionary(), Is.EqualTo(fields));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100)]
    [TestCase(101)]
    public void Observation_SearchTextAndResultIds_AreOptInAndBounded(int resultCount)
    {
        QueryObservation observation = new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QueryLogging:CaptureSearchText"] = "true",
            ["QueryLogging:CaptureResultIds"] = "true"
        }).Build());
        observation.SearchText(new string('a', 400));
        string[] ids = [.. Enumerable.Range(0, resultCount).Select(index => $"table{index:D3}-" + new string('a', 100))];
        observation.ResultIds(LoggerConsts.Query.Fields.ResultTableIds, ids);
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((string)fields["search_text"]!).Length, Is.EqualTo(128));
            Assert.That(fields["search_text_truncated"], Is.EqualTo(true));
            Assert.That(fields["result_table_ids_truncated"], Is.EqualTo(resultCount > 100));
            Assert.That(JsonSerializer.Deserialize<string[]>((string)fields["result_table_ids_json"]!), Is.EqualTo(ids.Take(100)));
        }
    }

    [Test]
    public void Data_LargeAllowedSubcube_HasBoundedSummaryAllocation()
    {
        IReadOnlyMatrixMetadata metadata = MatrixMetadataUtils.CreateMetadata([1, 50000, 1], ["en"]);
        Dictionary<string, Filter> filters = [];
        MatrixMap map = MetaFiltering.ApplyToMatrixMeta(metadata, filters);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        QuerySelectionSummary.Data(new QueryObservation(configuration), metadata, filters, map);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        QueryObservation observation = new(configuration);

        QuerySelectionSummary.Data(observation, metadata, filters, map);
        double duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();
        TestContext.Out.WriteLine($"50000-cell summary: {duration:F2} ms, {allocated} managed bytes, {JsonSerializer.SerializeToUtf8Bytes(fields).Length} payload bytes.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["requested_cells"], Is.EqualTo(50000L));
            Assert.That(allocated, Is.LessThan(2000000));
            Assert.That(Dimensions(fields), Has.Length.EqualTo(3));
        }
    }

    private static Dictionary<string, object?> Capture(IReadOnlyMatrixMetadata metadata, Dictionary<string, Filter> filters,
        string mode = "counts")
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["QueryLogging:SelectionDetailMode"] = mode
        }).Build();
        QueryObservation observation = new(configuration);
        MatrixMap map = MetaFiltering.ApplyToMatrixMeta(metadata, filters);
        QuerySelectionSummary.Data(observation, metadata, filters, map);
        return observation.Complete().ToDictionary();
    }

    private static JsonElement[] Dimensions(Dictionary<string, object?> fields)
    {
        using JsonDocument document = JsonDocument.Parse((string)fields["dimensions_json"]!);
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToArray();
    }

    private static string?[] Classes(JsonElement dimension) => dimension.GetProperty("selection_classes").EnumerateArray().Select(element => element.GetString()).ToArray();

    private static IReadOnlyMatrixMetadata CustomMetadata(string[] codes)
    {
        Dimension dimension = new("time", MatrixMetadataUtils.CreateMultilanguageString("time", ["en"]), [],
            new ValueList(codes.Select(code => new DimensionValue(code, MatrixMetadataUtils.CreateMultilanguageString(code, ["en"])))), DimensionType.Time);
        return new MatrixMetadata("en", ["en"], [dimension], []);
    }
}