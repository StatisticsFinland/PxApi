using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Px.Utils.Models.Data;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Models;

namespace PxApi.IntegrationTests
{
    [TestFixture]
    [NonParallelizable]
    internal class SampleDatabaseTests
    {
        private static readonly DataBaseRef Database = DataBaseRef.Create("sample");

        private static MountedDataBaseConnector CreateConnector() => new(
            Database,
            Path.Combine(TestContext.CurrentContext.TestDirectory, "SampleDatabases"),
            NullLogger<MountedDataBaseConnector>.Instance);

        [Test]
        public async Task TestingConfiguration_WithoutBaseSettings_LoadsAndConnects()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.testing.json")
                .Build();

            AppSettings.Load(configuration);
            AppSettings settings = AppSettings.Active;

            Assert.That(settings.DataBases, Has.Count.EqualTo(1));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.DataBases[0].Id, Is.EqualTo("sample"));
                Assert.That(settings.DataBases[0].Type, Is.EqualTo(DataBaseType.Mounted));
                Assert.That(settings.DataBases[0].Custom["RootPath"], Is.EqualTo("SampleDatabases"));
                Assert.That(settings.Features.SearchController, Is.False);
                Assert.That(settings.Authentication.IsEnabled, Is.False);
                Assert.That(settings.Localization.DefaultLanguage, Is.EqualTo("en"));
            }

            MountedDataBaseConnector connector = CreateConnector();
            await connector.CheckConnectionAsync();
            PxFileRef[] files = await connector.GetAllFilesAsync();
            Assert.That(files.Select(file => file.Id), Is.EquivalentTo(new[] { "flows", "observations" }));

            foreach (string language in new[] { "fi", "sv", "en" })
            {
                using Stream stream = await connector.TryReadAuxiliaryFileAsync($"Alias_{language}.txt", null);
                using StreamReader reader = new(stream);
                Assert.That(await reader.ReadLineAsync(), Is.Not.Null.And.Not.Empty);
            }
        }

        [TestCase("flows")]
        [TestCase("observations")]
        public async Task SampleTable_ReadsEverySyntheticValue(string table)
        {
            MountedDataBaseConnector connector = CreateConnector();
            PxFileRef file = PxFileRef.ValidateAndCreate(table, Database);
            IReadOnlyMatrixMetadata metadata = await connector.ReadMetadataAsync(file);
            MatrixMap map = new([.. metadata.Dimensions.Select(dimension =>
                new DimensionMap(dimension.Code, [.. dimension.Values.Select(value => value.Code)]))]);
            DoubleDataValue[] data = await connector.ReadDataAsync(file, map, metadata);

            double[] expected = table == "flows"
                ? [.. Enumerable.Range(0, 12).SelectMany(year => new[] { 100d + year * 10, 80d + year * 10 })]
                : [.. Enumerable.Range(0, 5).SelectMany(region =>
                    Enumerable.Range(0, 13).Select(year => region == 0 ? 100d + year * 4 : region * 10d + year))];
            int[] shape = table == "flows" ? [12, 2] : [5, 13, 1];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(metadata.DefaultLanguage, Is.EqualTo("en"));
                Assert.That(metadata.Dimensions.Select(dimension => dimension.Values.Count), Is.EqualTo(shape));
                Assert.That(data.Select(value => value.Type), Is.All.EqualTo(DataValueType.Exists));
                Assert.That(data.Select(value => value.UnsafeValue), Is.EqualTo(expected));
            }
        }
    }
}
