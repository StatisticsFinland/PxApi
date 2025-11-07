using Px.Utils.Language;
using PxApi.Models;

namespace PxApi.UnitTests.Utils
{
    internal static class TableGroupTestUtils
    {
        internal static TableGroup CreateTestTableGroup(string code = "group-code-1", string groupingCode = "grouping-code")
        {
            MultilanguageString name = new(new Dictionary<string, string>
            {
                { "fi", code + "-nimi.fi" },
                { "sv", code + "-namn.sv" },
                { "en", code + "-name.en" }
            });

            MultilanguageString groupingName = new(new Dictionary<string, string>
            {
                { "fi", groupingCode + "-nimi.fi" },
                { "sv", groupingCode + "-namn.sv" },
                { "en", groupingCode + "-name.en" }
            });

            TableGroup group = new()
            {
                Code = code,
                Name = name,
                GroupingCode = groupingCode,
                GroupingName = groupingName,
                Links = []
            };
            return group;
        }

        internal static List<TableGroup> CreateTestTableGroups(int count)
        {
            List<TableGroup> groups = [];
            for (int i = 1; i <= count; i++)
            {
                string code = "group-code-" + i.ToString();
                string grouping = "grouping-code-" + i.ToString();
                groups.Add(CreateTestTableGroup(code, grouping));
            }
            return groups;
        }
    }
}
