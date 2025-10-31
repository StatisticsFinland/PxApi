using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata.Enums;
using PxApi.Utilities;
using System.Globalization;
using System.Text;
using Px.Utils.Models.Data;
using Px.Utils.Language;

namespace PxApi.ModelBuilders
{
    /// <summary>
    /// Collection of static methods for building CSV responses from matrix requestMeta and data values.
    /// </summary>
    public static class CsvBuilder
    {
        /// <summary>
        /// Builds a CSV format response from matrix requestMeta and data values.
        /// </summary>
        /// <param name="requestMeta">Input <see cref="IReadOnlyMatrixMetadata"/> containing the structure and requestMeta</param>
        /// <param name="data">The <see cref="DoubleDataValue"/> array containing the actual data values</param>
        /// <param name="lang">Language of the response</param>
        /// <param name="completeMeta">The complete <see cref="IReadOnlyMatrixMetadata"/> for reference (used for filtering dimensions)</param>
        /// <returns>CSV formatted string representing the data</returns>
        /// <exception cref="InvalidOperationException">Thrown when DESCRIPTION meta property is missing</exception>
        public static string BuildCsvResponse(IReadOnlyMatrixMetadata requestMeta, DoubleDataValue[] data, string lang, IReadOnlyMatrixMetadata completeMeta)
        {
            // Get header for A1 cell (table description)
            string header = MatrixMetadataUtilityFunctions.GetValueByLanguage(requestMeta.AdditionalProperties, PxFileConstants.DESCRIPTION, lang) ??
            throw new InvalidOperationException("DESCRIPTION meta property is required for CSV export.");

            StringBuilder csv = new();

            // Get stub and heading dimensions from requestMeta
            string[] stubDimensions = GetDimensionNamesFromMetaPropertyForLanguage(requestMeta.AdditionalProperties, PxFileConstants.STUB, lang);
            string[] headingDimensions = GetDimensionNamesFromMetaPropertyForLanguage(requestMeta.AdditionalProperties, PxFileConstants.HEADING, lang);

            string[] stubDimensionCodes = [.. stubDimensions.Select(s =>
                requestMeta.Dimensions.FirstOrDefault(d => d.Name[lang] == s)?.Code ?? s)];
            string[] headingDimensionCodes = [.. headingDimensions.Select(s =>
                requestMeta.Dimensions.FirstOrDefault(d => d.Name[lang] == s)?.Code ?? s)];

            List<IDimensionMap> orderedDimensionMaps = [];

            // Add stub dimensions first (these become rows)
            foreach (string stubDimCode in stubDimensionCodes)
            {
                IReadOnlyDimension? stubDim = requestMeta.Dimensions.FirstOrDefault(d => d.Code == stubDimCode);
                if (stubDim != null)
                {
                    List<string> allValueCodes = [.. stubDim.Values.Select(v => v.Code)];
                    orderedDimensionMaps.Add(new DimensionMap(stubDimCode, allValueCodes));
                }
            }

            // Add heading dimensions last (these become columns)
            foreach (string headingDimCode in headingDimensionCodes)
            {
                IReadOnlyDimension? headingDim = requestMeta.Dimensions.FirstOrDefault(d => d.Code == headingDimCode);
                if (headingDim != null)
                {
                    List<string> allValueCodes = [.. headingDim.Values.Select(v => v.Code)];
                    orderedDimensionMaps.Add(new DimensionMap(headingDimCode, allValueCodes));
                }
            }

            // Create the ordered matrix map and transform requestMeta accordingly
            MatrixMap csvOrderedMap = new(orderedDimensionMaps);
            IReadOnlyMatrixMetadata orderedMetadata = requestMeta.GetTransform(csvOrderedMap);

            // Find the ordered dimensions to stub and heading groups
            List<IReadOnlyDimension> orderedStubDims = [];
            List<IReadOnlyDimension> orderedHeadingDims = [];

            // Split the ordered dimensions to stub and heading groups
            for (int i = 0; i < stubDimensions.Length && i < orderedMetadata.Dimensions.Count; i++)
            {
                orderedStubDims.Add(orderedMetadata.Dimensions[i]);
            }

            for (int i = stubDimensions.Length; i < orderedMetadata.Dimensions.Count; i++)
            {
                orderedHeadingDims.Add(orderedMetadata.Dimensions[i]);
            }

            // Filter out dimensions with only ELIMINATION value or if there's only one value available
            Dictionary<string, int> completeDimensionSizes = [];
            foreach (IReadOnlyDimension dim in completeMeta.Dimensions)
            {
                completeDimensionSizes[dim.Code] = dim.Values.Count;
            }
            List<IReadOnlyDimension> filteredStubDims = FilterSingleValueDimensions(orderedStubDims, completeDimensionSizes);
            List<IReadOnlyDimension> filteredHeadingDims = FilterSingleValueDimensions(orderedHeadingDims, completeDimensionSizes);

            BuildHeaderRow(csv, header, filteredHeadingDims, lang);
            BuildDataRows(csv, data, filteredStubDims, filteredHeadingDims, lang);

            return csv.ToString();
        }

