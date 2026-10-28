using PxApi.Utilities;
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using PxApi.Exceptions;
using PxApi.Services;

namespace PxApi.Filters;

/// <summary>Owns one completion event outside exception re-execution and response execution.</summary>
public sealed class QueryCompletionMiddleware(RequestDelegate next, ILogger<QueryCompletionMiddleware> logger, IConfiguration configuration)
{
    /// <summary>Observes tracked endpoints without inspecting or buffering response bodies.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (QueryObservation.Get(context) is not null)
        {
            await next(context);
            return;
        }
        Endpoint? endpoint = context.GetEndpoint();
        LoggerConsts.Query.Operation? operation = GetOperation(endpoint);
        if (operation is null)
        {
            long rejectionStarted = Stopwatch.GetTimestamp();
            await next(context);
            if (context.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType)
                CompleteRoutingRejection(context, rejectionStarted);
            return;
        }

        string requestId = Guid.NewGuid().ToString("N");
        QueryObservation observation = CreateObservation(context, endpoint, operation.Value, requestId);
        long started = Stopwatch.GetTimestamp();
        Exception? escaped = null;
        IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            [LoggerConsts.Query.Fields.RequestId] = requestId,
            [LoggerConsts.Query.Fields.Operation] = LoggerConsts.Query.Value(operation.Value)
        });
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            escaped = exception;
            throw;
        }
        finally
        {
            CompleteRequest(context, observation, operation.Value, started, escaped, scope);
        }
    }

    private static LoggerConsts.Query.Operation? GetOperation(Endpoint? endpoint)
        => endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerName switch
        {
            "Data" => LoggerConsts.Query.Operation.Data,
            "Metadata" => LoggerConsts.Query.Operation.Metadata,
            "Tables" => LoggerConsts.Query.Operation.Tables,
            "Search" => LoggerConsts.Query.Operation.Search,
            _ => null
        };

    private void CompleteRoutingRejection(HttpContext context, long started)
    {
        EndpointDataSource? endpoints = context.RequestServices.GetService<EndpointDataSource>();
        if (endpoints is null) return;
        foreach (RouteEndpoint endpoint in endpoints.Endpoints.OfType<RouteEndpoint>())
        {
            LoggerConsts.Query.Operation? operation = GetOperation(endpoint);
            HttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
            if (operation is null || methods is null ||
                !methods.HttpMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase) ||
                endpoint.RoutePattern.RawText is not string template) continue;

            TemplateMatcher matcher = new(TemplateParser.Parse(template), new RouteValueDictionary(endpoint.RoutePattern.Defaults));
            RouteValueDictionary values = new();
            if (!matcher.TryMatch(context.Request.Path, values)) continue;

            QueryObservation observation = CreateObservation(context, endpoint, operation.Value, Guid.NewGuid().ToString("N"), values);
            CompleteRequest(context, observation, operation.Value, started, null);
            return;
        }
    }

    private QueryObservation CreateObservation(HttpContext context, Endpoint? endpoint, LoggerConsts.Query.Operation operation, string requestId,
        RouteValueDictionary? routeValues = null)
    {
        routeValues ??= context.Request.RouteValues;
        QueryObservation observation = new(configuration);
        context.Features.Set(observation);
        observation.Set(LoggerConsts.Query.Fields.EventName, LoggerConsts.Query.CompletionEventName);
        observation.Set(LoggerConsts.Query.Fields.SchemaVersion, 1);
        observation.Set(LoggerConsts.Query.Fields.EventId, Guid.NewGuid().ToString("N"));
        observation.Set(LoggerConsts.Query.Fields.RequestId, requestId);
        observation.Set(LoggerConsts.Query.Fields.TraceId, Activity.Current?.TraceId.ToString());
        observation.Set(LoggerConsts.Query.Fields.SpanId, Activity.Current?.SpanId.ToString());
        observation.Set(LoggerConsts.Query.Fields.Operation, operation);
        observation.Set(LoggerConsts.Query.Fields.Method, context.Request.Method);
        observation.Set(LoggerConsts.Query.Fields.RouteTemplate, (endpoint as RouteEndpoint)?.RoutePattern.RawText);
        observation.Set(LoggerConsts.Query.Fields.RequestedDatabaseId, routeValues["database"]?.ToString());
        observation.Set(LoggerConsts.Query.Fields.RequestedTableId, routeValues["table"]?.ToString());
        observation.Set(LoggerConsts.Query.Fields.RequestedLanguage, context.Request.Query["lang"].FirstOrDefault());
        observation.Set(LoggerConsts.Query.Fields.RequestedFormat, RequestedFormat(context.Request.Headers.Accept.ToString()));
        return observation;
    }

    private void CompleteRequest(HttpContext context, QueryObservation observation, LoggerConsts.Query.Operation operation, long started, Exception? escaped,
        IDisposable? scope = null)
    {
        try
        {
            Exception? failure = escaped ?? context.Features.Get<IExceptionHandlerFeature>()?.Error;
            int status = context.Response.StatusCode;
            bool cancelled = failure is OperationCanceledException || context.RequestAborted.IsCancellationRequested || status == 499;
            bool responseFailed = observation.ResponseFailed || escaped is not null && observation.ResponseExecutionStarted;
            LoggerConsts.Query.Outcome outcome = Outcome(cancelled, escaped is not null || responseFailed, status);
            SetFailureReason(observation, failure, cancelled, responseFailed, context.RequestAborted.IsCancellationRequested);
            int completionStatus = status;
            if (escaped is not null && !context.Response.HasStarted) completionStatus = cancelled ? 499 : 500;
            observation.Set(LoggerConsts.Query.Fields.StatusCode, completionStatus);
            observation.Set(LoggerConsts.Query.Fields.Outcome, outcome);
            observation.Set(LoggerConsts.Query.Fields.DurationMs, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (outcome == LoggerConsts.Query.Outcome.Success && !HttpMethods.IsHead(context.Request.Method))
                observation.Set(LoggerConsts.Query.Fields.ReturnedCells, observation.PreparedCells);
            SetRejectionReason(observation, status, operation, failure);
            ReportEscapedFailure(observation, escaped, cancelled);
        }
        finally
        {
            scope?.Dispose();
        }
        logger.Log(LogLevel.Information, new EventId(1001, LoggerConsts.Query.CompletionEventName), observation.Complete(), null, static (_, _) => LoggerConsts.Query.CompletionEventName);
    }

    private void ReportEscapedFailure(QueryObservation observation, Exception? escaped, bool cancelled)
    {
        if (escaped is not null && !cancelled && !observation.ExceptionReported)
            logger.LogError(escaped, "Tracked request failed outside the error response handler.");
    }

    private static LoggerConsts.Query.RequestedFormat RequestedFormat(string accept)
    {
        if (accept.Length == 0) return LoggerConsts.Query.RequestedFormat.Default;
        if (accept.Length > 512) return LoggerConsts.Query.RequestedFormat.Other;
        if (accept.Contains("text/csv", StringComparison.OrdinalIgnoreCase)) return LoggerConsts.Query.RequestedFormat.Csv;
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)) return LoggerConsts.Query.RequestedFormat.Json;
        return accept.Contains("*/*", StringComparison.Ordinal) ? LoggerConsts.Query.RequestedFormat.Wildcard : LoggerConsts.Query.RequestedFormat.Other;
    }

    private static LoggerConsts.Query.Outcome Outcome(bool cancelled, bool failed, int status)
    {
        if (cancelled) return LoggerConsts.Query.Outcome.Cancelled;
        if (failed || status >= 500) return LoggerConsts.Query.Outcome.Failed;
        return status >= 400 ? LoggerConsts.Query.Outcome.Rejected : LoggerConsts.Query.Outcome.Success;
    }

    private static void SetFailureReason(QueryObservation observation, Exception? failure, bool cancelled, bool responseFailed, bool aborted)
    {
        if (cancelled) observation.Reject(aborted ? LoggerConsts.Query.ErrorCode.ClientDisconnected : LoggerConsts.Query.ErrorCode.CancellationUnknown);
        else if (failure is InvalidModelException) observation.Reject(LoggerConsts.Query.ErrorCode.InvalidModel);
        else if (failure is not null) observation.Reject(responseFailed ? LoggerConsts.Query.ErrorCode.SerializationFailed : LoggerConsts.Query.ErrorCode.Unknown);
    }

    private static void SetRejectionReason(QueryObservation observation, int status, LoggerConsts.Query.Operation operation, Exception? failure)
    {
        if (status < 400 || failure is not null) return;
        if (operation == LoggerConsts.Query.Operation.Search && status == 404 && !observation.ActionEntered)
            observation.Reject(LoggerConsts.Query.ErrorCode.FeatureDisabled);
        else SetDefaultReason(observation, status);
    }

    private static void SetDefaultReason(QueryObservation observation, int status)
    {
        if (observation.HasErrorCode) return;
        observation.Reject(status switch
        {
            400 => LoggerConsts.Query.ErrorCode.InvalidParameters, 401 => LoggerConsts.Query.ErrorCode.Unauthorized, 403 => LoggerConsts.Query.ErrorCode.Forbidden,
            404 => LoggerConsts.Query.ErrorCode.NotFound, 406 => LoggerConsts.Query.ErrorCode.UnsupportedFormat, 413 => LoggerConsts.Query.ErrorCode.TooManyCells,
            415 => LoggerConsts.Query.ErrorCode.UnsupportedContentType, 503 => LoggerConsts.Query.ErrorCode.Unavailable, _ => LoggerConsts.Query.ErrorCode.Unknown
        });
    }
}