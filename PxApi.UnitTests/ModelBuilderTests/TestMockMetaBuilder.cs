using Px.Utils.Language;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using PxApi.ModelBuilders;

namespace PxApi.UnitTests.ModelBuilderTests
{
    internal static class TestMockMetaBuilder
    {
        internal static MatrixMetadata GetMockMetadata(DimensionType[]? additionalDimensions = null, Dictionary<string, MetaProperty>?[]? dimensionAdditionalProps = null)
        {
            string defaultLang = "fi";
            List<string> availableLangs = ["fi", "sv", "en"];

            List<Dimension> additional = [];
            for(int i = 0; i < additionalDimensions?.Length; i++)
            {
                additional.Add(GetMockDimension($"dim{1 + i}", additionalDimensions[i], dimensionAdditionalProps?[4 + i]));
            }

            List<Dimension> dimensions = [
                GetMockContentDimension("content"),
                GetMockTimeDimension("time"),
                GetMockDimension("dim0", DimensionType.Ordinal),
                GetMockDimension("dim1", DimensionType.Nominal),
                ..additional
                ];

            MultilanguageString description = new([
                new("fi", "table-description.fi"),
                new("sv", "table-description.sv"),
                new("en", "table-description.en"),
            ]);

            MultilanguageString contents = new([
                new("fi", "table-contents.fi"),
                new("sv", "table-contents.sv"),
                new("en", "table-contents.en"),
            ]);
            
            MultilanguageString source = new([
                new("fi", "table-source.fi"),
                new("sv", "table-source.sv"),
                new("en", "table-source.en"),
            ]);

            MultilanguageString note = new([
                new("fi", "table-note.fi"),
                new("sv", "table-note.sv"),
                new("en", "table-note.en"),
            ]);

            string subjectArea = "subjcode";

            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.TABLEID, new StringProperty("table-tableid") },
                { PxFileConstants.DESCRIPTION, new MultilanguageStringProperty(description) },
                { PxFileConstants.CONTENTS, new MultilanguageStringProperty(contents) },
                { PxFileConstants.SOURCE, new MultilanguageStringProperty(source) },
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) },
                { PxFileConstants.SUBJECT_AREA, new StringProperty(subjectArea)   }
            };

            return new(defaultLang, availableLangs, dimensions, props);
        }

        internal static Dimension GetMockDimension(string identifier, DimensionType type, Dictionary<string, MetaProperty>? additionalProps = null)
        {
            MultilanguageString name = new([
                new("fi", $"{identifier}-name.fi"),
                new("sv", $"{identifier}-name.sv"),
                new("en", $"{identifier}-name.en"),
            ]);

            List<DimensionValue> values =
            [
                GetMockDimensionValue($"{identifier}-value0"),
                GetMockDimensionValue($"{identifier}-value1"),
            ];

            MultilanguageString note = new([
                new("fi", $"{identifier}-note.fi"),
                new("sv", $"{identifier}-note.sv"),
                new("en", $"{identifier}-note.en"),
            ]);


            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) },
            };
            if (additionalProps != null)
            {
                foreach (KeyValuePair<string, MetaProperty> kvp in additionalProps)
                {
                    props.TryAdd(kvp.Key, kvp.Value);
                }
            }
            
            return new Dimension($"{identifier}-code", name, props, values, type);
        }

        private static TimeDimension GetMockTimeDimension(string identifier)
        {
            MultilanguageString name = new([
                new("fi", $"{identifier}-name.fi"),
                new("sv", $"{identifier}-name.sv"),
                new("en", $"{identifier}-name.en"),
            ]);

            List<DimensionValue> values =
            [
                GetMockDimensionValue($"{identifier}-value0"),
                GetMockDimensionValue($"{identifier}-value1"),
            ];

            MultilanguageString note = new([
                new("fi", $"{identifier}-note.fi"),
                new("sv", $"{identifier}-note.sv"),
                new("en", $"{identifier}-note.en"),
            ]);

            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) }
            };

            return new TimeDimension($"{identifier}-code", name, props, values, TimeDimensionInterval.Year);
        }

        internal static ContentDimension GetMockContentDimension(string identifier)
        {
            MultilanguageString name = new([
                new("fi", $"{identifier}-name.fi"),
                new("sv", $"{identifier}-name.sv"),
                new("en", $"{identifier}-name.en"),
            ]);

            List<ContentDimensionValue> values =
            [
                GetMockContentValue($"{identifier}-value0"),
                GetMockContentValue($"{identifier}-value1"),
            ];

            MultilanguageString note = new([
                new("fi", $"{identifier}-note.fi"),
                new("sv", $"{identifier}-note.sv"),
                new("en", $"{identifier}-note.en"),
            ]);

            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) }
            };

            return new ContentDimension($"{identifier}-code", name, props, values);
        }

        internal static DimensionValue GetMockDimensionValue(string identifier)
        {
            MultilanguageString name = new([
                new("fi", $"{identifier}-name.fi"),
                new("sv", $"{identifier}-name.sv"),
                new("en", $"{identifier}-name.en"),
            ]);

            MultilanguageString note = new([
                new("fi", $"{identifier}-note.fi"),
                new("sv", $"{identifier}-note.sv"),
                new("en", $"{identifier}-note.en"),
            ]);

            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) }
            };

            return new DimensionValue($"{identifier}-code", name, false, props);
        }

        internal static ContentDimensionValue GetMockContentValue(string identifier)
        {
            DimensionValue value = GetMockDimensionValue(identifier);

            MultilanguageString unit = new([
                new("fi", $"{identifier}-unit.fi"),
                new("sv", $"{identifier}-unit.sv"),
                new("en", $"{identifier}-unit.en"),
            ]);

            DateTime lastUpdated = new(2024, 10, 10, 0, 0, 0, DateTimeKind.Utc);

            return new ContentDimensionValue(value, unit, lastUpdated, 2);
        }
    }
}
