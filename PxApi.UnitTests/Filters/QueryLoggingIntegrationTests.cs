using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Elastic.Clients.Elasticsearch;
using Moq;
using Px.Utils.Models.Data.DataValue;
using Px.Utils.Models.Data;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.DataSources;
using PxApi.Filters;
using PxApi.Models;
using PxApi.Models.JsonStat;
using PxApi.Exceptions;
using PxApi.Services;
using PxApi.Services.Search;
using PxApi.Models.Search;
using PxApi.UnitTests.ModelBuilderTests;
using PxApi.UnitTests.Utils;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PxApi.UnitTests.Filters;

[TestFixture]
[NonParallelizable]
public class QueryLoggingIntegrationTests
{
    private WebApplication application = null!;
    private HttpClient client = null!;
    private readonly CompletionLogger logger = new();
    private Mock<ICachedDataSource> source = null!;
    private Mock<ISearchService> search = null!;
    private FailingJsonStatConverter serializer = null!;
    private int backendRequests;
    private int backendStatus;
    private JsonElement? backendTotal;
    private bool malformedBackendResponse;
    private string backendErrorType = "test_error";

    [SetUp]
    public async Task SetUp()
    {
        logger.Events.Clear();
        logger.Diagnostics.Clear();
        backendRequests = 0;
        backendStatus = 400;
        backendTotal = null;
        malformedBackendResponse = false;
        backendErrorType = "test_error";
        Dictionary<string, string?> config = TestConfigFactory.Merge(TestConfigFactory.Base(), TestConfigFactory.MountedDb(0, "db", "."));
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(config);
        TestConfigFactory.BuildAndLoad(config);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddProvider(logger);
        Program.AddServices(builder.Services);
        serializer = new FailingJsonStatConverter();
        builder.Services.Configure<JsonOptions>(options => options.JsonSerializerOptions.Converters.Insert(0, serializer));
        builder.Services.AddSingleton<ILogger<QueryCompletionMiddleware>>(logger);
        source = new Mock<ICachedDataSource>();
        DataBaseRef database = DataBaseRef.Create("db");
        PxFileRef file = PxFileRef.ValidateAndCreate("table", database);
        IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
        source.Setup(instance => instance.GetDataBaseReference("db")).Returns(database);
        source.Setup(instance => instance.GetFileReferenceCachedAsync("table", database, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        source.Setup(instance => instance.GetFileListCachedAsync(database, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableSortedDictionary<string, PxFileRef>.Empty.Add("table", file));
        source.Setup(instance => instance.GetMetadataCachedAsync(file, It.IsAny<CancellationToken>())).ReturnsAsync(metadata);
        source.Setup(instance => instance.GetDataCachedAsync(file, It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DoubleDataValue(1, DataValueType.Exists), new DoubleDataValue(2, DataValueType.Exists)]);
        builder.Services.RemoveAll<ICachedDataSource>();
        builder.Services.AddSingleton(source.Object);
        search = new Mock<ISearchService>();
        builder.Services.RemoveAll<ISearchService>();
        builder.Services.AddSingleton(search.Object);
        application = builder.Build();
        application.UseRouting();
        application.UseMiddleware<QueryCompletionMiddleware>();
        application.UseExceptionHandler("/error");
        application.MapControllers();
        application.MapPost("/test/untracked-415", () => Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));
        application.MapGet("/test/failed-response", async (HttpContext context) =>
        {
            QueryObservation observation = QueryObservation.Get(context)!;
            observation.PreparedCells = 10;
            observation.ResponseExecutionStarted = true;
            await context.Response.WriteAsync("partial");
            await context.Response.Body.FlushAsync();
            throw new IOException("Test response write failed.");
        }).WithMetadata(new ControllerActionDescriptor { ControllerName = "Data" });
        application.MapPost("/test-elasticsearch/{**path}", async (HttpContext context) =>
        {
            Interlocked.Increment(ref backendRequests);
            context.Response.StatusCode = backendStatus;
            context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
            if (malformedBackendResponse)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{");
                return;
            }
            if (backendStatus != 200)
            {
                await context.Response.WriteAsJsonAsync(new { error = new { type = backendErrorType, reason = "Test backend error." }, status = backendStatus });
                return;
            }
            Dictionary<string, object?> hits = new() { ["hits"] = Array.Empty<object>() };
            if (backendTotal.HasValue) hits["total"] = backendTotal.Value;
            await context.Response.WriteAsJsonAsync(new { took = 1, timed_out = false, _shards = new { total = 1, successful = 1, skipped = 0, failed = 0 }, hits });
        });
        await application.StartAsync();
        string address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await application.StopAsync();
        await application.DisposeAsync();
    }

    [TestCase("GET")]
    [TestCase("HEAD")]
    [TestCase("OPTIONS")]
    public async Task DataRequest_EmitsExactlyOneEventAfterMvcExecution(string method)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), "/data/databases/db/tables/table");
        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();

