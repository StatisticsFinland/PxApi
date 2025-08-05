using System.Text.Json;
using System.Text.Json.Serialization;

namespace PxApi.Models.QueryFilters
{
    /// <summary>
    /// Provides custom JSON serialization and deserialization for objects implementing the <see cref="IFilter"/>
    /// interface.
    /// </summary>
    public class FilterJsonConverter : JsonConverter<IFilter>
    {
        /// <summary>
        /// Specifies the type of filter to use when serializing and deserializing.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum FilterType
        {
            /// <summary>
            /// Code filter that matches input strings against a list of filter strings.
            /// </summary>
            Code,
            /// <summary>
            /// All filter that matches all input strings.
            /// </summary>
            All,
            /// <summary>
            /// From filter that matches input strings starting from the first string that matches the filter string.
            /// </summary>
            From,
            /// <summary>
            /// To filter that matches input strings up to and including the first string that matches the filter string.
            /// </summary>
            To,
            /// <summary>
            /// First filter that returns the first N elements from the input collection.
            /// </summary>
            First,
            /// <summary>
            /// Last filter that returns the last N elements from the input collection.
            /// </summary>
            Last
        }

        /// <summary>
        /// JSON model used for serializing and deserializing filter objects.
        /// </summary>
        public class FilterJsonModel
        {
            /// <summary>
            /// Enumeration type of the filter.
            /// <see cref="FilterType"/>
            /// </summary>
            [JsonPropertyName("type")]
            public FilterType Type { get; set; }
            /// <summary>
            /// Value of the filter query. Can be a string, list of strings, or an integer depending on the filter type.
            /// </summary>
            [JsonPropertyName("query")]
            public JsonElement Query { get; set; }
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IFilter value, JsonSerializerOptions options)
        {
            if (value is CodeFilter codeFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.Code, query = codeFilter.FilterStrings });
                return;
            }

            if (value is AllFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.All });
                return;
            }

            if (value is FromFilter fromFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.From, query = fromFilter.FilterString });
                return;
            }
            
            if (value is ToFilter toFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.To, query = toFilter.FilterString });
                return;
            }

            if (value is FirstFilter firstFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.First, query = firstFilter.Count });
            }

            if (value is LastFilter lastFilter)
            {
                JsonSerializer.Serialize(writer, new { type = FilterType.Last, query = lastFilter.Count });
            }
        }

        /// <inheritdoc/>
        public override IFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            FilterJsonModel? filterWrapper = JsonSerializer.Deserialize<FilterJsonModel>(ref reader, options);

            switch (filterWrapper!.Type)
            {
                case FilterType.Code:
                    {
                        List<string>? codesList = filterWrapper.Query.Deserialize<List<string>>(options);
                        codesList!.ForEach(ValidateInputString);
                        return new CodeFilter(codesList);
                    }
                case FilterType.All:
                    {
                        return new AllFilter();
                    }
                case FilterType.From:
                    {
                        string? fromCode = filterWrapper.Query.Deserialize<string>(options);
                        ValidateInputString(fromCode!);
                        return new FromFilter(fromCode!);
                    }
                case FilterType.To:
                    {
                        string? toCode = filterWrapper.Query.Deserialize<string>(options);
                        ValidateInputString(toCode!);
                        return new FromFilter(toCode!);
                    }
                case FilterType.First:
                    {
                        int firstCount = filterWrapper.Query.Deserialize<int>(options);
                        return new FirstFilter(firstCount);
                    }
                case FilterType.Last:
                    {
                        int lastCount = filterWrapper.Query.Deserialize<int>(options);
                        return new LastFilter(lastCount);
                    }
                default: throw new InvalidOperationException("Unknown filter type: " + filterWrapper.Type);
            }
        }

        private static void ValidateInputString(string input)
        {
            if (input.Length > 50)
            {
                throw new ArgumentException("Filter string exceeds maximum length of 50 characters");
            }

            foreach (char c in input)
            {
                if (!char.IsLetterOrDigit(c) || c == '*')
                {
                    throw new ArgumentException("Input string contains characters other than letters and numbers");
                }
            }
        }
    }
}
