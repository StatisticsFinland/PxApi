using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Px.Utils.Models.Data;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Models.JsonStat;
using PxApi.Models.QueryFilters;
using PxApi.Models;
using PxApi.UnitTests.Utils;
using System.Text;

namespace PxApi.UnitTests.ControllerTests
{
    [TestFixture]
    internal class DataControllerStreamTests
    {
        private static readonly string PX_FILE_FIXTURE =
            """
            CHARSET="ANSI";
            AXIS-VERSION="2013";
            CODEPAGE="utf-8";
            LANGUAGE="fi";
            LANGUAGES="fi","sv","en";
            CREATION-DATE="20220616 09:00";
            NEXT-UPDATE="20250128 08:00";
            TABLEID="statfin_ashi_pxt_13pz";
            DECIMALS=1;
            SHOWDECIMALS=0;
            MATRIX="001_13pz_2024q2";
            SUBJECT-CODE="ASHI";
            SUBJECT-AREA="ashi";
            SUBJECT-AREA[sv]="ashi";
            SUBJECT-AREA[en]="ashi";
            COPYRIGHT=YES;
            DESCRIPTION="test_description_fi";
            DESCRIPTION[sv]="test_description_sv";
            DESCRIPTION[en]="test_description_en";
            TITLE="test_title_fi";
            TITLE[sv]="test_title_sv";
            TITLE[en]="test_title_en";
            CONTENTS="test_contents_fi";
            CONTENTS[sv]="test_contents_sv";
            CONTENTS[en]="test_contents_en";
            UNITS="";
            UNITS[sv]="";
            UNITS[en]="";
            STUB="Vuosineljännes","Alue";
            STUB[sv]="Kvartal","Område";
            STUB[en]="Quarter","Region";
            HEADING="Tiedot";
            HEADING[sv]="Uppgifter";
            HEADING[en]="Information";
            CONTVARIABLE="Tiedot";
            CONTVARIABLE[sv]="Uppgifter";
            CONTVARIABLE[en]="Information";
            VALUES("Vuosineljännes")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1*","2024Q2*";
            VALUES[sv]("Kvartal")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1*","2024Q2*";
            VALUES[en]("Quarter")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1*","2024Q2*";
            VALUES("Alue")="Koko maa","Pääkaupunkiseutu (PKS)","Koko maa ilman pääkaupunkiseutua",
            "Suuret kaupungit (yhteensä)","Koko maa ilman suuria kaupunkeja";
            VALUES[sv]("Område")="Hela landet","Huvudstadsregionen","Hela landet utan Huvudstadsregionen",
            "Stora städerna (totalt)","Hela landet utan stora städerna";
            VALUES[en]("Region")="Whole country","Greater Helsinki","Whole country excluding Greater Helsinki",
            "Major cities (total)","Whole country excluding major cities";
            VALUES("Tiedot")="Uusin julkistus, neljännesmuutos, %","Ensimmäinen julkistus, neljännesmuutos, %",
            "Tarkentuminen, neljännesmuutos, %-yksikköä","Uusin julkistus, vuosimuutos, %",
            "Ensimmäinen julkistus, vuosimuutos, %","Tarkentuminen, vuosimuutos, %-yksikköä",
            "Uusin julkistus, kauppojen lukumäärä","Ensimmäinen julkistus, kauppojen lukumäärä",
            "Tarkentuminen, kauppojen lukumäärä";
            VALUES[sv]("Uppgifter")="Senaste publikation, kvartalsförändring, %",
            "Första publikation, kvartalsförändring, %","Revidering, kvartalsförändring, procentenhet",
            "Senaste publikation, årsförändring, %","Första publikation, årsförändring, %",
            "Revidering, årsförändring, procentenhet","Senaste publikation, antal köp",
            "Första publikation, antal köp","Revidering, antal köp";
            VALUES[en]("Information")="Latest release, quarterly change, %",
            "First release, quarterly change, %","Revision, quarterly change, percentage point",
            "Latest release, yearly change, %","First release, yearly change, %",
            "Revision, yearly change, percentage point","Latest release, number of sales",
            "First release, number of sales","Revision, number of sales";
            TIMEVAL("Vuosineljännes")=TLIST(Q1),"20221","20222","20223","20224","20231","20232","20233","20234",
            "20241","20242";
            TIMEVAL[sv]("Kvartal")=TLIST(Q1),"20221","20222","20223","20224","20231","20232","20233","20234",
            "20241","20242";
            TIMEVAL[en]("Quarter")=TLIST(Q1),"20221","20222","20223","20224","20231","20232","20233","20234",
            "20241","20242";
            CODES("Vuosineljännes")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1","2024Q2";
            CODES[sv]("Kvartal")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1","2024Q2";
            CODES[en]("Quarter")="2022Q1","2022Q2","2022Q3","2022Q4","2023Q1","2023Q2","2023Q3","2023Q4",
            "2024Q1","2024Q2";
            CODES("Alue")="ksu","pks","msu","kas","muu";
            CODES[sv]("Område")="ksu","pks","msu","kas","muu";
            CODES[en]("Region")="ksu","pks","msu","kas","muu";
            CODES("Tiedot")="neljmuut","neljmuut_eka","neljmuut_rev","vmuut","vmuut_eka","vmuut_rev",
            "lkm_julk_viim","lkm_julk_eka","lkm_julk_rev";
            CODES[sv]("Uppgifter")="neljmuut","neljmuut_eka","neljmuut_rev","vmuut","vmuut_eka","vmuut_rev",
            "lkm_julk_viim","lkm_julk_eka","lkm_julk_rev";
            CODES[en]("Information")="neljmuut","neljmuut_eka","neljmuut_rev","vmuut","vmuut_eka","vmuut_rev",
            "lkm_julk_viim","lkm_julk_eka","lkm_julk_rev";
            VARIABLE-TYPE("Vuosineljännes")="Time";
            VARIABLE-TYPE[sv]("Kvartal")="Time";
            VARIABLE-TYPE[en]("Quarter")="Time";
            VARIABLE-TYPE("Alue")="Classificatory";
            VARIABLE-TYPE[sv]("Område")="Classificatory";
            VARIABLE-TYPE[en]("Region")="Classificatory";
            MAP("Alue")="Alue 2022";
            MAP[sv]("Område")="Alue 2022";
            MAP[en]("Region")="Alue 2022";
            PRECISION("Tiedot","Uusin julkistus, neljännesmuutos, %")=1;
            PRECISION[sv]("Uppgifter","Senaste publikation, kvartalsförändring, %")=1;
            PRECISION[en]("Information","Latest release, quarterly change, %")=1;
            PRECISION("Tiedot","Ensimmäinen julkistus, neljännesmuutos, %")=1;
            PRECISION[sv]("Uppgifter","Första publikation, kvartalsförändring, %")=1;
            PRECISION[en]("Information","First release, quarterly change, %")=1;
            PRECISION("Tiedot","Tarkentuminen, neljännesmuutos, %-yksikköä")=1;
            PRECISION[sv]("Uppgifter","Revidering, kvartalsförändring, procentenhet")=1;
            PRECISION[en]("Information","Revision, quarterly change, percentage point")=1;
            PRECISION("Tiedot","Uusin julkistus, vuosimuutos, %")=1;
            PRECISION[sv]("Uppgifter","Senaste publikation, årsförändring, %")=1;
            PRECISION[en]("Information","Latest release, yearly change, %")=1;
            PRECISION("Tiedot","Ensimmäinen julkistus, vuosimuutos, %")=1;
            PRECISION[sv]("Uppgifter","Första publikation, årsförändring, %")=1;
            PRECISION[en]("Information","First release, yearly change, %")=1;
            PRECISION("Tiedot","Tarkentuminen, vuosimuutos, %-yksikköä")=1;
            PRECISION[sv]("Uppgifter","Revidering, årsförändring, procentenhet")=1;
            PRECISION[en]("Information","Revision, yearly change, percentage point")=1;
            LAST-UPDATED("Uusin julkistus, neljännesmuutos, %")="20241029 08:00";
            LAST-UPDATED[sv]("Senaste publikation, kvartalsförändring, %")="20241029 08:00";
            LAST-UPDATED[en]("Latest release, quarterly change, %")="20241029 08:00";
            LAST-UPDATED("Ensimmäinen julkistus, neljännesmuutos, %")="20241029 08:00";
            LAST-UPDATED[sv]("Första publikation, kvartalsförändring, %")="20241029 08:00";
            LAST-UPDATED[en]("First release, quarterly change, %")="20241029 08:00";
            LAST-UPDATED("Tarkentuminen, neljännesmuutos, %-yksikköä")="20241029 08:00";
            LAST-UPDATED[sv]("Revidering, kvartalsförändring, procentenhet")="20241029 08:00";
            LAST-UPDATED[en]("Revision, quarterly change, percentage point")="20241029 08:00";
            LAST-UPDATED("Uusin julkistus, vuosimuutos, %")="20241029 08:00";
            LAST-UPDATED[sv]("Senaste publikation, årsförändring, %")="20241029 08:00";
            LAST-UPDATED[en]("Latest release, yearly change, %")="20241029 08:00";
            LAST-UPDATED("Ensimmäinen julkistus, vuosimuutos, %")="20241029 08:00";
            LAST-UPDATED[sv]("Första publikation, årsförändring, %")="20241029 08:00";
            LAST-UPDATED[en]("First release, yearly change, %")="20241029 08:00";
            LAST-UPDATED("Tarkentuminen, vuosimuutos, %-yksikköä")="20241029 08:00";
            LAST-UPDATED[sv]("Revidering, årsförändring, procentenhet")="20241029 08:00";
            LAST-UPDATED[en]("Revision, yearly change, percentage point")="20241029 08:00";
            LAST-UPDATED("Uusin julkistus, kauppojen lukumäärä")="20241029 08:00";
            LAST-UPDATED[sv]("Senaste publikation, antal köp")="20241029 08:00";
            LAST-UPDATED[en]("Latest release, number of sales")="20241029 08:00";
            LAST-UPDATED("Ensimmäinen julkistus, kauppojen lukumäärä")="20241029 08:00";
            LAST-UPDATED[sv]("Första publikation, antal köp")="20241029 08:00";
            LAST-UPDATED[en]("First release, number of sales")="20241029 08:00";
            LAST-UPDATED("Tarkentuminen, kauppojen lukumäärä")="20241029 08:00";
            LAST-UPDATED[sv]("Revidering, antal köp")="20241029 08:00";
            LAST-UPDATED[en]("Revision, number of sales")="20241029 08:00";
            UNITS("Uusin julkistus, neljännesmuutos, %")="Prosentti";
            UNITS[sv]("Senaste publikation, kvartalsförändring, %")="Procent";
            UNITS[en]("Latest release, quarterly change, %")="Per cent";
            UNITS("Ensimmäinen julkistus, neljännesmuutos, %")="Prosentti";
            UNITS[sv]("Första publikation, kvartalsförändring, %")="Procent";
            UNITS[en]("First release, quarterly change, %")="Per cent";
            UNITS("Tarkentuminen, neljännesmuutos, %-yksikköä")="Prosenttiyksikkö";
            UNITS[sv]("Revidering, kvartalsförändring, procentenhet")="Procentenhet";
            UNITS[en]("Revision, quarterly change, percentage point")="Percentage point";
            UNITS("Uusin julkistus, vuosimuutos, %")="Prosentti";
            UNITS[sv]("Senaste publikation, årsförändring, %")="Procent";
            UNITS[en]("Latest release, yearly change, %")="Per cent";
            UNITS("Ensimmäinen julkistus, vuosimuutos, %")="Prosentti";
            UNITS[sv]("Första publikation, årsförändring, %")="Procent";
            UNITS[en]("First release, yearly change, %")="Per cent";
            UNITS("Tarkentuminen, vuosimuutos, %-yksikköä")="Prosenttiyksikkö";
            UNITS[sv]("Revidering, årsförändring, procentenhet")="Procentenhet";
            UNITS[en]("Revision, yearly change, percentage point")="Percentage point";
            UNITS("Uusin julkistus, kauppojen lukumäärä")="Lukumäärä";
            UNITS[sv]("Senaste publikation, antal köp")="Antal";
            UNITS[en]("Latest release, number of sales")="Number";
            UNITS("Ensimmäinen julkistus, kauppojen lukumäärä")="Lukumäärä";
            UNITS[sv]("Första publikation, antal köp")="Antal";
            UNITS[en]("First release, number of sales")="Number";
            UNITS("Tarkentuminen, kauppojen lukumäärä")="Lukumäärä";
            UNITS[sv]("Revidering, antal köp")="Antal";
            UNITS[en]("Revision, number of sales")="Number";
            SOURCE="Tilastokeskus, osakeasuntojen hinnat";
            SOURCE[sv]="Statistikcentralen, aktiebostadspriser";
            SOURCE[en]="Statistics Finland, prices of dwellings in housing companies";
            META-ID("Vuosineljännes")="SCALE-TYPE=None";
            META-ID[sv]("Kvartal")="SCALE-TYPE=None";
            META-ID[en]("Quarter")="SCALE-TYPE=None";
            META-ID("Alue")="SCALE-TYPE=nominal";
            META-ID[sv]("Område")="SCALE-TYPE=nominal";
            META-ID[en]("Region")="SCALE-TYPE=nominal";
            DATA=
            0.3 0.7 -0.4 3 3.4 -0.4 19831 12773 7058 
            -0.1 0.2 -0.3 2.8 3.1 -0.3 5708 4102 1606 
            0.8 1.3 -0.5 3.2 3.7 -0.5 14096 8658 5438 
            0.2 0.5 -0.3 3.5 3.8 -0.3 9378 6667 2711 
            0.6 1.2 -0.6 2.1 2.6 -0.5 10426 6093 4333 
            1.3 1.1 0.2 1.9 1.7 0.2 19993 11946 8047 
            1.2 0.4 0.8 1.8 1.2 0.6 5838 4038 1800 
            1.5 1.8 -0.3 2 2.3 -0.3 14105 7896 6209 
            1.2 0.4 0.8 2 1.4 0.6 9649 6299 3350 
            1.7 2.4 -0.7 1.6 2.3 -0.7 10294 5635 4659 
            -1.7 -1.7 0 0.3 0.3 0 17849 10704 7145 
            -2.2 -2.9 0.7 -0.2 -0.8 0.6 4902 3227 1675 
            -1.1 -0.4 -0.7 0.8 1.5 -0.7 12916 7463 5453 
            -1.9 -2.1 0.2 0.4 0.2 0.2 7828 5157 2671 
            -1.2 -0.9 -0.3 0.1 0.6 -0.5 9990 5533 4457 
            -3 -3.1 0.1 -3 -3 0 12957 9169 3788 
            -2.9 -3.2 0.3 -4.1 -4.3 0.2 3824 2925 899 
            -3.1 -3 -0.1 -2 -1.6 -0.4 9032 6147 2885 
            -3.1 -3.2 0.1 -3.6 -3.6 0 6019 4509 1510 
            -2.9 -2.9 0 -1.9 -1.7 -0.2 6837 4563 2274 
            -2.2 -2.3 0.1 -5.5 -5.5 0 13316 7765 5551 
            -2.7 -2.3 -0.4 -6.6 -6.2 -0.4 3622 2423 1199 
            -1.7 -2.1 0.4 -4.4 -4.8 0.4 9668 5337 4331 
            -2.3 -2.1 -0.2 -6 -5.8 -0.2 5791 3770 2021 
            -2 -2.4 0.4 -4.4 -4.8 0.4 7499 3990 3509 
            -0.4 -0.2 -0.2 -7.1 -7 -0.1 13920 7962 5958 
            -1 -1.2 0.2 -8.6 -8.5 -0.1 3961 2509 1452 
            0.2 0.9 -0.7 -5.6 -5.4 -0.2 9925 5436 4489 
            -0.9 -0.9 0 -7.9 -7.7 -0.2 6429 4105 2324 
            0.5 1.2 -0.7 -5.6 -5.5 -0.1 7457 3840 3617 
            -2 -2 0 -7.4 -7.3 -0.1 13352 7921 5431 
            -2.1 -1.9 -0.2 -8.5 -8.4 -0.1 3267 2306 961 
            -2 -2.1 0.1 -6.4 -6.2 -0.2 10040 5604 4436 
            -1.7 -1.6 -0.1 -7.7 -7.6 -0.1 5532 3806 1726 
            -2.6 -2.8 0.2 -6.9 -6.7 -0.2 7775 4104 3671 
            -1.3 -0.5 -0.8 -5.8 -5.2 -0.6 14898 10363 4535 
            -1.9 -1 -0.9 -7.5 -7.1 -0.4 4367 3105 1262 
            -0.7 0.1 -0.8 -4.1 -3.3 -0.8 10504 7241 3263 
            -2 -1.6 -0.4 -6.7 -6.6 -0.1 6744 4841 1903 
            0.1 1.6 -1.5 -4 -2.5 -1.5 8127 5505 2622 
            -1.5 -1.5 0 -5.1 -5.1 0 12268 9379 2889 
            -1.3 -1.5 0.2 -6.1 -6.3 0.2 3190 2332 858 
            -1.8 -1.5 -0.3 -4.2 -3.9 -0.3 9021 7034 1987 
            -1.3 -1.5 0.2 -5.7 -5.9 0.2 5194 3940 1254 
            -2 -1.5 -0.5 -4 -3.6 -0.4 7017 5426 1591 
            0.8 1 -0.2 -4 -3.7 -0.3 13774 11878 1896 
            0 0.1 -0.1 -5.1 -5.1 0 3786 3121 665 
            1.5 2 -0.5 -2.9 -2.4 -0.5 9975 8746 1229 
            0.2 0.3 -0.1 -4.7 -4.6 -0.1 6083 5177 906 
            1.9 2.5 -0.6 -2.6 -2 -0.6 7678 6690 988;
            """;


