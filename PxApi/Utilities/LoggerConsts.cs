using System.Text.Json;

namespace PxApi.Utilities
{
    /// <summary>
    /// Shared logging scope keys and query-event contract.
    /// </summary>
    public static class LoggerConsts
    {
        /// <summary>
        /// Used to identify a class scope in logging.
        /// </summary>
        public const string CONTROLLER = "controller";
        /// <summary>
        /// Used to identify a function scope in logging.
        /// </summary>
        public const string FUNCTION = "function";
        /// <summary>
        /// Used to identify an action name in logging.
        /// </summary>
        public const string ACTION = "action";
        /// <summary>
        /// Used to identify a database identifier in logging.
        /// </summary>
        public const string DB_ID = "database_id";
        /// <summary>
        /// Used to identify a Px file identifier in logging.
        /// </summary>
        public const string PX_FILE = "px_file";
        /// <summary>
        /// Used to identify an auxiliary file path (e.g. Alias_{lang}.txt) in logging scopes.
        /// </summary>
        public const string AUXILIARY_PATH = "aux_path";
        /// <summary>
        /// Unique name of the blob container in logging scopes.
        /// </summary>
        public const string CONTAINER_NAME = "container_name";
        /// <summary>
        /// Used to identify a content dimension value code in logging scopes.
        /// </summary>
        public const string CONTENT_VALUE_CODE = "content_value_code";
        /// <summary>
        /// Used to identify a blob name in logging scopes.
        /// </summary>
        public const string BLOB_NAME = "blob_name";
        /// <summary>
        /// Used to identify a sanitized search query in logging scopes.
        /// </summary>
        public const string SEARCH_QUERY = "search_query";
        /// <summary>
        /// Placeholder value used in logging scopes when a requested resource was not found.
        /// </summary>
        public const string NOT_FOUND_PLACEHOLDER = "not_found";

        /// <summary>Placeholder for unavailable logging context.</summary>
        public const string UNKNOWN_PLACEHOLDER = "unknown";

        /// <summary>Fixed audit-log fields and fallback values; configured HTTP header names remain unchanged.</summary>
        internal static class Audit
        {
            public const string CategoryValue = "audit";
            public const string Anonymous = "anonymous";
            public const string Unknown = UNKNOWN_PLACEHOLDER;

            /// <summary>Structured fields emitted by audit records.</summary>
            public static class Fields
            {
                public const string Category = "category";
                public const string User = "user";
                public const string ClientIp = "client_ip";
            }
        }

        /// <summary>Provider-neutral query logging contract, separate from diagnostic scope keys.</summary>
        internal static class Query
        {
            /// <summary>Name of the query completion event.</summary>
            public const string CompletionEventName = "query_completed";

            /// <summary>Structured fields emitted by query observations.</summary>
            public static class Fields
            {
                public const string EventName = "event_name";
                public const string SchemaVersion = "schema_version";
                public const string EventId = "event_id";
                public const string RequestId = "request_id";
                public const string TraceId = "trace_id";
                public const string SpanId = "span_id";
                public const string Operation = "operation";
                public const string Method = "method";
                public const string RouteTemplate = "route_template";
                public const string RequestedDatabaseId = "requested_database_id";
                public const string RequestedTableId = "requested_table_id";
                public const string RequestedLanguage = "requested_language";
                public const string RequestedFormat = "requested_format";
                public const string DatabaseId = DB_ID;
                public const string TableId = "table_id";
                public const string Language = "language";
                public const string Format = "format";
                public const string StatusCode = "status_code";
                public const string Outcome = "outcome";
                public const string ErrorCode = "error_code";
                public const string DurationMs = "duration_ms";
                public const string RequestedCells = "requested_cells";
                public const string ReturnedCells = "returned_cells";
                public const string TableCells = "table_cells";
                public const string DimensionCount = "dimension_count";
                public const string ExplicitFilterDimensionCount = "explicit_filter_dimension_count";
                public const string DefaultedDimensionCount = "defaulted_dimension_count";
                public const string VaryingDimensionCount = "varying_dimension_count";
                public const string SelectionClassificationVersion = "selection_classification_version";
                public const string SelectionCodeLimit = "selection_code_limit";
                public const string DimensionsJson = "dimensions_json";
                public const string DimensionsTruncated = "dimensions_truncated";
                public const string SelectionDetailsTruncated = "selection_details_truncated";
                public const string SelectionDetailMode = "selection_detail_mode";
                public const string DataCacheOutcome = "data_cache_outcome";
                public const string MetadataCacheHits = "metadata_cache_hits";
                public const string MetadataCacheMisses = "metadata_cache_misses";
                public const string FileListCacheHits = "file_list_cache_hits";
                public const string FileListCacheMisses = "file_list_cache_misses";
                public const string Page = "page";
                public const string PageSize = "page_size";
                public const string TotalTables = "total_tables";
                public const string ReturnedTables = "returned_tables";
                public const string ReturnedTableIds = "returned_table_ids";
                public const string ResultTableIds = "result_table_ids";
                public const string SearchScope = "search_scope";
                public const string SearchTarget = "search_target";
                public const string SearchText = "search_text";
                public const string SearchTextMode = "search_text_mode";
                public const string SearchTextTruncated = "search_text_truncated";
                public const string TotalMatches = "total_matches";
                public const string TotalMatchesRelation = "total_matches_relation";
                public const string ReturnedMatches = "returned_matches";
                public const string OriginalFormat = "{OriginalFormat}";
                public const string JsonSuffix = "_json";
                public const string TruncatedSuffix = "_truncated";
            }

