using PxApi.Utilities;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Moq;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Controllers;
using PxApi.Filters;
using PxApi.Models;
using PxApi.Services;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PxApi.UnitTests.Services;

[TestFixture]
public class QueryLoggingTransportTests
{
    [TestCase("own")]
    [TestCase("audit")]
    public void NLog_ProductionEnvelope_UsesLowerSnakeCase(string targetName)
    {
        using LogFactory logFactory = new() { ThrowConfigExceptions = true };
        XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "nlog.config"));
        XNamespace nlogNamespace = "http://www.nlog-project.org/schemas/NLog.xsd";
        document.Descendants(nlogNamespace + "rules").Remove();
        XmlLoggingConfiguration configuration = XmlLoggingConfiguration.CreateFromXmlString(document.ToString(), logFactory);
        FileTarget target = configuration.FindTargetByName<FileTarget>(targetName)
            ?? throw new InvalidOperationException($"NLog target '{targetName}' was not found.");
        LogEventInfo entry = new(NLog.LogLevel.Info, "PxApi.TransportTest", LoggerConsts.Query.CompletionEventName);

        using JsonDocument record = JsonDocument.Parse(target.Layout.Render(entry));
        string[] keys = [.. record.RootElement.EnumerateObject().Select(property => property.Name)];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(keys, Is.Not.Empty);
            Assert.That(keys, Is.All.Matches("^[a-z][a-z0-9]*(_[a-z0-9]+)*$"));
            if (targetName == "own") Assert.That(record.RootElement.GetProperty("level").GetString(), Is.EqualTo("info"));
        }
    }

    [Test]
    public void NLog_StructuredStateAndScopes_AreQueryableJsonProperties()
    {
        MemoryTarget target = new("capture") { Layout = new JsonLayout { IncludeEventProperties = true, IncludeScopeProperties = true } };
        LoggingConfiguration configuration = new();
        configuration.AddRuleForAllLevels(target);
        using LogFactory logFactory = new() { Configuration = configuration };
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(new NLogLoggerProvider(new NLogProviderOptions
        {
            IncludeScopes = true,
            CaptureEventId = EventIdCaptureType.None
        }, logFactory)));
        ILogger logger = factory.CreateLogger("PxApi.TransportTest");

        Emit(logger);
        logFactory.Flush();

        Assert.That(target.Logs, Has.Count.EqualTo(1));
        using JsonDocument record = JsonDocument.Parse(target.Logs[0]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.RootElement.GetProperty("synthetic_string").GetString(), Is.EqualTo("safe"));
            Assert.That(record.RootElement.GetProperty("synthetic_number").GetInt64(), Is.EqualTo(42));
            Assert.That(record.RootElement.GetProperty("synthetic_boolean").GetBoolean(), Is.True);
            Assert.That(record.RootElement.GetProperty("scope_marker").GetString(), Is.EqualTo("scope"));
            Assert.That(Guid.TryParse(record.RootElement.GetProperty("event_id").GetString(), out _), Is.True);
            Assert.That(JsonDocument.Parse(record.RootElement.GetProperty("synthetic_json").GetString()!).RootElement.GetArrayLength(), Is.EqualTo(2));
        }
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public async Task NLog_DatabaseCacheClear_PreservesDatabaseWithoutDuplicateProperties(bool fails)
    {
        using LogFactory logFactory = new() { ThrowConfigExceptions = true };
        XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "nlog.config"));
        XNamespace nlogNamespace = "http://www.nlog-project.org/schemas/NLog.xsd";
        document.Descendants(nlogNamespace + "rules").Remove();
        XmlLoggingConfiguration configuration = XmlLoggingConfiguration.CreateFromXmlString(document.ToString(), logFactory);
        FileTarget productionTarget = configuration.FindTargetByName<FileTarget>("own")
            ?? throw new InvalidOperationException("NLog application target was not found.");
        MemoryTarget target = new("capture") { Layout = productionTarget.Layout };
        configuration.AddRuleForAllLevels(target);
        logFactory.Configuration = configuration;
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(new NLogLoggerProvider(new NLogProviderOptions
        {
            IncludeScopes = true,
            CaptureEventId = EventIdCaptureType.None
        }, logFactory)));
        DataBaseRef database = DataBaseRef.Create("db");
        Mock<ICachedDataSource> source = new();
        source.Setup(instance => instance.GetDataBaseReference("db")).Returns(database);
        if (fails) source.Setup(instance => instance.ClearDatabaseCacheAsync(database)).ThrowsAsync(new IOException("Test cache failure."));
        else source.Setup(instance => instance.ClearDatabaseCacheAsync(database)).Returns(Task.CompletedTask);
        CacheController controller = new(source.Object, factory.CreateLogger<CacheController>());

        ActionResult response = await controller.ClearAllCacheAsync("db");
        logFactory.Flush();

        Assert.That(target.Logs, Has.Count.EqualTo(1));
        using JsonDocument record = JsonDocument.Parse(target.Logs.Single());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((ObjectResult)response).StatusCode, Is.EqualTo(fails ? 500 : 200));
            Assert.That(record.RootElement.EnumerateObject().Select(property => property.Name), Is.Unique);
            Assert.That(record.RootElement.GetProperty("database_id").GetString(), Is.EqualTo("db"));
            Assert.That(record.RootElement.GetProperty("level").GetString(), Is.EqualTo(fails ? "error" : "info"));
            if (fails) Assert.That(record.RootElement.GetProperty("exception").GetString(), Does.Contain("Test cache failure."));
        }
    }

    [Test]
    public async Task NLog_QueryCompletion_PreservesCorrelationWithoutDuplicateProperties()
    {
        using LogFactory logFactory = new() { ThrowConfigExceptions = true };
        XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "nlog.config"));
        XNamespace nlogNamespace = "http://www.nlog-project.org/schemas/NLog.xsd";
        document.Descendants(nlogNamespace + "rules").Remove();
        XmlLoggingConfiguration configuration = XmlLoggingConfiguration.CreateFromXmlString(document.ToString(), logFactory);
        FileTarget productionTarget = configuration.FindTargetByName<FileTarget>("own")
            ?? throw new InvalidOperationException("NLog application target was not found.");
        MemoryTarget target = new("capture") { Layout = productionTarget.Layout };
        configuration.AddRuleForAllLevels(target);
        logFactory.Configuration = configuration;
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug)
            .AddProvider(new NLogLoggerProvider(new NLogProviderOptions
            {
                IncludeScopes = true,
                CaptureEventId = EventIdCaptureType.None
            }, logFactory)));
        ILogger diagnosticLogger = factory.CreateLogger("PxApi.TransportTest");
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.RouteValues = new RouteValueDictionary { ["database"] = "db", ["table"] = "table" };
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("data/databases/{database}/tables/{table}"), 0,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Data" }), "data"));
        QueryCompletionMiddleware middleware = new(_ =>
        {
            diagnosticLogger.LogDebug("Storage diagnostic.");
            return Task.CompletedTask;
        }, factory.CreateLogger<QueryCompletionMiddleware>(), new ConfigurationBuilder().Build());

        await middleware.InvokeAsync(context);
        logFactory.Flush();

        Assert.That(target.Logs, Has.Count.EqualTo(2));
        using JsonDocument diagnostic = JsonDocument.Parse(target.Logs[0]);
        using JsonDocument completion = JsonDocument.Parse(target.Logs[1]);
        string[] diagnosticKeys = [.. diagnostic.RootElement.EnumerateObject().Select(property => property.Name)];
        string[] completionKeys = [.. completion.RootElement.EnumerateObject().Select(property => property.Name)];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnosticKeys, Is.Unique);
            Assert.That(completionKeys, Is.Unique);
            Assert.That(completion.RootElement.GetProperty("event_name").GetString(), Is.EqualTo("query_completed"));
            Assert.That(completion.RootElement.GetProperty("operation").GetString(), Is.EqualTo("data"));
            Assert.That(diagnostic.RootElement.GetProperty("operation").GetString(), Is.EqualTo("data"));
            Assert.That(completion.RootElement.GetProperty("request_id").GetString(),
                Is.EqualTo(diagnostic.RootElement.GetProperty("request_id").GetString()));
            Assert.That(Guid.TryParse(completion.RootElement.GetProperty("event_id").GetString(), out _), Is.True);
        }
    }

    [Test]
    public async Task ApplicationInsights_ProductionRegistration_PreservesStructuredStateAndScopes()
    {
        CaptureProcessor processor = new();
        await using WebApplication application = CreateTelemetryApplication(processor);
        ILoggerFactory factory = application.Services.GetRequiredService<ILoggerFactory>();
        ILogger logger = factory.CreateLogger("PxApi.TransportTest");

        Emit(logger);

        Assert.That(processor.Records, Has.Count.EqualTo(1));
        Dictionary<string, object?> record = processor.Records.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record["synthetic_string"]?.ToString(), Is.EqualTo("safe"));
            Assert.That(record["synthetic_number"]?.ToString(), Is.EqualTo("42"));
            Assert.That(record["synthetic_boolean"]?.ToString(), Is.EqualTo("True"));
            Assert.That(record["scope_marker"]?.ToString(), Is.EqualTo("scope"));
            Assert.That(Guid.TryParse(record["event_id"]?.ToString(), out _), Is.True);
            Assert.That(JsonDocument.Parse(record["synthetic_json"]!.ToString()!).RootElement.GetArrayLength(), Is.EqualTo(2));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ApplicationInsights_ProductionRegistration_RequiresAuditOptIn(bool includeAuditLogs)
    {
        CaptureProcessor processor = new();
        await using WebApplication application = CreateTelemetryApplication(processor, new()
        {
            ["ApplicationInsights:IncludeAuditLogs"] = includeAuditLogs.ToString()
        });
        ILoggerFactory factory = application.Services.GetRequiredService<ILoggerFactory>();

        Emit(factory.CreateLogger("PxApi.TransportTest"));
        Emit(factory.CreateLogger<AuditLogService>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processor.Records, Has.Count.EqualTo(includeAuditLogs ? 2 : 1));
            Assert.That(processor.Categories.Count(category => category == typeof(AuditLogService).FullName), Is.EqualTo(includeAuditLogs ? 1 : 0));
            Assert.That(processor.Categories.Count(category => category == "PxApi.TransportTest"), Is.EqualTo(1));
        }
    }

    [TestCase("Information", 1)]
    [TestCase("Warning", 0)]
    public async Task ApplicationInsights_ProductionRegistration_HonorsConfiguredThreshold(string minimumLevel, int expectedCompletions)
    {
        CaptureProcessor processor = new();
        await using WebApplication application = CreateTelemetryApplication(processor, new()
        {
            ["Logging:OpenTelemetry:LogLevel:Default"] = minimumLevel
        });
        ILogger logger = application.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PxApi.TransportTest");

        logger.LogDebug("Below configured threshold.");
        Emit(logger);
        logger.LogWarning("Above configured threshold.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processor.Records.Count(record => Equals(record.GetValueOrDefault("event_name"), "query_completed")), Is.EqualTo(expectedCompletions));
            Assert.That(processor.Records, Has.Count.EqualTo(expectedCompletions + 1));
        }
    }

    [Test]
    public async Task ApplicationInsights_ProductionRegistration_AppliesSamplingOptions()
    {
        CaptureProcessor processor = new();
        await using WebApplication application = CreateTelemetryApplication(processor, new()
        {
            ["ApplicationInsights:SamplingRatio"] = "0.25",
            ["ApplicationInsights:EnableTraceBasedLogsSampler"] = "true"
        });
        ApplicationInsightsServiceOptions options = application.Services.GetRequiredService<IOptions<ApplicationInsightsServiceOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.SamplingRatio, Is.EqualTo(0.25f));
            Assert.That(options.EnableTraceBasedLogsSampler, Is.True);
        }
    }

    [Test]
    public async Task ApplicationInsights_ProductionRegistration_WhenDisabled_DoesNotRegisterProvider()
    {
        CaptureProcessor processor = new();
        await using WebApplication application = CreateTelemetryApplication(processor, enabled: false);

        Emit(application.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PxApi.TransportTest"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(application.Services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>(), Is.Empty);
            Assert.That(processor.Records, Is.Empty);
        }
    }

    private static WebApplication CreateTelemetryApplication(CaptureProcessor processor,
        Dictionary<string, string?>? overrides = null, bool enabled = true)
    {
        Dictionary<string, string?> values = new()
        {
            ["ApplicationInsights:ConnectionString"] = enabled ? $"InstrumentationKey={Guid.NewGuid():D};IngestionEndpoint=http://127.0.0.1" : null,
            ["ApplicationInsights:SamplingRatio"] = "0.5",
            ["ApplicationInsights:EnableTraceBasedLogsSampler"] = "false",
            ["Logging:OpenTelemetry:LogLevel:Default"] = "Information"
        };
        foreach (KeyValuePair<string, string?> pair in overrides ?? []) values[pair.Key] = pair.Value;
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        ApplicationInsightsConfig configuration = new(builder.Configuration.GetSection("ApplicationInsights"), "TEST_AI_" + Guid.NewGuid().ToString("N"));
        Program.AddApplicationInsights(builder.Logging, configuration);
        builder.Services.Configure<OpenTelemetryLoggerOptions>(options => options.AddProcessor(processor));
        builder.Services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            options.DisableOfflineStorage = true;
            options.EnableLiveMetrics = false;
            options.Transport = new HttpClientTransport(new HttpClient(new LocalTelemetryHandler()));
        });
        return builder.Build();
    }

    private static void Emit(ILogger logger)
    {
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        observation.Set(LoggerConsts.Query.Fields.EventName, "query_completed");
        observation.Set(LoggerConsts.Query.Fields.SchemaVersion, 1);
        observation.Set(LoggerConsts.Query.Fields.EventId, Guid.NewGuid().ToString("N"));
        observation.Set("synthetic_string", "safe");
        observation.Set("synthetic_number", 42L);
        observation.Set("synthetic_boolean", true);
        observation.Set("synthetic_json", JsonSerializer.Serialize(new[] { "one", "two" }));
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object> { ["scope_marker"] = "scope" });
        logger.Log(LogLevel.Information, new EventId(1001, "query_completed"), observation.Complete(), null, static (_, _) => "query_completed");
    }

    private sealed class CaptureProcessor : BaseProcessor<LogRecord>
    {
        public List<Dictionary<string, object?>> Records { get; } = [];
        public List<string?> Categories { get; } = [];
        public override void OnEnd(LogRecord record)
        {
            Dictionary<string, object?> fields = record.Attributes?.ToDictionary() ?? [];
            record.ForEachScope((scope, destination) =>
            {
                foreach (KeyValuePair<string, object?> property in scope) destination[property.Key] = property.Value;
            }, fields);
            Records.Add(fields);
            Categories.Add(record.CategoryName);
        }
    }

    private sealed class LocalTelemetryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}