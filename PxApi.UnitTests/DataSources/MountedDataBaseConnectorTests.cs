using Microsoft.Extensions.Logging.Abstractions;
using PxApi.DataSources;
using PxApi.Models;
using System.Globalization;

namespace PxApi.UnitTests.DataSources
{
    [TestFixture]
    internal sealed class MountedDataBaseConnectorTests
    {
        private readonly DataBaseRef _database = DataBaseRef.Create("mountedtestdb");
        private string _rootPath = null!;

        [SetUp]
        public void SetUp()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"PxApiMountedDataBaseConnectorTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(_rootPath, _database.Id, "nested"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }

        [Test]
        public async Task GetSourceVersionAsync_IsStableAndChangesWhenLengthChangesAtSameTimestamp()
        {
            string filePath = Path.Combine(_rootPath, _database.Id, "nested", "table.px");
            await File.WriteAllTextAsync(filePath, "initial");
            DateTime fixedTimestamp = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(filePath, fixedTimestamp);
            DateTime observedTimestamp = File.GetLastWriteTimeUtc(filePath);

            MountedDataBaseConnector connector = new(
                _database,
                _rootPath,
                NullLogger<MountedDataBaseConnector>.Instance);
            PxFileRef file = PxFileRef.ValidateAndCreate("table", _database, ["nested"]);

            string originalVersion = await connector.GetSourceVersionAsync(file);
            string repeatedVersion = await connector.GetSourceVersionAsync(file);
            long originalLength = new FileInfo(filePath).Length;
            await File.AppendAllTextAsync(filePath, "-changed");
            File.SetLastWriteTimeUtc(filePath, observedTimestamp);
            long changedLength = new FileInfo(filePath).Length;

            string changedVersion = await connector.GetSourceVersionAsync(file);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.GetLastWriteTimeUtc(filePath), Is.EqualTo(observedTimestamp));
                Assert.That(changedLength, Is.GreaterThan(originalLength));
                Assert.That(originalVersion, Is.EqualTo(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{observedTimestamp.Ticks}:{originalLength}")));
                Assert.That(repeatedVersion, Is.EqualTo(originalVersion));
                Assert.That(changedVersion, Is.EqualTo(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{observedTimestamp.Ticks}:{changedLength}")));
                Assert.That(changedVersion, Is.Not.EqualTo(originalVersion));
            }
        }
    }
}
