using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PxApi.Controllers;

namespace PxApi.OpenApi
{
    /// <summary>
    /// Class for setting visibility of API controllers in Swagger. 
    /// CacheController is always hidden from OpenAPI documentation since it's for internal use only.
    /// </summary>
    public class ApiExplorerConventionsFactory : IActionModelConvention
    {
        /// <summary>
        /// Applies the convention to hide controller actions from API explorer.
        /// CacheController is always hidden from OpenAPI documentation regardless of feature flag status.
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

            bool isCacheController = action.Controller.ControllerType == typeof(CacheController);
            if (isCacheController)
            {
                // Always hide CacheController from OpenAPI documentation since it's for internal use only
                action.ApiExplorer.IsVisible = false;
            }
        }
    }
}