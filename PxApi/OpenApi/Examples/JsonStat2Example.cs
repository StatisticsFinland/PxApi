using PxApi.Configuration;
using PxApi.Models.JsonStat;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Px.Utils.Models.Data;

namespace PxApi.OpenApi.Examples
{
    /// <summary>
    /// Provides a shared example of a JsonStat2 response for OpenAPI documentation.
    /// </summary>
    [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
    public static class JsonStat2Example
    {
        /// <summary>
        /// Gets the singleton instance of the JsonStat2 example, serialized from a real model object
        /// so the example always reflects the current schema and converter behaviour.
        /// </summary>
        public static readonly JsonNode? Instance = JsonSerializer.SerializeToNode(
            new JsonStat2
            {
                Id = ["vuosi", "sukupuoli", "ika", "tiedot"],
                Label = "Population according to age (5-year) and sex, 2014-2023",
                Source = "Statistics Finland, population structure",
                Updated = "2024-04-26T08:00:00Z",
                Dimension = new Dictionary<string, Dimension>
                {
                    ["vuosi"] = new()
                    {
                        Label = "Year",
                        Category = new Category
                        {
                            Index = ["2014", "2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022", "2023"],
                            Label = new Dictionary<string, string>
                            {
                                ["2014"] = "2014",
                                ["2015"] = "2015",
                                ["2016"] = "2016",
                                ["2017"] = "2017",
                                ["2018"] = "2018",
                                ["2019"] = "2019",
                                ["2020"] = "2020",
                                ["2021"] = "2021",
                                ["2022"] = "2022",
                                ["2023"] = "2023"
                            }
                        }
                    },
                    ["sukupuoli"] = new()
                    {
                        Label = "Sex",
                        Category = new Category
                        {
                            Index = ["SSS", "1", "2"],
                            Label = new Dictionary<string, string>
                            {
                                ["SSS"] = "Total",
                                ["1"] = "Males",
                                ["2"] = "Females"
                            }
                        }
                    },
                    ["ika"] = new()
                    {
                        Label = "Age",
                        Category = new Category
                        {
                            Index = ["SSS"],
                            Label = new Dictionary<string, string> { ["SSS"] = "Total" }
                        }
                    },
                    ["tiedot"] = new()
                    {
                        Label = "Information",
                        Category = new Category
                        {
                            Index = ["vaesto"],
                            Label = new Dictionary<string, string> { ["vaesto"] = "Population 31 Dec" },
                            Unit = new Dictionary<string, Unit>
                            {
                                ["vaesto"] = new() { Label = "", Decimals = 0 }
                            }
                        }
                    }
                },
                Value = new PrecisionDataArray(
                [
                    new(5471753, DataValueType.Exists), new(2691863, DataValueType.Exists), new(2779890, DataValueType.Exists),
                    new(5487308, DataValueType.Exists), new(2701490, DataValueType.Exists), new(2785818, DataValueType.Exists),
                    new(5503297, DataValueType.Exists), new(2712327, DataValueType.Exists), new(2790970, DataValueType.Exists),
                    new(5513130, DataValueType.Exists), new(2719131, DataValueType.Exists), new(2793999, DataValueType.Exists),
                    new(5517919, DataValueType.Exists), new(2723290, DataValueType.Exists), new(2794629, DataValueType.Exists),
                    new(5525292, DataValueType.Exists), new(2728262, DataValueType.Exists), new(2797030, DataValueType.Exists),
                    new(5533793, DataValueType.Exists), new(2733808, DataValueType.Exists), new(2799985, DataValueType.Exists),
                    new(5548241, DataValueType.Exists), new(2743101, DataValueType.Exists), new(2805140, DataValueType.Exists),
                    new(5563970, DataValueType.Exists), new(2753477, DataValueType.Exists), new(2810493, DataValueType.Exists),
                    new(5603851, DataValueType.Exists), new(2773898, DataValueType.Exists), new(2829953, DataValueType.Exists)
                ]),
                Size = [10, 3, 1, 1],
                Role = new Dictionary<string, List<string>>
                {
                    ["time"] = ["Vuosi"],
                    ["metric"] = ["Tiedot"]
                }
            },
            GlobalJsonConverterOptions.Default);
    }
}