        Dictionary<string, object?> completed = Completion();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(completed["operation"], Is.EqualTo("data"));
            Assert.That(completed["method"], Is.EqualTo(method));
            Assert.That(completed["outcome"], Is.EqualTo("success"));
            Assert.That(completed.ContainsKey("returned_cells"), Is.EqualTo(method == "GET"));
        }
    }

    [Test]
    public async Task GetAndPost_EquivalentSelections_PreserveSummary()
    {
        using HttpResponseMessage get = await client.GetAsync("/data/databases/db/tables/table");
        await get.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> getCompletion = Completion();
        logger.Events.Clear();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");
        using HttpResponseMessage post = await client.PostAsync("/data/databases/db/tables/table", content);
        await post.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> postCompletion = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(postCompletion["requested_cells"], Is.EqualTo(getCompletion["requested_cells"]));
            Assert.That(postCompletion["dimensions_json"], Is.EqualTo(getCompletion["dimensions_json"]));
        }
    }

    [TestCase("HEAD", "db", 200, null)]
    [TestCase("OPTIONS", "db", 200, null)]
    [TestCase("HEAD", "missing", 404, "database_not_found")]
    [TestCase("OPTIONS", "missing", 404, "database_not_found")]
    public async Task TableProbes_RecordResolvedIdentityAndRejection(string method, string database, int status, string? reason)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), $"/meta/databases/{database}/tables");
        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
            Assert.That(completed["operation"], Is.EqualTo("tables"));
            Assert.That(completed["method"], Is.EqualTo(method));
            Assert.That(completed["requested_database_id"], Is.EqualTo(database));
            Assert.That(completed.GetValueOrDefault("database_id"), Is.EqualTo(status == 200 ? "db" : null));
            Assert.That(completed.GetValueOrDefault("error_code"), Is.EqualTo(reason));
            Assert.That(completed["outcome"], Is.EqualTo(status == 200 ? "success" : "rejected"));
            if (method == "HEAD")
            {
                Assert.That(completed["page"], Is.EqualTo(1));
                Assert.That(completed["page_size"], Is.EqualTo(50));
            }
            Assert.That(completed.ContainsKey("returned_tables"), Is.False);
        }
    }

    [TestCase("?page=0")]
    [TestCase("?pageSize=0")]
    [TestCase("?pageSize=101")]
    public async Task TableHead_InvalidPaging_RecordsSpecificRejection(string query)
    {
        using HttpRequestMessage request = new(HttpMethod.Head, "/meta/databases/db/tables" + query);
        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(Completion()["error_code"], Is.EqualTo("invalid_paging"));
            Assert.That(Completion()["outcome"], Is.EqualTo("rejected"));
        }
    }

    [TestCase("/data/databases/missing/tables/table", 404, "database_not_found")]
    [TestCase("/data/databases/db/tables/missing", 404, "table_not_found")]
    [TestCase("/data/databases/db/tables/table?lang=missing", 400, "invalid_language")]
    [TestCase("/data/databases/db/tables/table?filters=bad", 400, "invalid_filter")]
    [TestCase("/meta/databases/db/tables?page=0", 400, "invalid_model")]
    [TestCase("/meta/search?q=population", 404, "feature_disabled")]
    public async Task Rejections_EmitOnceAndKeepOriginalOperation(string path, int status, string reason)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
            Assert.That(completed["error_code"], Is.EqualTo(reason));
            Assert.That(completed["outcome"], Is.EqualTo("rejected"));
            Assert.That(completed["operation"], Is.Not.EqualTo("error"));
            Assert.That(completed.ContainsKey("returned_cells"), Is.False);
            if (reason is "database_not_found" or "table_not_found" or "invalid_language")
                Assert.That(logger.Diagnostics.Where(entry => entry.Category == typeof(DataController).FullName && entry.Level == LogLevel.Debug), Is.Empty);
        }
    }

    [TestCase("text/plain")]
    [TestCase("application/xml")]
    public async Task UnsupportedContentType_EmitsOnceAndKeepsDataIdentity(string contentType)
    {
        using StringContent content = new("unsupported", Encoding.UTF8, contentType);
        using HttpResponseMessage response = await client.PostAsync("/data/databases/db/tables/table", content);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
            Assert.That(completed["operation"], Is.EqualTo("data"));
            Assert.That(completed["method"], Is.EqualTo("POST"));
            Assert.That(completed["route_template"], Is.EqualTo("data/databases/{database}/tables/{table}"));
            Assert.That(completed["requested_database_id"], Is.EqualTo("db"));
            Assert.That(completed["requested_table_id"], Is.EqualTo("table"));
            Assert.That(completed["status_code"], Is.EqualTo(415));
            Assert.That(completed["outcome"], Is.EqualTo("rejected"));
            Assert.That(completed["error_code"], Is.EqualTo("unsupported_content_type"));
            Assert.That(completed.ContainsKey("returned_cells"), Is.False);
        }
    }

    [TestCase("/test/untracked-415", 415)]
    [TestCase("/data/databases/db/tables/table/unmatched", 404)]
    public async Task UntrackedPost_DoesNotEmitCompletion(string path, int status)
    {
        using StringContent content = new("unsupported", Encoding.UTF8, "text/plain");
        using HttpResponseMessage response = await client.PostAsync(path, content);
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
            Assert.That(logger.Events, Is.Empty);
        }
    }

    [TestCase("{")]
    [TestCase("{\"dimension\":{}}")]
    [TestCase("{\"dimension\":{\"first\":2}}")]
    [TestCase("{\"dimension\":{\"type\":\"First\"}}")]
    [TestCase("{\"dimension\":{\"query\":[\"A\"]}}")]
    [TestCase("{\"dimension\":null}")]
    [TestCase("{\"dimension\":{\"type\":\"code\",\"query\":null}}")]
    [TestCase("{\"dimension\":{\"type\":\"from\",\"query\":null}}")]
    [TestCase("{\"dimension\":{\"type\":\"to\",\"query\":null}}")]
    [TestCase("{\"dimension\":{\"type\":\"first\",\"query\":null}}")]
    [TestCase("{\"dimension\":{\"type\":\"last\",\"query\":null}}")]
    [TestCase("{\"dimension\":{\"type\":\"code\",\"query\":[null]}}")]
    [TestCase("{\"dimension\":{\"type\":99,\"query\":1}}")]
    public async Task MalformedBody_Reexecution_PreservesDataIdentity(string body)
    {
        using StringContent content = new(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync("/data/databases/db/tables/table", content);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(completed["operation"], Is.EqualTo("data"));
            Assert.That(completed["requested_table_id"], Is.EqualTo("table"));
            Assert.That(completed["method"], Is.EqualTo("POST"));
            Assert.That(completed["outcome"], Is.EqualTo("rejected"));
            Assert.That(completed["error_code"], Is.EqualTo("invalid_model"));
            Assert.That(completed.ContainsKey("returned_cells"), Is.False);
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures[0].Exception, Is.TypeOf<InvalidModelException>());
            Assert.That(logger.Diagnostics.Where(entry => entry.Level >= LogLevel.Error), Is.Empty);
        }
    }

    [TestCase("text/csv", 200, "text/csv")]
    [TestCase("image/png", 406, null)]
    public async Task Negotiation_RecordsSelectedFormatOnly(string accept, int status, string? format)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/data/databases/db/tables/table");
        request.Headers.Accept.ParseAdd(accept);
        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
            Assert.That(completed.GetValueOrDefault("format"), Is.EqualTo(format));
            Assert.That(completed.ContainsKey("returned_cells"), Is.EqualTo(status == 200));
        }
    }

    [TestCase("GET")]
    [TestCase("POST")]
    public async Task EmptySelection_IsRejectedWithoutFetchingData(string method)
    {
        string dimension = TestMockMetaBuilder.GetMockMetadata().Dimensions[0].Code;
        string path = "/data/databases/db/tables/table";
        using HttpRequestMessage request = new(new HttpMethod(method), method == "GET"
            ? path + "?filters=" + Uri.EscapeDataString(dimension + ":code=REVIEW_UNKNOWN_CODE") : path);
        if (method == "POST") request.Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [dimension] = new { type = "code", query = new[] { "REVIEW_UNKNOWN_CODE" } }
        }), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(completed["outcome"], Is.EqualTo("rejected"));
            Assert.That(completed["error_code"], Is.EqualTo("invalid_filter"));
            Assert.That(completed.ContainsKey("returned_cells"), Is.False);
            Assert.That(logger.Diagnostics.Count(entry => entry.Exception is ArgumentException), Is.EqualTo(1));
            Assert.That(logger.Diagnostics.Where(entry => entry.Level >= LogLevel.Error), Is.Empty);
        }
        source.Verify(instance => instance.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Unauthorized_StopsBeforeActionButEmitsCompletion()
    {
        Dictionary<string, string?> config = TestConfigFactory.Base();
        config["Authentication:Data:Key"] = Guid.NewGuid().ToString("N");
        TestConfigFactory.BuildAndLoad(config);

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(Completion()["error_code"], Is.EqualTo("unauthorized"));
        }
    }

    [Test]
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task DownstreamDiagnostics_PreserveActionScopeAfterAuthentication(bool authenticationEnabled, bool rejected)
    {
        application.Configuration["LogOptions:AuditLog:Enabled"] = "true";
        if (authenticationEnabled)
        {
            string key = Guid.NewGuid().ToString("N");
            Dictionary<string, string?> config = TestConfigFactory.Merge(TestConfigFactory.Base(), TestConfigFactory.MountedDb(0, "db", "."));
            config["Authentication:Data:Key"] = key;
            config["Authentication:Data:HeaderName"] = "X-Test-Key";
            TestConfigFactory.BuildAndLoad(config);
            client.DefaultRequestHeaders.Add("X-Test-Key", key);
        }

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table" + (rejected ? "?filters=bad" : string.Empty));
        await response.Content.ReadAsByteArrayAsync();
        string category = (rejected ? typeof(DataController) : typeof(AuditLogService)).FullName!;
        Diagnostic diagnostic = logger.Diagnostics.Single(entry => entry.Category == category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(rejected ? HttpStatusCode.BadRequest : HttpStatusCode.OK));
            Assert.That(diagnostic.Scope["controller"], Is.EqualTo(nameof(DataController)));
            Assert.That(diagnostic.Scope["action"], Is.EqualTo("GetData"));
            Assert.That(diagnostic.Scope.ContainsKey("function"), Is.False);
            Assert.That(diagnostic.Scope["request_id"], Is.EqualTo(Completion()["request_id"]));
            Assert.That(diagnostic.Scope["operation"], Is.EqualTo("data"));
            if (authenticationEnabled)
                Assert.That(logger.Diagnostics.Single(entry => entry.Category == typeof(PxApi.Authentication.ApiKeyAuthAttribute).FullName).Scope["controller"],
                    Is.EqualTo(nameof(PxApi.Authentication.ApiKeyAuthAttribute)));
        }
    }

    [TestCase("GET")]
    [TestCase("POST")]
    public async Task TooManyCells_RecordsDemandBeforeSelectionSummaryOrDataFetch(string method)
    {
        Dictionary<string, string?> config = TestConfigFactory.Base();
        config["QueryLimits:JsonStatMaxCells"] = "1";
        TestConfigFactory.BuildAndLoad(config);

        using HttpRequestMessage request = new(new HttpMethod(method), "/data/databases/db/tables/table");
        if (method == "POST") request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(413));
            Assert.That(Completion()["requested_cells"], Is.GreaterThan(1L));
            Assert.That(Completion()["error_code"], Is.EqualTo("too_many_cells"));
            Assert.That(Completion().ContainsKey("dimensions_json"), Is.False);
            Assert.That(Completion().ContainsKey("selection_classification_version"), Is.False);
            Assert.That(Completion().ContainsKey("returned_cells"), Is.False);
            source.Verify(instance => instance.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Test]
    public async Task SerializerFailure_IsFailedEvenWhenErrorHandlerReturns400()
    {
        serializer.Fail = true;

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(Completion()["outcome"], Is.EqualTo("failed"));
            Assert.That(Completion()["error_code"], Is.EqualTo("serialization_failed"));
            Assert.That(Completion().ContainsKey("returned_cells"), Is.False);
            Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(ErrorController).FullName));
        }
    }

    [Test]
    public void PartialWriteFailure_IsNotSuccessfulDespiteCommitted200()
    {
        Assert.CatchAsync<HttpRequestException>(async () =>
        {
            using HttpResponseMessage response = await client.GetAsync("/test/failed-response");
            await response.Content.ReadAsByteArrayAsync();
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Completion()["status_code"], Is.EqualTo(200));
            Assert.That(Completion()["outcome"], Is.EqualTo("failed"));
            Assert.That(Completion().ContainsKey("returned_cells"), Is.False);
            Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(QueryCompletionMiddleware).FullName));
        }
    }

    [Test]
    public async Task Cancellation_RecordsUnknownCauseWithoutAssumingDisconnect()
    {
        source.Setup(instance => instance.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(499));
            Assert.That(Completion()["outcome"], Is.EqualTo("cancelled"));
            Assert.That(Completion()["error_code"], Is.EqualTo("cancellation_unknown"));
            Assert.That(logger.Diagnostics.Where(entry => entry.Category == typeof(OperationCanceledExceptionFilter).FullName), Is.Empty);
        }
    }

    [Test]
    public async Task SearchUnavailable_Emits503CompletionWithoutSearchText()
    {
        application.Configuration["FeatureManagement:SearchController"] = "true";
        search.Setup(instance => instance.SearchAsync(It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SearchUnavailableException("Test backend unavailable."));

        using HttpResponseMessage response = await client.GetAsync("/meta/search?q=population");
        await response.Content.ReadAsByteArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(Completion()["error_code"], Is.EqualTo("search_unavailable"));
            Assert.That(Completion()["search_target"], Is.EqualTo("content"));
            Assert.That(Completion().ContainsKey("search_text"), Is.False);
            Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(SearchController).FullName));
        }
    }

    [Test]
    public async Task InvalidBinaryMetadata_LogsOnceAtErrorBoundaryWithStorageContext()
    {
        DataBaseRef database = DataBaseRef.Create("db");
        PxFileRef file = PxFileRef.ValidateAndCreate("table", database);
        DiagnosticBinaryConnector connector = new(database, application.Services.GetRequiredService<ILogger<BinaryBlobDataBaseConnector>>());
        source.Setup(instance => instance.GetMetadataCachedAsync(file, It.IsAny<CancellationToken>()))
            .Returns((PxFileRef referencedFile, CancellationToken token) => connector.ReadMetadataAsync(referencedFile, token));

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        string body = await response.Content.ReadAsStringAsync();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(Completion()["outcome"], Is.EqualTo("failed"));
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(ErrorController).FullName));
            Assert.That(failures.Single().Exception, Is.InstanceOf<InvalidDataException>());
            Assert.That(failures.Single().Exception!.InnerException, Is.InstanceOf<JsonException>());
            Assert.That(failures.Single().Exception!.Message, Does.Contain("meta/db/table_202501010000.meta.json"));
            Assert.That(failures.Single().Exception!.Message, Does.Contain("test-container"));
            Assert.That(body, Does.Not.Contain("test-container"));
            Assert.That(logger.Diagnostics.Count(entry => entry.Level >= LogLevel.Error), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task MissingBinaryData_LogsOnceAtControllerWithStorageContext()
    {
        DataBaseRef database = DataBaseRef.Create("db");
        PxFileRef file = PxFileRef.ValidateAndCreate("table", database);
        IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
        DiagnosticBinaryConnector connector = new(database, application.Services.GetRequiredService<ILogger<BinaryBlobDataBaseConnector>>());
        source.Setup(instance => instance.GetDataCachedAsync(file, It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()))
            .Returns((PxFileRef referencedFile, IMatrixMap map, CancellationToken token) => connector.ReadDataAsync(referencedFile, map, metadata, token));

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        string body = await response.Content.ReadAsStringAsync();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(Completion()["error_code"], Is.EqualTo("data_unavailable"));
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(DataController).FullName));
            Assert.That(failures.Single().Level, Is.EqualTo(LogLevel.Information));
            Assert.That(failures.Single().Exception, Is.InstanceOf<BinaryBlobSynchronizationException>());
            Assert.That(failures.Single().Exception!.Message, Does.Contain("test-container/bin/db/table"));
            Assert.That(body, Does.Not.Contain("test-container"));
            Assert.That(logger.Diagnostics.Where(entry => entry.Level >= LogLevel.Error), Is.Empty);
        }
    }

    [Test]
    public async Task MissingMetadataStorageFile_LogsOnceWithoutExposingStoragePath()
    {
        FileNotFoundException failure = new("Metadata blob missing from test-container.", "meta/db/table.meta.json");
        source.Setup(instance => instance.GetMetadataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        using HttpResponseMessage response = await client.GetAsync("/meta/databases/db/tables/table");
        string body = await response.Content.ReadAsStringAsync();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(Completion()["operation"], Is.EqualTo("metadata"));
            Assert.That(Completion()["error_code"], Is.EqualTo("table_not_found"));
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(MetadataController).FullName));
            Assert.That(failures.Single().Exception, Is.SameAs(failure));
            Assert.That(body, Does.Not.Contain("test-container"));
            Assert.That(body, Does.Not.Contain(failure.FileName));
        }
    }

    [Test]
    public async Task MissingConnector_LogsOnceAtErrorBoundary()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        DataBaseConnectorFactoryImpl factory = new(provider, application.Services.GetRequiredService<ILogger<DataBaseConnectorFactoryImpl>>());
        IReadOnlyMatrixMetadata metadata = TestMockMetaBuilder.GetMockMetadata();
        source.Setup(instance => instance.GetDataCachedAsync(It.IsAny<PxFileRef>(), It.IsAny<IMatrixMap>(), It.IsAny<CancellationToken>()))
            .Returns((PxFileRef referencedFile, IMatrixMap map, CancellationToken token) => factory.GetConnector(referencedFile.DataBase).ReadDataAsync(referencedFile, map, metadata, token));

        using HttpResponseMessage response = await client.GetAsync("/data/databases/db/tables/table");
        await response.Content.ReadAsByteArrayAsync();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(Completion()["outcome"], Is.EqualTo("failed"));
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(ErrorController).FullName));
            Assert.That(failures.Single().Exception!.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(failures.Single().Exception!.Message, Does.Contain("db"));
            Assert.That(logger.Diagnostics.Count(entry => entry.Level >= LogLevel.Error), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task InvalidSearchBackendResponse_LogsOnlyAtController()
    {
        UseLoopbackSearchBackend();

        using HttpResponseMessage response = await client.GetAsync("/meta/search?q=population");
        await response.Content.ReadAsByteArrayAsync();
        Diagnostic[] failures = [.. logger.Diagnostics.Where(entry => entry.Exception is not null)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(Completion()["error_code"], Is.EqualTo("search_unavailable"));
            Assert.That(backendRequests, Is.EqualTo(1));
            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures.Single().Category, Is.EqualTo(typeof(SearchController).FullName));
            Assert.That(failures.Single().Exception!.Message, Is.EqualTo("Search backend returned an error."));
            Assert.That(((SearchUnavailableException)failures.Single().Exception!).StatusCode, Is.EqualTo(400));
            Assert.That(((SearchUnavailableException)failures.Single().Exception!).ErrorType, Is.EqualTo("test_error"));
            Assert.That(failures.Single().Message, Does.Contain("400").And.Contain("test_error"));
            Assert.That(failures.Single().Message, Does.Not.Contain("Test backend error."));
            Assert.That(logger.Diagnostics.Where(entry => entry.Category == typeof(ElasticSearchService).FullName), Is.Empty);
            Assert.That(logger.Diagnostics.Count(entry => entry.Level >= LogLevel.Error), Is.EqualTo(1));
        }
    }

    [TestCase("{\"value\":12,\"relation\":\"eq\"}", 12L, "exact")]
    [TestCase("{\"value\":10000,\"relation\":\"gte\"}", 10000L, "lower_bound")]
    [TestCase("{\"value\":0,\"relation\":\"eq\"}", 0L, "exact")]
    [TestCase("7", 7L, "exact")]
    [TestCase(null, null, "unknown")]
    public async Task SearchBackendTotals_RealSdkMapping_PreservesObservedMeaning(string? totalJson, long? expectedTotal, string relation)
    {
        backendStatus = 200;
        backendTotal = totalJson is null ? null : JsonSerializer.Deserialize<JsonElement>(totalJson);
        UseLoopbackSearchBackend();

        using HttpResponseMessage response = await client.GetAsync("/meta/search?q=population&page=2&pageSize=10");
        await response.Content.ReadAsByteArrayAsync();
        Dictionary<string, object?> completed = Completion();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(completed["outcome"], Is.EqualTo("success"));
            Assert.That(completed.ContainsKey("total_matches"), Is.EqualTo(expectedTotal.HasValue));
            Assert.That(completed.GetValueOrDefault("total_matches"), Is.EqualTo(expectedTotal));
            Assert.That(completed["total_matches_relation"], Is.EqualTo(relation));
            Assert.That(completed["returned_matches"], Is.EqualTo(0));
            Assert.That(completed["page"], Is.EqualTo(2));
            Assert.That(completed["page_size"], Is.EqualTo(10));
            Assert.That(backendRequests, Is.EqualTo(1));
            Assert.That(logger.Diagnostics.Where(entry => entry.Level >= LogLevel.Error), Is.Empty);
        }
    }

    [Test]
    public async Task MalformedSearchBackendResponse_PreservesSdkTransportException()
    {
        backendStatus = 200;
        malformedBackendResponse = true;
        UseLoopbackSearchBackend();

        using HttpResponseMessage response = await client.GetAsync("/meta/search?q=population");
        string body = await response.Content.ReadAsStringAsync();
        Diagnostic failure = logger.Diagnostics.Single(entry => entry.Exception is not null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(failure.Exception, Is.TypeOf<SearchUnavailableException>());
            Assert.That(failure.Exception!.InnerException, Is.Not.Null);
            Assert.That(failure.Category, Is.EqualTo(typeof(SearchController).FullName));
            Assert.That(JsonSerializer.Deserialize<string>(body), Is.EqualTo("Search is temporarily unavailable."));
            Assert.That(Completion()["error_code"], Is.EqualTo("search_unavailable"));
            Assert.That(backendRequests, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task SearchBackendErrorType_IsBoundedAndSanitized()
    {
        backendErrorType = new string('a', 140) + "\r\nforged";
        UseLoopbackSearchBackend();

        using HttpResponseMessage response = await client.GetAsync("/meta/search?q=population");
        await response.Content.ReadAsByteArrayAsync();
        Diagnostic failure = logger.Diagnostics.Single(entry => entry.Exception is not null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(((SearchUnavailableException)failure.Exception!).ErrorType, Is.EqualTo(new string('a', 128)));
            Assert.That(failure.Message, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("forged"));
        }
    }

    private void UseLoopbackSearchBackend()
    {
        application.Configuration["FeatureManagement:SearchController"] = "true";
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Search:IndexPrefix"] = "test"
        }).Build();
        ElasticsearchClientSettings settings = new ElasticsearchClientSettings(new Uri(client.BaseAddress!, "test-elasticsearch/")).MaximumRetries(0);
        ElasticSearchService backend = new(new ElasticsearchClient(settings), new SearchConfig(configuration.GetSection("Search")));
        search.Setup(instance => instance.SearchAsync(It.IsAny<string>(), It.IsAny<SearchTarget>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string query, SearchTarget target, string language, int page, int pageSize, CancellationToken token) => backend.SearchAsync(query, target, language, page, pageSize, token));
    }

    private sealed class DiagnosticBinaryConnector(DataBaseRef database, ILogger<BinaryBlobDataBaseConnector> connectorLogger)
        : BinaryBlobDataBaseConnector(database, "test-container", null!, connectorLogger)
    {
        internal override Task<IReadOnlyList<string>> GetBlobItemsAsync(string prefix, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["meta/db/table_202501010000.meta.json"]);

        internal override Task<Stream> OpenBlobReadStreamAsync(string blobName, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("{")));

        internal override Task<bool> BlobExistsAsync(string blobName, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class FailingJsonStatConverter : JsonConverter<JsonStat2>
    {
        public bool Fail { get; set; }
        public override JsonStat2? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, JsonStat2 value, JsonSerializerOptions options)
        {
            if (Fail) throw new JsonException("Test serialization failed.");
            JsonSerializer.Serialize(writer, value, GlobalJsonConverterOptions.Default);
        }
    }

    private Dictionary<string, object?> Completion()
    {
        Assert.That(logger.Events.Count, Is.EqualTo(1));
        return logger.Events.Single();
    }

    private sealed record Diagnostic(string Category, LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> Scope);

    private sealed class CompletionLogger : ILogger<QueryCompletionMiddleware>, ILoggerProvider
    {
        private readonly LoggerExternalScopeProvider scopes = new();

        public ConcurrentQueue<Dictionary<string, object?>> Events { get; } = new();
        public ConcurrentQueue<Diagnostic> Diagnostics { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CategoryLogger(categoryName, this);
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Capture(typeof(QueryCompletionMiddleware).FullName!, logLevel, eventId, state, exception, formatter);

        private void Capture<TState>(string categoryName, LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name == "query_completed" && state is IEnumerable<KeyValuePair<string, object?>> properties)
                Events.Enqueue(properties.ToDictionary());
            else if (categoryName.StartsWith("PxApi.", StringComparison.Ordinal))
            {
                Dictionary<string, object?> scope = [];
                scopes.ForEachScope((state, captured) =>
                {
                    if (state is IEnumerable<KeyValuePair<string, object?>> values)
                        foreach (KeyValuePair<string, object?> value in values) captured[value.Key] = value.Value;
                }, scope);
                Diagnostics.Enqueue(new Diagnostic(categoryName, logLevel, formatter(state, exception), exception, scope));
            }
        }

        private sealed class CategoryLogger(string categoryName, CompletionLogger owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Capture(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }
}