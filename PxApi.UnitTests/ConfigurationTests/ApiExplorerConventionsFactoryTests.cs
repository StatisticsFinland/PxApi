using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PxApi.Configuration;
using PxApi.Controllers;
using System.Reflection;

namespace PxApi.UnitTests.Configuration
{
    public class ApiExplorerConventionsFactoryTests
    {
        [Test]
        public void Apply_SetsIsVisibleFalse_WhenIsVisibleIsNull()
        {
            ControllerModel controllerModel = new(typeof(DataController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = null };
            MethodInfo methodInfo = typeof(DataController).GetMethod("ToString")!;
            ActionModel action = new(methodInfo, new List<Attribute>())
            {
                Controller = controllerModel,
                ApiExplorer = apiExplorerModel
            };

            ApiExplorerConventionsFactory factory = new ();
            factory.Apply(action);

            Assert.That(action.ApiExplorer.IsVisible, Is.False);
        }

        [Test]
        public void Apply_SetsIsVisibleFalse_ForCacheController()
        {
            ControllerModel controllerModel = new(typeof(CacheController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = true };
            MethodInfo methodInfo = typeof(CacheController).GetMethod("ToString")!;
            ActionModel action = new(methodInfo, new List<Attribute>())
            {
                Controller = controllerModel,
                ApiExplorer = apiExplorerModel
            };

            ApiExplorerConventionsFactory factory = new();
            factory.Apply(action);

            Assert.That(action.ApiExplorer.IsVisible, Is.False);
        }

        [Test]
        public void Apply_DoesNotChangeIsVisible_ForOtherControllers()
        {
            ControllerModel controllerModel = new(typeof(DataController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = true };
            MethodInfo methodInfo = typeof(DataController).GetMethod("ToString")!;
            ActionModel action = new(methodInfo, new List<Attribute>())
            {
                Controller = controllerModel,
                ApiExplorer = apiExplorerModel
            };

            ApiExplorerConventionsFactory factory = new();
            factory.Apply(action);

            Assert.That(action.ApiExplorer.IsVisible, Is.True);
        }
    }
}