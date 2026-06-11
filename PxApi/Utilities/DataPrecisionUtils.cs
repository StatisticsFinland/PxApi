using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;

namespace PxApi.Utilities
{
    /// <summary>
    /// Provides helper methods for applying metadata-defined precision to data values.
    /// </summary>
    internal static class DataPrecisionUtils
    {
        /// <summary>
        /// Applies content value precision to existing numeric data values based on the content dimension value
        /// associated with each flattened matrix index.
        /// </summary>
        /// <param name="data">Flattened data array in matrix order.</param>
        /// <param name="meta">Metadata of the requested matrix matching <paramref name="data"/> layout.</param>
        /// <returns>A new array where existing numeric values are rounded by matching content value precision.</returns>
        public static DoubleDataValue[] ApplyContentPrecision(DoubleDataValue[] data, IReadOnlyMatrixMetadata meta)
        {
            int contentDimensionPosition = -1;
            for (int i = 0; i < meta.Dimensions.Count; i++)
            {
                if (meta.Dimensions[i].Type == DimensionType.Content)
                {
                    contentDimensionPosition = i;
                    break;
                }
            }

            if (contentDimensionPosition < 0 
                || meta.Dimensions[contentDimensionPosition] is not ContentDimension contentDimension
                || contentDimension.Values.Count == 0)
            {
                return [.. data];
            }

            int contentStride = 1;
            for (int i = contentDimensionPosition + 1; i < meta.Dimensions.Count; i++)
            {
                contentStride *= meta.Dimensions[i].Values.Count;
            }

            DoubleDataValue[] roundedData = [.. data];

            for (int dataIndex = 0; dataIndex < roundedData.Length; dataIndex++)
            {
                DoubleDataValue value = roundedData[dataIndex];
                if (value.Type != DataValueType.Exists)
                {
                    continue;
                }

                int contentValueIndex = (dataIndex / contentStride) % contentDimension.Values.Count;
                int precision = contentDimension.Values[contentValueIndex].Precision;
                double roundedValue = Math.Round(value.UnsafeValue, precision, MidpointRounding.AwayFromZero);
                roundedData[dataIndex] = new DoubleDataValue(roundedValue, DataValueType.Exists);
            }

            return roundedData;
        }
    }
}