        /// <summary>
        /// Filters out dimensions that have only one value that is an elimination (total) value or if the complete dimension has only one value.
        /// This makes the CSV headers cleaner by omitting redundant dimension information.
        /// </summary>
        /// <param name="dimensions">The dimensions to filter</param>
        /// <param name="completeDimensionSizes">Dictionary of complete dimension sizes for reference</param>
        /// <returns>List of dimensions that should be included in headers</returns>
        private static List<IReadOnlyDimension> FilterSingleValueDimensions(List<IReadOnlyDimension> dimensions, Dictionary<string, int> completeDimensionSizes)
        {
            return [.. dimensions.Where(dim =>
            {
                // Keep dimensions with multiple values
                if (dim.Values.Count > 1) return true;

                if (dim.Values.Count == 1)
                {
                    // If it's an elimination/total value or the complete map only has one value for the dimension, omit it from headers
                    return !IsEliminationValue(dim, dim.Values[0]) && completeDimensionSizes[dim.Code] > 1;
                }

                return true;
            })];
        }

        /// <summary>
        /// Checks if a dimension value is an elimination (total) value using the same logic as MetaFiltering.
        /// </summary>
        /// <param name="dimension">The dimension to check</param>
        /// <param name="value">The value to check</param>
        /// <returns>True if the value is an elimination value</returns>
        private static bool IsEliminationValue(IReadOnlyDimension dimension, IReadOnlyDimensionValue value)
        {
            if (dimension.AdditionalProperties.TryGetValue("ELIMINATION", out MetaProperty? prop))
            {
                if (prop.Type == MetaPropertyType.Text)
                {
                    string valCode = ((StringProperty)prop).Value;
                    return value.Code.Equals(valCode);
                }
                else if (prop.Type == MetaPropertyType.MultilanguageText)
                {
                    MultilanguageString valName = ((MultilanguageStringProperty)prop).Value;
                    return value.Name.Equals(valName);
                }
            }
            return false;
        }

        private static string[] GetDimensionNamesFromMetaPropertyForLanguage(IReadOnlyDictionary<string, MetaProperty> properties, string key, string lang)
        {
            string[]? dimensionList = MatrixMetadataUtilityFunctions.GetValueListByLanguage(properties, key, lang);
            if (dimensionList is null || dimensionList.Length == 0)
            {
                return [];
            }
            else return dimensionList;
        }

        private static void BuildHeaderRow(StringBuilder csv, string header, List<IReadOnlyDimension> headingDims, string lang)
        {
            // Start with the A1 cell (description)
            csv.Append($"\"{header}\"");

            // Add heading dimension value combinations as column headers
            if (headingDims.Count > 0)
            {
                List<string[]> headingCombinations = GetValueCombinations(headingDims, lang);
                foreach (string[] combination in headingCombinations)
                {
                    csv.Append(',');
                    csv.Append($"\"{string.Join(" ", combination)}\"");
                }
            }

            csv.AppendLine();
        }

