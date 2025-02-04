using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using PxApi.Configuration;
using PxApi.DataSources;
using System.Diagnostics.CodeAnalysis;

namespace PxApi
{
    /// <summary>
    /// Main entry point of the application.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point of the application.
        /// </summary>
        [ExcludeFromCodeCoverage] // Difficult to test Main method since the app.Run() method is blocking.
        public static void Main()
        {
            Logger logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
            try
            {
                logger.Debug("Main called and logger initialized.");

                IConfiguration configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .AddEnvironmentVariables()
                    .Build();

                // This enables calling AppSettings.Active to access the configuration.
                AppSettings.Load(configuration);

                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                // Add services to the container.
                AddServices(builder.Services);

                WebApplication app = builder.Build();

                // Configure the HTTP request pipeline.
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "/{documentName}/document.json";
                });
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/openapi/document.json", "PxApi");
                    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
                });

                app.UseExceptionHandler("/error");

                app.UseHttpsRedirection();

                app.UseAuthorization();

                app.MapControllers();

                app.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped program because of exception");
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        [ExcludeFromCodeCoverage] // Not worth it to make public for testing.
        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddControllers();
            serviceCollection.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("openapi", new OpenApiInfo { Title = "PxApi", Version = "v1" });
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PxApi.xml"));
                c.UseOneOfForPolymorphism();
                c.SelectSubTypesUsing(baseType =>
                    typeof(Program).Assembly.GetTypes().Where(type => type.IsSubclassOf(baseType)));
            });
            serviceCollection.AddMemoryCache();
            serviceCollection.AddTransient<IDataSource, LocalFileSystemDataSource>();
        }
    }
}
