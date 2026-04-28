using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PxApi.Configuration;
using PxApi.Controllers;

namespace PxApi.OpenApi
{
    /// <summary>
    /// Convention that controls API explorer visibility for internal and search endpoints.
    /// Cache controller actions are always hidden from OpenAPI documentation,
    /// and search controller actions are hidden when the search feature flag is disabled.
    /// </summary>
    public class ApiExplorerConventionsFactory : IActionModelConvention
    {
        /// <summary>
        /// Applies the convention to hide controller actions from API explorer.
        /// </summary>
        /// <param name="action">The action model to apply the convention to.</param>
        public void Apply(ActionModel action)
        {
            // Controllers with ApiExplorerSettings(IgnoreApi = true) have been set to be ignored in Swagger
            // Setting shows as null in the API explorer
            if (action.ApiExplorer.IsVisible is null)
            {
                action.ApiExplorer.IsVisible = false;
                return;
            }

            if (action.Controller.ControllerType == typeof(CacheController))
            {
                action.ApiExplorer.IsVisible = false;
                return;
            }

            if (action.Controller.ControllerType == typeof(SearchController)
                && !AppSettings.Active.Features.SearchController)
            {
                action.ApiExplorer.IsVisible = false;
            }
        }
    }
}