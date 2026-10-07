using PxApi.Utilities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PxApi.Filters;
using PxApi.Services;

namespace PxApi.UnitTests.Filters;

[TestFixture]
public class QueryCompletionMiddlewareTests
{
    private readonly IConfiguration configuration = new ConfigurationBuilder().Build();

    [TestCase(200, "success")]
    [TestCase(400, "rejected")]
    [TestCase(401, "rejected")]
    [TestCase(404, "rejected")]
    [TestCase(406, "rejected")]
    [TestCase(413, "rejected")]
    [TestCase(415, "rejected")]
    [TestCase(503, "failed")]
    [TestCase(499, "cancelled")]
    public async Task InvokeAsync_EmitsOneStructuredCompletion(int status, string outcome)
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        HttpContext context = CreateContext();
        QueryCompletionMiddleware middleware = new(http =>
        {
            http.Response.StatusCode = status;
            QueryObservation.Get(http)!.PreparedCells = 7;
            return Task.CompletedTask;
        }, logger.Object, configuration);

        await middleware.InvokeAsync(context);

        Assert.That(events, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(events[0]["event_name"], Is.EqualTo("query_completed"));
            Assert.That(events[0]["schema_version"], Is.EqualTo(1));
            Assert.That(events[0]["outcome"], Is.EqualTo(outcome));
            Assert.That(events[0]["status_code"], Is.EqualTo(status));
            Assert.That(events[0]["duration_ms"], Is.TypeOf<double>());
            Assert.That(events[0].ContainsKey("returned_cells"), Is.EqualTo(status == 200));
        }
    }

    [Test]
    public async Task InvokeAsync_Reexecution_PreservesOriginalOperationAndEmitsOnce()
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        HttpContext context = CreateContext();
        QueryCompletionMiddleware inner = new(http => Task.CompletedTask, logger.Object, configuration);
        QueryCompletionMiddleware outer = new(async http =>
        {
            http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Error" }), "error"));
            http.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = new InvalidOperationException() });
            http.Response.StatusCode = 500;
            await inner.InvokeAsync(http);
        }, logger.Object, configuration);

        await outer.InvokeAsync(context);

        Assert.That(events, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(events[0]["operation"], Is.EqualTo("data"));
            Assert.That(events[0]["outcome"], Is.EqualTo("failed"));
            Assert.That(events[0]["route_template"], Is.EqualTo("data/databases/{database}/tables/{table}"));
        }
    }

    [Test]
    public void InvokeAsync_ResponseFailure_DoesNotPublishPreparedCells()
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        HttpContext context = CreateContext();
        QueryCompletionMiddleware middleware = new(http =>
        {
            QueryObservation.Get(http)!.PreparedCells = 7;
            QueryObservation.Get(http)!.ResponseExecutionStarted = true;
            throw new IOException("write failed");
        }, logger.Object, configuration);

        Assert.ThrowsAsync<IOException>(() => middleware.InvokeAsync(context));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0]["outcome"], Is.EqualTo("failed"));
            Assert.That(events[0]["error_code"], Is.EqualTo("serialization_failed"));
            Assert.That(events[0].ContainsKey("returned_cells"), Is.False);
        }
    }

    [TestCase("HEAD")]
    [TestCase("OPTIONS")]
    public async Task InvokeAsync_Probes_RetainMethod(string method)
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        HttpContext context = CreateContext();
        context.Request.Method = method;
        QueryCompletionMiddleware middleware = new(_ => Task.CompletedTask, logger.Object, configuration);

        await middleware.InvokeAsync(context);

        Assert.That(events.Single()["method"], Is.EqualTo(method));
    }

    [Test]
    public async Task InvokeAsync_UntrackedEndpoint_EmitsNothing()
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        QueryCompletionMiddleware middleware = new(_ => Task.CompletedTask, logger.Object, configuration);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.That(events, Is.Empty);
    }

    [Test]
    public async Task InvokeAsync_ParallelRequests_DoNotShareStateOrIdentifiers()
    {
        List<Dictionary<string, object?>> events = [];
        Mock<ILogger<QueryCompletionMiddleware>> logger = CreateLogger(events);
        QueryCompletionMiddleware middleware = new(async http =>
        {
            QueryObservation.Get(http)!.Set(LoggerConsts.Query.Fields.TableId, http.Request.RouteValues["table"]);
            await Task.Yield();
        }, logger.Object, configuration);
        HttpContext first = CreateContext();
        HttpContext second = CreateContext();
        second.Request.RouteValues["table"] = "second";

        await Task.WhenAll(middleware.InvokeAsync(first), middleware.InvokeAsync(second));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(events.Select(entry => entry["request_id"]).Distinct().Count(), Is.EqualTo(2));
            Assert.That(events.Select(entry => entry["table_id"]), Is.EquivalentTo(new[] { "table", "second" }));
        }
    }

    private static HttpContext CreateContext()
    {
        DefaultHttpContext context = new();
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("data/databases/{database}/tables/{table}"), 0,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Data" }), "data"));
        context.Request.Method = "GET";
        context.Request.RouteValues["database"] = "db";
        context.Request.RouteValues["table"] = "table";
        return context;
    }

    private static Mock<ILogger<QueryCompletionMiddleware>> CreateLogger(List<Dictionary<string, object?>> events)
    {
        Mock<ILogger<QueryCompletionMiddleware>> logger = new();
        logger.Setup(instance => instance.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                if (((EventId)invocation.Arguments[1]).Name != "query_completed") return;
                IEnumerable<KeyValuePair<string, object?>> state = (IEnumerable<KeyValuePair<string, object?>>)invocation.Arguments[2];
                lock (events) events.Add(state.ToDictionary());
            }));
        return logger;
    }
}