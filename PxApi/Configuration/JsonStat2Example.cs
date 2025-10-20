using Microsoft.OpenApi.Any;
using System.Diagnostics.CodeAnalysis;

namespace PxApi.Configuration
{
    /// <summary>
    /// Provides a shared example of a JsonStat2 response for OpenAPI documentation.
    /// </summary>
    [SuppressMessage("SonarAnalyzer.CSharp", "S1192", Justification = "Duplicate string literals are intentional to represent example JSON structure.")]
    public static class JsonStat2Example
    {
        /// <summary>
        /// Gets the singleton instance of the JsonStat2 example.
        /// </summary>
        public static readonly IOpenApiAny Instance = new OpenApiObject
        {
            ["version"] = new OpenApiString("2.0"),
            ["class"] = new OpenApiString("dataset"),
            ["id"] = new OpenApiArray { new OpenApiString("vuosi"), new OpenApiString("sukupuoli"), new OpenApiString("ika"), new OpenApiString("tiedot") },
            ["label"] = new OpenApiString("Population according to age (5-year) and sex, 2014-2023"),
            ["source"] = new OpenApiString("Statistics Finland, population structure"),
            ["updated"] = new OpenApiString("2024-04-26T08:00:00Z"),
            ["dimension"] = new OpenApiObject
            {
                ["vuosi"] = new OpenApiObject
                {
                    ["label"] = new OpenApiString("Year"),
                    ["category"] = new OpenApiObject
                    {
                        ["index"] = new OpenApiArray
                        {
                            new OpenApiString("2014"), new OpenApiString("2015"), new OpenApiString("2016"),
                            new OpenApiString("2017"), new OpenApiString("2018"), new OpenApiString("2019"),
                            new OpenApiString("2020"), new OpenApiString("2021"), new OpenApiString("2022"),
                            new OpenApiString("2023")
                        },
                        ["label"] = new OpenApiObject
                        {
                            ["2014"] = new OpenApiString("2014"), ["2015"] = new OpenApiString("2015"),
                            ["2016"] = new OpenApiString("2016"), ["2017"] = new OpenApiString("2017"),
                            ["2018"] = new OpenApiString("2018"), ["2019"] = new OpenApiString("2019"),
                            ["2020"] = new OpenApiString("2020"), ["2021"] = new OpenApiString("2021"),
                            ["2022"] = new OpenApiString("2022"), ["2023"] = new OpenApiString("2023")
                        }
                    }
                },
                ["sukupuoli"] = new OpenApiObject
                {
                    ["label"] = new OpenApiString("Sex"),
                    ["category"] = new OpenApiObject
                    {
                        ["index"] = new OpenApiArray { new OpenApiString("SSS"), new OpenApiString("1"), new OpenApiString("2") },
                        ["label"] = new OpenApiObject
                        {
                            ["SSS"] = new OpenApiString("Total"),
                            ["1"] = new OpenApiString("Males"),
                            ["2"] = new OpenApiString("Females")
                        }
                    }
                },
                ["ika"] = new OpenApiObject
                {
                    ["label"] = new OpenApiString("Age"),
                    ["category"] = new OpenApiObject
                    {
                        ["index"] = new OpenApiArray { new OpenApiString("SSS") },
                        ["label"] = new OpenApiObject { ["SSS"] = new OpenApiString("Total") }
                    }
                },
                ["tiedot"] = new OpenApiObject
                {
                    ["label"] = new OpenApiString("Information"),
                    ["category"] = new OpenApiObject
                    {
                        ["index"] = new OpenApiArray { new OpenApiString("vaesto") },
                        ["label"] = new OpenApiObject { ["vaesto"] = new OpenApiString("Population 31 Dec") },
                        ["unit"] = new OpenApiObject
                        {
                            ["vaesto"] = new OpenApiObject
                            {
                                ["label"] = new OpenApiString(""),
                                ["decimals"] = new OpenApiInteger(0)
                            }
                        }
                    }
                }
            },
            ["value"] = new OpenApiArray
            {
                new OpenApiInteger(5471753), new OpenApiInteger(2691863), new OpenApiInteger(2779890),
                new OpenApiInteger(5487308), new OpenApiInteger(2701490), new OpenApiInteger(2785818),
                new OpenApiInteger(5503297), new OpenApiInteger(2712327), new OpenApiInteger(2790970),
                new OpenApiInteger(5513130), new OpenApiInteger(2719131), new OpenApiInteger(2793999),
                new OpenApiInteger(5517919), new OpenApiInteger(2723290), new OpenApiInteger(2794629),
                new OpenApiInteger(5525292), new OpenApiInteger(2728262), new OpenApiInteger(2797030),
                new OpenApiInteger(5533793), new OpenApiInteger(2733808), new OpenApiInteger(2799985),
                new OpenApiInteger(5548241), new OpenApiInteger(2743101), new OpenApiInteger(2805140),
                new OpenApiInteger(5563970), new OpenApiInteger(2753477), new OpenApiInteger(2810493),
                new OpenApiInteger(5603851), new OpenApiInteger(2773898), new OpenApiInteger(2829953)
            },
            ["size"] = new OpenApiArray { new OpenApiInteger(10), new OpenApiInteger(3), new OpenApiInteger(1), new OpenApiInteger(1) },
            ["role"] = new OpenApiObject
            {
                ["time"] = new OpenApiArray { new OpenApiString("Vuosi") },
                ["metric"] = new OpenApiArray { new OpenApiString("Tiedot") }
            }
        };
    }
}
