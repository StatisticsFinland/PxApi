using Microsoft.AspNetCore.Mvc.Filters;
using PxApi.Services;

namespace PxApi.Filters;

/// <summary>Marks result execution so lifecycle failures can be distinguished from action failures.</summary>
public sealed class QueryResponseExecutionFilter : IAlwaysRunResultFilter
{
    /// <inheritdoc />
    public void OnResultExecuting(ResultExecutingContext context)
    {
        QueryObservation? observation = QueryObservation.Get(context.HttpContext);
        if (observation is not null) observation.ResponseExecutionStarted = true;
    }

    /// <inheritdoc />
    public void OnResultExecuted(ResultExecutedContext context)
    {
        QueryObservation? observation = QueryObservation.Get(context.HttpContext);
        if (observation is not null && context.Exception is not null) observation.ResponseFailed = true;
    }
}