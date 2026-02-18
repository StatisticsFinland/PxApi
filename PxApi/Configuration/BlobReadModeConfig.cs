namespace PxApi.Configuration
{
    /// <summary>
    /// Optional configuration for blob read mode selection thresholds used by
    /// <see cref="Utilities.BlobReadModeSelector"/> to choose between streaming and windowed reads.
    /// When the configuration section is not present, default values are used.
    /// </summary>
    /// <param name="section">Configuration section containing blob read mode settings.</param>
    public class BlobReadModeConfig(IConfigurationSection section)
    {
        private const long DefaultSmallThreshold = 2_000_000;
        private const long DefaultMaxWindowedReadSize = 10_000_000;
        private const long DefaultReadWindowGap = 500_000;

        /// <summary>
        /// Blob sizes below this threshold are always streamed.
        /// Also used as the threshold for the starting linear read index
        /// to decide whether to skip ahead when streaming.
        /// Default is 2,000,000.
        /// </summary>
        public long SmallThreshold { get; } = ValidatePositive(
            section.GetValue<long>(nameof(SmallThreshold), DefaultSmallThreshold),
            nameof(SmallThreshold));

        /// <summary>
        /// Maximum read span length (in linearized index space) for which windowed reading is preferred.
        /// If the span exceeds this value, streaming is used instead.
        /// Default is 10,000,000.
        /// </summary>
        public long MaxWindowedReadSize { get; } = ValidatePositive(
            section.GetValue<long>(nameof(MaxWindowedReadSize), DefaultMaxWindowedReadSize),
            nameof(MaxWindowedReadSize));

        /// <summary>
        /// Minimum gap length (in linearized index space) between selected indices
        /// that qualifies for subtraction from the span when evaluating read density.
        /// Default is 500,000.
        /// </summary>
        public long ReadWindowGap { get; } = ValidatePositive(
            section.GetValue<long>(nameof(ReadWindowGap), DefaultReadWindowGap),
            nameof(ReadWindowGap));

        private static long ValidatePositive(long value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentException($"BlobReadMode:{name} must be greater than 0, but was {value}.");
            }
            return value;
        }
    }
}
