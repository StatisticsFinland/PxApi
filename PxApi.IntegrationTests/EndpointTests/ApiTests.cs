using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NLog;
using PxApi.Configuration;
using PxApi.IntegrationTests.Infrastructure;

namespace PxApi.IntegrationTests.EndpointTests;

[TestFixture]
[NonParallelizable]
public sealed class ApiTests
{
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void StartApplication()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    [OneTimeTearDown]
    public void StopApplication()
    {
        _client?.Dispose();
        _factory?.Dispose();
        if (_factory is not null && Directory.Exists(_factory.LogDirectory))
            Directory.Delete(_factory.LogDirectory, recursive: true);
    }

    [Test]
    public async Task LocalLogs_ContainReadableDebugDiagnostics()
    {
        using HttpResponseMessage clear = await _client.DeleteAsync("/cache/databases/sample");
        Assert.That(clear.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await GetJson("/meta/databases/sample/tables");
        LogManager.GetLogger("PxApi.Services.AuditLogService").Info("Disabled audit routing probe");
        LogManager.GetLogger("PxApi.IntegrationTests.DebugProbe").Trace("Below Debug threshold probe");
        LogManager.Flush();

        string[] files = Directory.GetFiles(_factory.LogDirectory, "nlog-*.log");
        Assert.That(files, Is.Not.Empty);
        string text = string.Join("\n", await Task.WhenAll(files.Select(path => File.ReadAllTextAsync(path))));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Application started. Hosting environment: testing"));
            Assert.That(text, Does.Contain("\"level\": \"debug\""));
            Assert.That(text, Does.Contain("Opening file stream"));
            Assert.That(text, Does.Contain("query_completed"));
            Assert.That(text, Does.Not.Contain("Disabled audit routing probe"));
            Assert.That(text, Does.Not.Contain("Below Debug threshold probe"));
            Assert.That(Directory.GetFiles(_factory.LogDirectory, "audit-*.log"), Is.Empty);
        }
    }

    private async Task<JsonElement> GetJson(string path)
    {
        using HttpResponseMessage response = await _client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        }
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static double[] ExpectedValues(string table) => table == "flows"
        ? [.. Enumerable.Range(0, 12).SelectMany(i => new[] { 100d + i * 10, 80d + i * 10 })]
        : [.. Enumerable.Range(0, 5).SelectMany(region =>
            Enumerable.Range(0, 13).Select(i => region == 0 ? 100d + i * 4 : region * 10d + i))];

