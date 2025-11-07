namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for query limits in data endpoints.
    /// </summary>
    /// <param name="configuration">The configuration section containing query limits settings.</param>
    public class QueryLimitsConfig(IConfigurationSection configuration)
    {
        /// <summary>
        /// Maximum number of cells allowed in JSON data endpoint requests.
        /// If not set or less than 1, no limit is applied.
        /// </summary>
        public long JsonMaxCells { get; } = GetLongValue(configuration, "JsonMaxCells");

        /// <summary>
        /// Maximum number of cells allowed in JSON-stat endpoint requests.
        /// If not set or less than 1, no limit is applied.
        /// </summary>
        public long JsonStatMaxCells { get; } = GetLongValue(configuration, "JsonStatMaxCells");

        private static long GetLongValue(IConfigurationSection configuration, string key)
        {
            long value = configuration.GetValue(key, long.MaxValue);
            if (value < 1) throw new ArgumentException($"Configuration {key} must be greater than 0.");
            return value;
        }
    }
}