        private static void BuildDataRows(StringBuilder csv, DoubleDataValue[] data, List<IReadOnlyDimension> filteredStubDims, List<IReadOnlyDimension> filteredHeadingDims, string lang)
        {
            if (filteredStubDims.Count == 0 && filteredHeadingDims.Count == 0)
            {
                // If no dimensions to display, create a single row with all data
                csv.Append("\"\"");
                for (int i = 0; i < data.Length; i++)
                {
                    csv.Append(',');
                    csv.Append(FormatDataValue(data[i]));
                }
                csv.AppendLine();
                return;
            }

            // Generate combinations for filtered dimensions (which are the same for data indexing)
            List<string[]> stubCombinations = GetValueCombinations(filteredStubDims, lang);
            int headingCount = filteredHeadingDims.Count > 0 ? GetValueCombinations(filteredHeadingDims, lang).Count : 1;

            // If no stub dimensions to display, create one empty row for data
            if (stubCombinations.Count == 0)
            {
                stubCombinations = [[]];
            }

            for (int stubIndex = 0; stubIndex < stubCombinations.Count; stubIndex++)
            {
                string[] stubCombination = stubCombinations[stubIndex];

                // Add row header (filtered stub dimension values)
                if (stubCombination.Length > 0)
                {
                    csv.Append($"\"{string.Join(" ", stubCombination)}\"");
                }
                else
                {
                    // If no stub dimensions to display, use empty string
                    csv.Append("\"\"");
                }

                // Add data values for this row
                for (int headingIndex = 0; headingIndex < headingCount; headingIndex++)
                {
                    csv.Append(',');
                    int dataIndex = stubIndex * headingCount + headingIndex;

                    if (dataIndex < data.Length)
                    {
                        DoubleDataValue value = data[dataIndex];
                        csv.Append(FormatDataValue(value));
                    }
                }

                if (stubIndex < stubCombinations.Count - 1)
                {
                    csv.AppendLine();
                }
            }
        }

        private static List<string[]> GetValueCombinations(List<IReadOnlyDimension> dimensions, string lang)
        {
            if (dimensions.Count == 0)
            {
                return [];
            }

            int totalCombinations = dimensions.Aggregate(1, (total, dim) => total * dim.Values.Count);
            List<string[]> result = [];

            // Generate all combinations starting from the innermost loop
            for (int i = 0; i < totalCombinations; i++)
            {
                string[] combination = new string[dimensions.Count];
                int remainder = i;

                // Work backwards through dimensions (rightmost first)
                for (int dimIndex = dimensions.Count - 1; dimIndex >= 0; dimIndex--)
                {
                    IReadOnlyDimension dimension = dimensions[dimIndex];
                    int valueIndex = remainder % dimension.Values.Count;
                    remainder /= dimension.Values.Count;

                    IReadOnlyDimensionValue value = dimension.Values[valueIndex];
                    string valueName = value.Name[lang] ?? value.Code;
                    combination[dimIndex] = valueName;
                }

                result.Add(combination);
            }

            return result;
        }

        private static string FormatDataValue(DoubleDataValue value)
        {
            if (value.Type == DataValueType.Exists)
            {
                // Use period as decimal separator, no thousands separator
                return value.UnsafeValue.ToString("0.###################", CultureInfo.InvariantCulture);
            }
            else
            {
                // Convert missing value type to dot codes
                return GetMissingValueDotCode(value.Type);
            }
        }

        private static string GetMissingValueDotCode(DataValueType type)
        {
            return type switch
            {
                DataValueType.Missing => ".",      // index 0
                DataValueType.CanNotRepresent => "..",  // index 1
                DataValueType.Confidential => "...",   // index 2
                DataValueType.NotAcquired => "....",   // index 3
                DataValueType.NotAsked => ".....",     // index 4
                DataValueType.Empty => "......",     // index 5
                DataValueType.Nill => "-",       // index 6
                _ => "."  // default to single dot for unknown types
            };
        }
    }
}