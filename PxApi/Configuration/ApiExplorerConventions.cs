using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PxApi.Controllers;

namespace PxApi.Configuration
{
    /// <summary>
    /// Class for setting visibility of API controllers in Swagger. 
    /// CacheController is only visible if the CacheController feature is enabled.
    /// </summary>
    public class ApiExplorerConventionsFactory : IActionModelConvention
    {
        /// <summary>
        /// Applies the convention to hide controller actions from API explorer when their feature is disabled.
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
                // Check the feature flag from AppSettings
                bool isCacheControllerEnabled = AppSettings.Active.Features.CacheController;
                action.ApiExplorer.IsVisible = isCacheControllerEnabled;
            }
        }
    }
}