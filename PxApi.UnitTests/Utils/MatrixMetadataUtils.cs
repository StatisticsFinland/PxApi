using Px.Utils.Language;
using Px.Utils.ModelBuilders;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.PxFile.Metadata;
using System.Text;

namespace PxApi.UnitTests.Utils
{
    internal static class MatrixMetadataUtils
    {
        internal static async Task<IReadOnlyMatrixMetadata> GetMetadataFromFixture(string fixture)
        {
            PxFileMetadataReader reader = new();
            MemoryStream metadataStream = new(Encoding.UTF8.GetBytes(fixture));
            IAsyncEnumerable<KeyValuePair<string, string>> entries = reader.ReadMetadataAsync(metadataStream, Encoding.UTF8);
            MatrixMetadataBuilder builder = new();
            return await builder.BuildAsync(entries);
        }

        internal static IReadOnlyMatrixMetadata CreateMetadata(int[] valueAmounts, string[] languages)
        {
            List<Dimension> dimensions = [];
            for (int i = 0; i < valueAmounts.Length; i++)
            {
                dimensions.Add(CreateDimension(i, valueAmounts[i], languages));
            }

            return new MatrixMetadata(
                languages[0],
                languages,
                dimensions,
                []
            );
        }

        internal static Dimension CreateDimension(int index, int valuesCount, string[] languages)
        {
            string code = $"dim{index}";

            DimensionType type = index switch
            {
                0 => DimensionType.Content,
                1 => DimensionType.Time,
                _ => DimensionType.Other
            };

            return new Dimension(
                code,
                CreateMultilanguageString(code, languages),
                [],
                CreateDimensionValues(code, valuesCount, languages),
                type
            );
        }

        internal static ValueList CreateDimensionValues(string dimensionName, int amount, string[] languages)
        {
            List<DimensionValue> values = [];
            for (int i = 0; i < amount; i++)
            {
                values.Add(CreateDimensionValue(dimensionName, i, languages));
            }
            return new(values);
        }

        internal static DimensionValue CreateDimensionValue(string dimensionName, int index, string[] languages)
        {
            string code = $"{dimensionName}-val{index}";
            return new DimensionValue(
                code,
                CreateMultilanguageString(code, languages)
            );
        }

        internal static MultilanguageString CreateMultilanguageString(string text, string[] languages)
        {
            Dictionary<string, string> langsAndTranslations = [];
            foreach (string lang in languages)
            {
                langsAndTranslations.Add(lang, $"{text}.{lang}");
            }
            return new(langsAndTranslations);
        }
    }
}

