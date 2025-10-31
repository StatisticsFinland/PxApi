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
                additional.Add(GetMockDimension($"dim{2 + i}", additionalDimensions[i], dimensionAdditionalProps?[4 + i]));
            }

            List<Dimension> dimensions = [
                GetMockContentDimension("content"),
                GetMockTimeDimension("time"),
                GetMockDimension("dim0", DimensionType.Ordinal, dimensionAdditionalProps?[2]),
                GetMockDimension("dim1", DimensionType.Nominal, dimensionAdditionalProps?[3]),
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

            string subjectcode = "subjcode";

            Dictionary<string, MetaProperty> props = new()
            {
                { PxFileConstants.TABLEID, new StringProperty("table-tableid") },
                { PxFileConstants.DESCRIPTION, new MultilanguageStringProperty(description) },
                { PxFileConstants.CONTENTS, new MultilanguageStringProperty(contents) },
                { PxFileConstants.SOURCE, new MultilanguageStringProperty(source) },
                { PxFileConstants.NOTE, new MultilanguageStringProperty(note) },
                { PxFileConstants.SUBJECT_CODE, new StringProperty(subjectcode)   },
                { PxFileConstants.STUB, CreateStubProperty() },
                { PxFileConstants.HEADING, CreateHeadingProperty(additional) }
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

        private static MultilanguageStringListProperty CreateStubProperty()
        {
            // STUB contains: content dimension, dim0, dim1
            List<MultilanguageString> stubDimensionNames = [
                new MultilanguageString([
                    new("fi", "content-name.fi"),
                    new("sv", "content-name.sv"),
                    new("en", "content-name.en")
                ]),
                new MultilanguageString([
                    new("fi", "dim0-name.fi"),
                    new("sv", "dim0-name.sv"),
                    new("en", "dim0-name.en")
                ]),
                new MultilanguageString([
                    new("fi", "dim1-name.fi"),
                    new("sv", "dim1-name.sv"),
                    new("en", "dim1-name.en")
                ])
            ];

            return new MultilanguageStringListProperty(stubDimensionNames);
        }

        private static MultilanguageStringListProperty CreateHeadingProperty(List<Dimension> additionalDimensions)
        {
            // HEADING contains: time dimension + any additional dimensions
            List<MultilanguageString> headingDimensionNames = [
                new MultilanguageString([
                    new("fi", "time-name.fi"),
                    new("sv", "time-name.sv"),
                    new("en", "time-name.en")
                ])
            ];

            // Add additional dimensions to HEADING
            for (int i = 0; i < additionalDimensions.Count; i++)
            {
                headingDimensionNames.Add(new MultilanguageString([
                    new("fi", $"dim{i + 2}-name.fi"),
                    new("sv", $"dim{i + 2}-name.sv"),
                    new("en", $"dim{i + 2}-name.en")
                ]));
            }

            return new MultilanguageStringListProperty(headingDimensionNames);
        }
    }
}
