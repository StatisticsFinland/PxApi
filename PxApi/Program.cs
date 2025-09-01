using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

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
        public static async Task Main()
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

                // Validate database connections before starting the application
                logger.Info("Validating database connections before starting application");
                await app.ValidateDatabaseConnectionsAsync();
                logger.Info("All database connections are valid");

                // Configure the HTTP request pipeline.
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "/{documentName}/document.json";
                });
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("openapi/document.json", "PxApi");
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
                // Make sure to exit with non-zero code to indicate failure
                Environment.ExitCode = 1;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        [ExcludeFromCodeCoverage] // Not worth it to make public for testing.
        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = GlobalJsonConverterOptions.Default.PropertyNamingPolicy;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = GlobalJsonConverterOptions.Default.PropertyNameCaseInsensitive;
                options.JsonSerializerOptions.AllowTrailingCommas = GlobalJsonConverterOptions.Default.AllowTrailingCommas;
                options.JsonSerializerOptions.Encoder = GlobalJsonConverterOptions.Default.Encoder;
                
                // Copy all converters from the global options
                foreach (JsonConverter converter in GlobalJsonConverterOptions.Default.Converters)
                {
                    options.JsonSerializerOptions.Converters.Add(converter);
                }
            });
            serviceCollection.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("openapi", new OpenApiInfo { Title = "PxApi", Version = "v1" });
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PxApi.xml"));
                c.UseOneOfForPolymorphism();
                c.SelectSubTypesUsing(baseType =>
                    typeof(Program).Assembly.GetTypes().Where(type => type.IsSubclassOf(baseType)));
            });
            serviceCollection.AddMemoryCache();
            
            // Register database connectors as keyed services
            serviceCollection.AddDataBaseConnectors();

            serviceCollection.AddSingleton<DatabaseCache>();
            serviceCollection.AddScoped<ICachedDataSource, CachedDataSource>();

            // Register the database connector factory
            serviceCollection.AddScoped<IDataBaseConnectorFactory, DataBaseConnectorFactoryImpl>();
        }
    }
}