        private Mock<IDataBaseConnectorFactory> _mockConnectorFactory = null!;
        private Mock<IDataBaseConnector> _mockConnector = null!;
        private Mock<ILogger<DataController>> _mockLogger = null!;
        private CachedDataSource _cachedDataSource = null!;
        private DataController _controller = null!;
        private DataBaseRef _testDatabase;
        private PxFileRef _testTable;
        private static readonly string[] expected = ["vuosineljannes", "alue", "tiedot"];

        [SetUp]
        public void SetUp()
        {
            _mockConnectorFactory = new Mock<IDataBaseConnectorFactory>();
            _mockConnector = new Mock<IDataBaseConnector>();
            _mockLogger = new Mock<ILogger<DataController>>();

            SetupConfiguration();
            SetupTestData();
            SetupMocks();

            IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
            DatabaseCache databaseCache = new(memoryCache);
            _cachedDataSource = new CachedDataSource(_mockConnectorFactory.Object, databaseCache);
            
            _controller = new DataController(_cachedDataSource, _mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private static void SetupConfiguration()
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "testdb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["DataBases:0:CacheConfig:Modifiedtime:SlidingExpirationSeconds"] = "60",
                    ["DataBases:0:CacheConfig:Modifiedtime:AbsoluteExpirationSeconds"] = "60",
                    ["DataBases:0:Custom:ModifiedCheckIntervalMs"] = "1000",
                    ["DataBases:0:Custom:FileListingCacheDurationMs"] = "10000"
                }
            );
            IConfiguration configuration = TestConfigFactory.BuildAndLoad(configData);

