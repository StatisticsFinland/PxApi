using Microsoft.AspNetCore.Mvc.ApplicationModels;
using PxApi.Controllers;
using PxApi.OpenApi;
using PxApi.UnitTests.Utils;
using System.Reflection;

namespace PxApi.UnitTests.ConfigurationTests
{
    public class ApiExplorerConventionsFactoryTests
    {
        [Test]
        public void Apply_SetsIsVisibleFalse_WhenIsVisibleIsNull()
        {
            LoadConfigWithFeatures(cacheEnabled: true, searchEnabled: true);

            ControllerModel controllerModel = new(typeof(DataController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = null };
            MethodInfo methodInfo = typeof(DataController).GetMethod("ToString")!;
            ActionModel action = new(methodInfo, new List<Attribute>())
            {
                Controller = controllerModel,
                ApiExplorer = apiExplorerModel
            };

            ApiExplorerConventionsFactory factory = new();
            factory.Apply(action);

            Assert.That(action.ApiExplorer.IsVisible, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Apply_SetsIsVisibleFalse_ForCacheController_RegardlessOfFeatureFlag(bool cacheEnabled)
        {
            LoadConfigWithFeatures(cacheEnabled, searchEnabled: true);

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
        public void Apply_SetsIsVisibleFalse_WhenSearchControllerFeatureDisabled()
        {
            LoadConfigWithFeatures(cacheEnabled: true, searchEnabled: false);

            ControllerModel controllerModel = new(typeof(SearchController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = true };
            MethodInfo methodInfo = typeof(SearchController).GetMethod("ToString")!;
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
        public void Apply_KeepsIsVisibleTrue_WhenSearchControllerFeatureEnabled()
        {
            LoadConfigWithFeatures(cacheEnabled: true, searchEnabled: true);

            ControllerModel controllerModel = new(typeof(SearchController).GetTypeInfo(), []);
            ApiExplorerModel apiExplorerModel = new() { IsVisible = true };
            MethodInfo methodInfo = typeof(SearchController).GetMethod("ToString")!;
            ActionModel action = new(methodInfo, new List<Attribute>())
            {
                Controller = controllerModel,
                ApiExplorer = apiExplorerModel
            };

            ApiExplorerConventionsFactory factory = new();
            factory.Apply(action);

            Assert.That(action.ApiExplorer.IsVisible, Is.True);
        }

        [Test]
        public void Apply_DoesNotChangeIsVisible_ForControllersWithoutFeatureGate()
        {
            LoadConfigWithFeatures(cacheEnabled: false, searchEnabled: false);

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

        private static void LoadConfigWithFeatures(bool cacheEnabled, bool searchEnabled)
        {
            Dictionary<string, string?> configData = TestConfigFactory.Merge(
                TestConfigFactory.Base(),
                TestConfigFactory.MountedDb(0, "TestDb", "datasource/root/"),
                new Dictionary<string, string?>
                {
                    ["FeatureManagement:CacheController"] = cacheEnabled.ToString(),
                    ["FeatureManagement:SearchController"] = searchEnabled.ToString()
                }
            );
            TestConfigFactory.BuildAndLoad(configData);
        }
    }
}