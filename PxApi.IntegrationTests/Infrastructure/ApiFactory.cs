using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PxApi.IntegrationTests.Infrastructure;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string LogDirectory { get; } = CreateLogDirectory();

    private static string CreateLogDirectory()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PxApi.sln")))
            root = root.Parent;
        if (root is null)
            throw new DirectoryNotFoundException("Cannot locate the repository root for integration-test logs.");

        return Path.Combine(root.FullName, "agent_tmp", "sample-database-testing", Guid.NewGuid().ToString("N"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string contentRoot = AppContext.BaseDirectory;
        builder.UseEnvironment("testing");
        builder.UseContentRoot(contentRoot);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddJsonFile(Path.Combine(contentRoot, "appsettings.testing.json"));
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBases:0:Custom:RootPath"] = Path.Combine(contentRoot, "SampleDatabases"),
                ["LogOptions:Folder"] = LogDirectory
            });
        });
    }
}
