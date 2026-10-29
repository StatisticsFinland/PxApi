using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PxApi.UnitTests.Utils;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PxApi.UnitTests.OpenApi;

[TestFixture]
[NonParallelizable]
public class OpenApiMetadataTests
{
    [TearDown]
    public void ResetSettings() => TestConfigFactory.BuildAndLoad(TestConfigFactory.Base());

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase(null, "https://example.invalid/license")]
    public void License_WithoutName_IsOmitted(string? name, string? url)
    {
        OpenApiInfo info = ConfigureDocument(name, url);

        Assert.That(info.License, Is.Null);
    }

    [TestCase(null)]
    [TestCase("https://example.invalid/license")]
    public void License_WithName_PreservesConfiguredMetadata(string? url)
    {
        OpenApiInfo info = ConfigureDocument("Sample license", url);

        Assert.That(info.License, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(info.License.Name, Is.EqualTo("Sample license"));
            Assert.That(info.License.Url, Is.EqualTo(url is null ? null : new Uri(url)));
        }
    }

    private static OpenApiInfo ConfigureDocument(string? name, string? url)
    {
        Dictionary<string, string?> settings = TestConfigFactory.Base();
        settings["OpenApi:LicenseName"] = name;
        settings["OpenApi:LicenseUrl"] = url;
        TestConfigFactory.BuildAndLoad(settings);
        ServiceCollection services = new();
        Program.AddServices(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        SwaggerGenOptions options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        return options.SwaggerGeneratorOptions.SwaggerDocs["openapi"];
    }
}