            /// <summary>Property names within serialized dimension summaries.</summary>
            public static class DimensionFields
            {
                public const string Code = "code";
                public const string CodeTruncated = "code_truncated";
                public const string FilterType = "filter_type";
                public const string IsDefaulted = "is_defaulted";
                public const string SelectedCount = "selected_count";
                public const string TotalCount = "total_count";
                public const string SelectionClasses = "selection_classes";
                public const string CodeCaptureStatus = "code_capture_status";
                public const string SelectedCodes = "selected_codes";
                public const string RequestedCodeCount = "requested_code_count";
                public const string HasWildcard = "has_wildcard";
                public const string RequestedCount = "requested_count";
                public const string IsLatest = "is_latest";
            }

            /// <summary>Configuration keys controlling query detail capture.</summary>
            public static class Configuration
            {
                public const string SelectionDetailMode = "QueryLogging:SelectionDetailMode";
                public const string CaptureSearchText = "QueryLogging:CaptureSearchText";
                public const string CaptureResultIds = "QueryLogging:CaptureResultIds";
            }

            /// <summary>Fixed values shared by configuration and selection classes.</summary>
            public static class Values
            {
                public const string Off = "off";
                public const string Counts = "counts";
                public const string Codes = "codes";
                public const string DefaultFilter = "default";
                public const string FromStart = "from_start";
                public const string FromEnd = "from_end";
                public const string All = "all";
                public const string DefaultValue = "default_value";
                public const string Other = "other";
            }

            /// <summary>Reasons for rejecting or failing a tracked request.</summary>
            public enum ErrorCode
            {
                Unknown,
                InvalidFilter,
                InvalidModel,
                InvalidLanguage,
                InvalidParameters,
                InvalidPaging,
                InvalidSearch,
                DatabaseNotFound,
                TableNotFound,
                NotFound,
                FeatureDisabled,
                TooManyCells,
                SearchUnavailable,
                DataUnavailable,
                MetadataUnavailable,
                Unavailable,
                SerializationFailed,
                ClientDisconnected,
                CancellationUnknown,
                Unauthorized,
                Forbidden,
                UnsupportedFormat,
                UnsupportedContentType
            }

            /// <summary>Tracked query operations.</summary>
            public enum Operation { Data, Metadata, Tables, Search }

            /// <summary>Final request outcomes.</summary>
            public enum Outcome { Success, Rejected, Failed, Cancelled }

            /// <summary>Observed data-cache decisions.</summary>
            public enum DataCacheOutcome { NotUsed, Unknown, ExactHit, SupersetHit, Miss }

            /// <summary>Lookup caches contributing request-level counters.</summary>
            public enum Cache { Metadata, FileList }

            /// <summary>Selection detail privacy modes.</summary>
            public enum DetailMode { Counts, Codes, Off }

            /// <summary>Whether a selected code list is complete or omitted for cardinality.</summary>
            public enum CodeCaptureStatus { Complete, OverLimit }

            /// <summary>Discovery search scopes.</summary>
            public enum SearchScope { Global, Database }

            /// <summary>Search-text retention modes.</summary>
            public enum SearchTextMode { Off, Sanitized }

            /// <summary>Requested response-format classifications.</summary>
            public enum RequestedFormat { Default, Csv, Json, Wildcard, Other }

            /// <summary>Converts a contract enum to its stable lowercase, underscore-separated log value.</summary>
            public static string Value(Enum value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
        }
    }
}