    private static void AssertValues(JsonElement data, int[] size, double[] values)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.GetProperty("size").EnumerateArray().Select(v => v.GetInt32()), Is.EqualTo(size));
            Assert.That(data.GetProperty("value").EnumerateArray().Select(v => v.GetDouble()), Is.EqualTo(values));
        }
    }

    [Test]
    public async Task Discovery_UsesOnlySampleConfiguration()
    {
        JsonElement health = await GetJson("/health");
        Assert.That(health.GetProperty("databases").GetArrayLength(), Is.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(health.GetProperty("status").GetString(), Is.EqualTo("healthy"));
            Assert.That(health.GetProperty("databases")[0].GetProperty("id").GetString(), Is.EqualTo("sample"));
            Assert.That(health.GetProperty("databases")[0].GetProperty("status").GetString(), Is.EqualTo("healthy"));
            Assert.That(health.TryGetProperty("search", out _), Is.False);
        }

        await GetJson("/info");
        JsonElement databases = await GetJson("/meta/databases");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(databases.GetArrayLength(), Is.EqualTo(1));
            Assert.That(databases.GetRawText(), Does.Contain("sample"));
        }

        JsonElement tables = await GetJson("/meta/databases/sample/tables");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tables.GetProperty("tables").GetArrayLength(), Is.EqualTo(2));
            Assert.That(AppSettings.Active.Features.SearchController, Is.False);
            Assert.That(AppSettings.Active.ApplicationInsights.IsEnabled, Is.False);
            Assert.That(AppSettings.Active.Authentication.IsEnabled, Is.False);
        }

        JsonElement page = await GetJson("/meta/databases/sample/tables?pageSize=1&page=2");
        Assert.That(page.GetProperty("tables").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Swagger_IsAvailable()
    {
        using HttpResponseMessage ui = await _client.GetAsync("/");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ui.StatusCode, Is.EqualTo(HttpStatusCode.MovedPermanently));
            Assert.That(ui.Headers.Location?.OriginalString, Is.EqualTo("index.html"));
        }
        using HttpResponseMessage index = await _client.GetAsync("/index.html");
        string html = await index.Content.ReadAsStringAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(index.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("swagger").IgnoreCase);
        }
        JsonElement document = await GetJson("/openapi/document.json");
        Assert.That(document.TryGetProperty("openapi", out _), Is.True);
    }

    [TestCase("flows")]
    [TestCase("observations")]
    public async Task Metadata_AndGetAndPostData_ReturnEveryCell(string table)
    {
        JsonElement metadata = await GetJson($"/meta/databases/sample/tables/{table}");
        string[] ids = table == "flows" ? ["TIME", "MEASURE"] : ["REGION", "TIME", "MEASURE"];
        int[] size = table == "flows" ? [12, 2] : [5, 13, 1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.GetProperty("id").EnumerateArray().Select(v => v.GetString()), Is.EqualTo(ids));
            Assert.That(metadata.GetProperty("size").EnumerateArray().Select(v => v.GetInt32()), Is.EqualTo(size));
            Assert.That(metadata.GetProperty("source").GetRawText(), Does.Contain("Synthetic sample data"));
        }
        string parameters = string.Join("&", ids.Select(id => $"filters={id}:code=*"));
        JsonElement get = await GetJson($"/data/databases/sample/tables/{table}?{parameters}");
        AssertValues(get, size, ExpectedValues(table));
        Dictionary<string, object> filters = ids.ToDictionary(id => id,
            _ => (object)new { type = "code", query = new[] { "*" } });
        using HttpResponseMessage response = await _client.PostAsJsonAsync($"/data/databases/sample/tables/{table}", filters);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        AssertValues(await response.Content.ReadFromJsonAsync<JsonElement>(), size, ExpectedValues(table));
    }

    [Test]
    public async Task Filters_SelectExpectedValues()
    {
        JsonElement defaults = await GetJson("/data/databases/sample/tables/flows");
        AssertValues(defaults, [12, 1], [.. Enumerable.Range(0, 12).Select(i => 100d + i * 10)]);
        using HttpResponseMessage flows = await _client.PostAsJsonAsync("/data/databases/sample/tables/flows",
            new { TIME = new { type = "first", query = 2 }, MEASURE = new { type = "code", query = new[] { "IN" } } });
        Assert.That(flows.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertValues(await flows.Content.ReadFromJsonAsync<JsonElement>(), [2, 1], [100, 110]);
        JsonElement observation = await GetJson(
            "/data/databases/sample/tables/observations?filters=REGION:code=B&filters=TIME:code=2015");
        AssertValues(observation, [1, 1, 1], [22]);
        JsonElement last = await GetJson("/data/databases/sample/tables/flows?filters=TIME:last=2");
        AssertValues(last, [2, 1], [200, 210]);
    }

    [Test]
    public async Task Csv_ReturnsSelectedYearsAndValues()
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/data/databases/sample/tables/flows?filters=TIME:last=2");
        request.Headers.Accept.ParseAdd("text/csv");
        using HttpResponseMessage response = await _client.SendAsync(request);
        string csv = await response.Content.ReadAsStringAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/csv"));
            Assert.That(csv.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries),
                Is.EqualTo(new[]
                {
                    "\"Sample annual flows, 2013-2024\",\"Inbound\"",
                    "\"2023\",200",
                    "\"2024\",210"
                }));
        }
    }

    [TestCase("/meta/databases", "GET,HEAD,OPTIONS")]
    [TestCase("/meta/databases/sample/tables", "GET,HEAD,OPTIONS")]
    [TestCase("/meta/databases/sample/tables/flows", "GET,HEAD,OPTIONS")]
    [TestCase("/data/databases/sample/tables/flows", "GET,POST,HEAD,OPTIONS")]
    public async Task HeadAndOptions_ReturnExpectedMethods(string path, string methods)
    {
        using HttpRequestMessage headRequest = new(HttpMethod.Head, path);
        using HttpResponseMessage head = await _client.SendAsync(headRequest);
        byte[] body = await head.Content.ReadAsByteArrayAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(head.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.Empty);
        }
        using HttpRequestMessage optionsRequest = new(HttpMethod.Options, path);
        using HttpResponseMessage options = await _client.SendAsync(optionsRequest);
        IEnumerable<string> allowed = options.Content.Headers.Allow;
        if (options.Headers.TryGetValues("Allow", out IEnumerable<string>? values))
            allowed = allowed.Concat(values.SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(allowed, Is.EquivalentTo(methods.Split(',')));
        }
    }

    [TestCase("/cache/databases/sample/tables/flows")]
    [TestCase("/cache/databases/sample/tables")]
    [TestCase("/cache/databases/sample")]
    public async Task CacheClearing_PreservesResults(string path)
    {
        const string query = "/data/databases/sample/tables/flows?filters=MEASURE:code=*";
        JsonElement before = await GetJson(query);
        using HttpResponseMessage response = await _client.DeleteAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonElement after = await GetJson(query);
        using (Assert.EnterMultipleScope())
        {
            AssertValues(after, [12, 2], ExpectedValues("flows"));
            Assert.That(after.GetProperty("value").GetRawText(), Is.EqualTo(before.GetProperty("value").GetRawText()));
        }
        JsonElement listing = await GetJson("/meta/databases/sample/tables");
        Assert.That(listing.GetProperty("tables").GetArrayLength(), Is.EqualTo(2));
    }

    [TestCase("/meta/databases/missing/tables", HttpStatusCode.NotFound)]
    [TestCase("/meta/databases/sample/tables/missing", HttpStatusCode.NotFound)]
    [TestCase("/data/databases/sample/tables/missing", HttpStatusCode.NotFound)]
    [TestCase("/meta/databases/sample/tables?lang=xx", HttpStatusCode.BadRequest)]
    [TestCase("/data/databases/sample/tables/flows?filters=UNKNOWN:code=x", HttpStatusCode.BadRequest)]
    public async Task InvalidRequests_ReturnErrors(string path, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await _client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(expected));
            Assert.That(body, Is.Not.Empty);
        }
    }
}