            AppSettings.Load(configuration);
        }

        private void SetupTestData()
        {
            _testDatabase = DataBaseRef.Create("testdb");
            _testTable = PxFileRef.CreateFromPath(Path.Combine("c:", "foo", "testtable"), _testDatabase);
        }

        private static MemoryStream CreateTestDataStream()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(PX_FILE_FIXTURE));
        }

        private void SetupMocks()
        {
            _mockConnector.Setup(c => c.DataBase).Returns(_testDatabase);
            _mockConnector.Setup(c => c.ReadPxFile(_testTable))
                .Returns(CreateTestDataStream);
            _mockConnector.Setup(c => c.GetLastWriteTimeAsync(_testTable))
                .ReturnsAsync(DateTime.UtcNow.AddMinutes(-10));
            _mockConnector.Setup(c => c.GetAllFilesAsync())
                .ReturnsAsync([_testTable.FilePath]);
            _mockConnector.Setup(c => c.TryReadAuxiliaryFileAsync(It.IsAny<string>()))
                .ThrowsAsync(new FileNotFoundException());
            _mockConnectorFactory.Setup(f => f.GetAvailableDatabases())
                .Returns([_testDatabase]);
            _mockConnectorFactory.Setup(f => f.GetConnector(_testDatabase))
                .Returns(_mockConnector.Object);
        }

        [Test]
        public async Task GetDataAsync_NoCacheData_ReturnsCorrectDataFromStream()
        {
            // Arrange  
            string database = "testdb";
            string table = "testtable";
            string[] filters = [
                "Tiedot:code=neljmuut,neljmuut_eka",
                "Vuosineljannes:code=2022Q1,2022Q2",
                "Alue:code=ksu,pks,msu,kas,muu"
            ];

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data values for 2 metrics × 2 time periods × 5 regions = 20 data points
            double[] expectedValues = [
                0.3, 0.7, -0.1, 0.2, 0.8, 1.3, 0.2, 0.5, 0.6, 1.2, // 2022Q1: neljmuut, neljmuut_eka for all regions
                1.3, 1.1, 1.2, 0.4, 1.5, 1.8, 1.2, 0.4, 1.7, 2.4   // 2022Q2: neljmuut, neljmuut_eka for all regions
            ];

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result!;
                Assert.That(okResult.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 dataResponse = (JsonStat2)okResult.Value!;
                Assert.That(dataResponse.Value, Is.Not.Null);
                Assert.That(dataResponse.Dimension, Is.Not.Null);
                
                // Verify correct data dimensions: 2 Tiedot × 2 Vuosineljännes × 5 Alue = 20 data points
                Assert.That(dataResponse.Value, Has.Length.EqualTo(20));
                
                // Verify all values have correct DataValueType
                Assert.That(dataResponse.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                
                // Compare actual data values against expectedFi array
                double[] actualValues = [.. dataResponse.Value.Select(d => d.UnsafeValue)];
                Assert.That(actualValues, Is.EqualTo(expectedValues));
                
                // Check Dimensions structure
                Assert.That(dataResponse.Dimension, Has.Count.EqualTo(3));
                Assert.That(dataResponse.Dimension.Any(dm => dm.Key == "tiedot"));
                Assert.That(dataResponse.Dimension.Any(dm => dm.Key == "vuosineljannes"));
                Assert.That(dataResponse.Dimension.Any(dm => dm.Key == "alue"));
            });

            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task GetDataAsync_SecondCallUsesCache_ReturnsFromCache()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = [
                "Tiedot:code=neljmuut",
                "Vuosineljannes:code=2022Q1,2022Q2",
                "Alue:code=ksu,pks,msu,kas,muu"
            ];

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data values for 1 metric × 2 time periods × 5 regions = 10 data points
            double[] expectedValues = [
                0.3, -0.1, 0.8, 0.2, 0.6, // 2022Q1: neljmuut for all regions
                1.3, 1.2, 1.5, 1.2, 1.7   // 2022Q2: neljmuut for all regions
            ];

            // Act - First call should read from stream
            IActionResult result1 = await _controller.GetDataAsync(database, table, filters);
            
            // Act - Second call should use cache
            IActionResult result2 = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result1, Is.InstanceOf<OkObjectResult>());
                Assert.That(result2, Is.InstanceOf<OkObjectResult>());
                
                OkObjectResult okResult1 = (OkObjectResult)result1!;
                OkObjectResult okResult2 = (OkObjectResult)result2!;
                
                Assert.That(okResult1.Value, Is.InstanceOf<JsonStat2>());
                Assert.That(okResult2.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 dataResponse1 = (JsonStat2)okResult1.Value!;
                JsonStat2 dataResponse2 = (JsonStat2)okResult2.Value!;
                
                Assert.That(dataResponse1.Value, Is.Not.Null);
                Assert.That(dataResponse2.Value, Is.Not.Null);
                Assert.That(dataResponse1.Value, Has.Length.EqualTo(dataResponse2.Value.Length));
                
                // Single metric × 2 time periods × 5 regions = 10 data points
                Assert.That(dataResponse1.Value, Has.Length.EqualTo(10));
                Assert.That(dataResponse2.Value, Has.Length.EqualTo(10));
                
                // Verify all values have correct DataValueType
                Assert.That(dataResponse1.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                Assert.That(dataResponse2.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                
                // Compare actual data values against expectedFi array for both calls
                double[] actualValues1 = [.. dataResponse1.Value.Select(d => d.UnsafeValue)];
                double[] actualValues2 = [.. dataResponse2.Value.Select(d => d.UnsafeValue)];
                Assert.That(actualValues1, Is.EqualTo(expectedValues));
                Assert.That(actualValues2, Is.EqualTo(expectedValues));
                Assert.That(actualValues1, Is.EqualTo(actualValues2));
            });

            // Should have read from stream at least once, but cache should reduce subsequent reads
            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task GetDataAsync_SupersetInCache_ReturnsSubsetFromCache()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            
            // First request for superset
            string[] supersetFilters = [
                "Tiedot:code=neljmuut,neljmuut_eka",
                "Vuosineljannes:code=2022Q1,2022Q2",
                "Alue:code=ksu,pks,msu,kas,muu"
            ];
            
            // Second request for subset
            string[] subsetFilters = [
                "Tiedot:code=neljmuut",
                "Vuosineljannes:code=2022Q2",
                "Alue:code=ksu,pks,msu,kas,muu"
            ];

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data for superset: 2 metrics × 2 time periods × 5 regions = 20 data points
            double[] expectedSupersetValues = [
                0.3, 0.7, -0.1, 0.2, 0.8, 1.3, 0.2, 0.5, 0.6, 1.2, // 2022Q1: neljmuut, neljmuut_eka for all regions
                1.3, 1.1, 1.2, 0.4, 1.5, 1.8, 1.2, 0.4, 1.7, 2.4   // 2022Q2: neljmuut, neljmuut_eka for all regions
            ];

            // Expected data for subset: 1 metric × 1 time period × 5 regions = 5 data points
            double[] expectedSubsetValues = [1.3, 1.2, 1.5, 1.2, 1.7]; // 2022Q2 neljmuut for all regions

            // Act - First call loads superset data
            IActionResult supersetResult = await _controller.GetDataAsync(database, table, supersetFilters);
            
            // Act - Second call should get subset from cached superset
            IActionResult subsetResult = await _controller.GetDataAsync(database, table, subsetFilters);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(supersetResult, Is.InstanceOf<OkObjectResult>());
                Assert.That(subsetResult, Is.InstanceOf<OkObjectResult>());
                
                OkObjectResult okSupersetResult = (OkObjectResult)supersetResult!;
                OkObjectResult okSubsetResult = (OkObjectResult)subsetResult!;
                
                Assert.That(okSupersetResult.Value, Is.InstanceOf<JsonStat2>());
                Assert.That(okSubsetResult.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 supersetDataResponse = (JsonStat2)okSupersetResult.Value!;
                JsonStat2 subsetDataResponse = (JsonStat2)okSubsetResult.Value!;
                
                Assert.That(supersetDataResponse.Value, Is.Not.Null);
                Assert.That(subsetDataResponse.Value, Is.Not.Null);
                
                // Superset: 2 metrics × 2 time periods × 5 regions = 20 data points
                Assert.That(supersetDataResponse.Value, Has.Length.EqualTo(20));
                
                // Subset: 1 metric × 1 time period × 5 regions = 5 data points
                Assert.That(subsetDataResponse.Value, Has.Length.EqualTo(5));
                Assert.That(subsetDataResponse.Value, Has.Length.LessThan(supersetDataResponse.Value.Length));
                
                // Verify all data points exist
                Assert.That(supersetDataResponse.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                Assert.That(subsetDataResponse.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                
                // Compare actual superset data against expectedFi array
                double[] actualSupersetValues = [.. supersetDataResponse.Value.Select(d => d.UnsafeValue)];
                Assert.That(actualSupersetValues, Is.EqualTo(expectedSupersetValues));
                
                // Compare actual subset data against expectedFi array
                double[] actualSubsetValues = [.. subsetDataResponse.Value.Select(d => d.UnsafeValue)];
                Assert.That(actualSubsetValues, Is.EqualTo(expectedSubsetValues));
            });

            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task PostDataAsync_NoCacheData_ReturnsCorrectDataFromStream()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new()
            {
                { "Tiedot", new CodeFilter(["neljmuut", "neljmuut_eka"]) },
                { "Alue", new CodeFilter(["ksu"]) }
            };

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data: 2 metrics × 10 time periods × 1 region = 20 data points
            // For ksu region across all time periods, alternating between neljmuut and neljmuut_eka
            double[] expectedValues = [
                0.3, 0.7,    // 2022Q1: neljmuut, neljmuut_eka
                1.3, 1.1,    // 2022Q2: neljmuut, neljmuut_eka
                -1.7, -1.7,  // 2022Q3: neljmuut, neljmuut_eka
                -3, -3.1,    // 2022Q4: neljmuut, neljmuut_eka
                -2.2, -2.3,  // 2023Q1: neljmuut, neljmuut_eka
                -0.4, -0.2,  // 2023Q2: neljmuut, neljmuut_eka
                -2, -2,      // 2023Q3: neljmuut, neljmuut_eka
                -1.3, -0.5,  // 2023Q4: neljmuut, neljmuut_eka
                -1.5, -1.5,  // 2024Q1: neljmuut, neljmuut_eka
                0.8, 1,      // 2024Q2: neljmuut, neljmuut_eka
            ];

            // Act
            IActionResult result = await _controller.PostDataAsync(database, table, query);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result!;
                Assert.That(okResult.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 dataResponse = (JsonStat2)okResult.Value!;
                Assert.That(dataResponse.Value, Is.Not.Null);
                Assert.That(dataResponse.Dimension, Is.Not.Null);
                
                // Should return 2 metrics × 10 time periods × 1 region = 20 data points
                Assert.That(dataResponse.Value, Has.Length.EqualTo(20));

                // Verify all values have correct DataValueType
                Assert.That(dataResponse.Value.All(d => d.Type == DataValueType.Exists), Is.True);
                
                // Compare actual data against expectedFi array
                double[] actualValues = [.. dataResponse.Value.Select(d => d.UnsafeValue)];
                Assert.That(actualValues, Is.EqualTo(expectedValues));
                
                // Verify Dimensions structure
                Assert.That(dataResponse.Dimension, Has.Count.EqualTo(3));
            });

            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task GetDataAsync_NoCacheData_ReturnsCorrectJsonStat2FromStream()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = [
                "Alue:code=ksu,pks",
                "Tiedot:code=neljmuut"
            ];
            const string? lang = "en";

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data: 1 metric × 10 time periods × 2 regions = 20 data points
            // neljmuut for ksu and pks regions across all time periods
            double[] expectedValues = [
                0.3, -0.1,    // 2022Q1: ksu, pks
                1.3, 1.2,     // 2022Q2: ksu, pks
                -1.7, -2.2,   // 2022Q3: ksu, pks
                -3, -2.9,     // 2022Q4: ksu, pks
                -2.2, -2.7,   // 2023Q1: ksu, pks
                -0.4, -1,     // 2023Q2: ksu, pks
                -2, -2.1,     // 2023Q3: ksu, pks
                -1.3, -1.9,   // 2023Q4: ksu, pks
                -1.5, -1.3,   // 2024Q1: ksu, pks
                 0.8, 0,      // 2024Q2: ksu, pks
            ];

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, lang);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkObjectResult>());
                OkObjectResult okResult = (OkObjectResult)result!;
                Assert.That(okResult.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 jsonStat = (JsonStat2)okResult.Value!;
                Assert.That(jsonStat, Is.Not.Null);
                Assert.That(jsonStat.Value, Is.Not.Null);
                
                // Should return 1 metric × 10 time periods × 2 regions = 20 data points
                Assert.That(jsonStat.Value, Has.Length.EqualTo(20));
                
                // Verify JSON-stat metadata
                Assert.That(jsonStat.Version, Is.EqualTo("2.0"));
                Assert.That(jsonStat.Class, Is.EqualTo("dataset"));
                Assert.That(jsonStat.Id, Is.EqualTo(expected));
                Assert.That(jsonStat.Label, Is.EqualTo("test_description_en"));
                Assert.That(jsonStat.Source, Is.EqualTo("Statistics Finland, prices of dwellings in housing companies"));
                
                // Verify dimensions structure  
                Assert.That(jsonStat.Dimension, Is.Not.Null);
                Assert.That(jsonStat.Dimension, Has.Count.EqualTo(3));
                Assert.That(jsonStat.Dimension.ContainsKey("vuosineljannes"));
                Assert.That(jsonStat.Dimension.ContainsKey("alue"));
                Assert.That(jsonStat.Dimension.ContainsKey("tiedot"));
                
                // Verify size array matches expectedFi dimensions
                Assert.That(jsonStat.Size, Has.Count.EqualTo(3));
                Assert.That(jsonStat.Size[0], Is.EqualTo(10)); // 10 Vuosineljännes values  
                Assert.That(jsonStat.Size[1], Is.EqualTo(2)); // 2 Alue values
                Assert.That(jsonStat.Size[2], Is.EqualTo(1)); // 1 Tiedot value
                
                // Verify all data points exist
                Assert.That(jsonStat.Value.All(v => v.Type == DataValueType.Exists), Is.True);
                
                // Compare actual data against expectedFi array
                double[] actualValues = [.. jsonStat.Value.Select(v => v.UnsafeValue)];
                Assert.That(actualValues, Is.EqualTo(expectedValues));
                
                // Verify extension contains English missing value translations
                Assert.That(jsonStat.Extension, Is.Not.Null);
                Assert.That(jsonStat.Extension!.ContainsKey("missingValueDescriptions"));
                Dictionary<DataValueType, string>? translations = jsonStat.Extension["missingValueDescriptions"] as Dictionary<DataValueType, string>;
                Assert.That(translations, Is.Not.Null);
                Assert.That(translations![DataValueType.Missing], Is.EqualTo("Missing"));
            });

            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task PostDataAsync_ExactDatasetInCache_ReturnsFromCache()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            Dictionary<string, Filter> query = new()
            {
                { "Tiedot", new CodeFilter(["vmuut"]) },
                { "Alue", new CodeFilter(["ksu", "pks"]) }
            };
            const string? lang = "en";

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Expected data: 1 metric × 10 time periods × 2 regions = 20 data points
            // vmuut for ksu and pks regions across all time periods
            double[] expectedValues = [
                3, 2.8,       // 2022Q1: ksu, pks
                1.9, 1.8,     // 2022Q2: ksu, pks
                0.3, -0.2,    // 2022Q3: ksu, pks
                -3, -4.1,     // 2022Q4: ksu, pks
                -5.5, -6.6,   // 2023Q1: ksu, pks
                -7.1, -8.6,   // 2023Q2: ksu, pks
                -7.4, -8.5,   // 2023Q3: ksu, pks
                -5.8, -7.5,   // 2023Q4: ksu, pks
                -5.1, -6.1,    // 2024Q1: ksu, pks
                -4, -5.1     // 2024Q2: ksu, pks
            ];

            // Act - First call to populate cache
            IActionResult result1 = await _controller.PostDataAsync(database, table, query, lang);
            
            // Act - Second call should use cache
            IActionResult result2 = await _controller.PostDataAsync(database, table, query, lang);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result1, Is.InstanceOf<OkObjectResult>());
                Assert.That(result2, Is.InstanceOf<OkObjectResult>());
                
                OkObjectResult okResult1 = (OkObjectResult)result1!;
                OkObjectResult okResult2 = (OkObjectResult)result2!;
                
                Assert.That(okResult1.Value, Is.InstanceOf<JsonStat2>());
                Assert.That(okResult2.Value, Is.InstanceOf<JsonStat2>());
                
                JsonStat2 jsonStat1 = (JsonStat2)okResult1.Value!;
                JsonStat2 jsonStat2 = (JsonStat2)okResult2.Value!;
                
                Assert.That(jsonStat1, Is.Not.Null);
                Assert.That(jsonStat2, Is.Not.Null);
                Assert.That(jsonStat1.Value, Is.Not.Null);
                Assert.That(jsonStat2.Value, Is.Not.Null);
                Assert.That(jsonStat1.Value, Has.Length.EqualTo(jsonStat2.Value.Length));
                
                // Should return 1 metric × 10 time periods × 2 regions = 20 data points
                Assert.That(jsonStat1.Value, Has.Length.EqualTo(20));
                Assert.That(jsonStat2.Value, Has.Length.EqualTo(20));
                
                // Verify all data points exist
                Assert.That(jsonStat1.Value.All(v => v.Type == DataValueType.Exists), Is.True);
                Assert.That(jsonStat2.Value.All(v => v.Type == DataValueType.Exists), Is.True);
                
                // Compare actual data against expectedFi array for both calls
                double[] actualValues1 = [.. jsonStat1.Value.Select(v => v.UnsafeValue)];
                double[] actualValues2 = [.. jsonStat2.Value.Select(v => v.UnsafeValue)];
                Assert.That(actualValues1, Is.EqualTo(expectedValues));
                Assert.That(actualValues2, Is.EqualTo(expectedValues));
                Assert.That(actualValues1, Is.EqualTo(actualValues2));
            });

            _mockConnector.Verify(c => c.ReadPxFile(_testTable), Times.AtLeastOnce);
        }

        [Test]
        public async Task GetDataAsync_DatabaseNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "nonexistentdb";
            string table = "testtable";
            string[] filters = [];

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_TableNotFound_ReturnsNotFound()
        {
            // Arrange
            string database = "testdb";
            string table = "nonexistenttable";
            string[] filters = [];

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetDataAsync_InvalidLanguage_ReturnsBadRequest()
        {
            // Arrange
            string database = "testdb";
            string table = "testtable";
            string[] filters = [];
            const string invalidLang = "invalid";

            _controller.ControllerContext.HttpContext.Request.Headers.Accept = "application/json";

            // Act
            IActionResult result = await _controller.GetDataAsync(database, table, filters, invalidLang);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }
    }
}
