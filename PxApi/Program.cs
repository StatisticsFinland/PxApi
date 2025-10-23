using Microsoft.FeatureManagement;
using Microsoft.OpenApi.Models;
using NLog.Web;
using NLog;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using PxApi.OpenApi.DocumentFilters;
using PxApi.OpenApi.SchemaFilters;
using PxApi.OpenApi;

namespace PxApi
{
    /// <summary>
    /// Main entry point of the application.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "No meaningful logic to test in Main.")]
    public static class Program
    {
        /// <summary>
        /// Main entry point of the application.
        /// </summary>
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
                    app.Logger.LogInformation("Swagger UI configured with server {RootUrl}", AppSettings.Active.RootUrl);
                    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
                });

                app.UseExceptionHandler("/error");

                app.UseHttpsRedirection();

                app.UseAuthorization();

                app.MapControllers();

                await app.RunAsync();
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
            // Add feature management first and API explorer conventions to control Swagger visibility
            serviceCollection.AddFeatureManagement();
            
            serviceCollection.AddControllers(options =>
            {
                options.Conventions.Add(new ApiExplorerConventionsFactory());
            })
            .AddJsonOptions(options =>
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
                OpenApiConfig openApiConfig = AppSettings.Active.OpenApi;
                OpenApiInfo apiInfo = new()
                {
                    Title = "PxApi",
                    Version = "v1",
                    Description = "API for querying PX statistical datasets, providing JSON-stat 2.0 and CSV outputs with flexible dimension filtering (code, range, positional).",
                    Contact = new OpenApiContact
                    {
                        Name = openApiConfig.ContactName,
                        Url = openApiConfig.ContactUrl,
                        Email = openApiConfig.ContactEmail
                    },
                    License = new OpenApiLicense
                    {
                        Name = openApiConfig.LicenseName,
                        Url = openApiConfig.LicenseUrl
                    }
                };
                c.SwaggerDoc("openapi", apiInfo);
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PxApi.xml"));
                c.UseOneOfForPolymorphism();
                c.SelectSubTypesUsing(baseType => typeof(Program).Assembly.GetTypes().Where(type => type.IsSubclassOf(baseType)));

                // Add document filter to add JsonStat2 component schema
                c.DocumentFilter<JsonStat2ComponentDocumentFilter>();

                // Add the custom schema filter for DoubleDataValue to ensure it appears as number type in OpenAPI
                c.SchemaFilter<DoubleDataValueSchemaFilter>();
                
                // Add document filter to remove DoubleDataValue component schemas
                // c.DocumentFilter<DoubleDataValueDocumentFilter>(); // merged into DataValueDocumentFilter

                // Add document filter to remove DataValueType and DoubleDataValue component schemas
                c.DocumentFilter<DataValueDocumentFilter>();

                // Add document filter to remove Filter subclass component schemas
                c.DocumentFilter<FilterSubclassDocumentFilter>();
                
                // Add schema filter for Filter types to document the custom JSON structure
                c.SchemaFilter<FilterSchemaFilter>();
                
                // Add document filter to enhance DataController POST endpoint documentation with request body examples
                c.DocumentFilter<DataControllerPostEndpointDocumentFilter>();
                
                // Add document filter to enhance DataController GET endpoint documentation with query parameter examples
                c.DocumentFilter<DataControllerGetEndpointDocumentFilter>();

                // Add document filter to inject example response for DatabasesController GET endpoint
                c.DocumentFilter<DatabasesControllerGetEndpointDocumentFilter>();

                // Add document filter to inject the servers list from configuration
                c.DocumentFilter<RootUrlServersDocumentFilter>();

                // Add document filter to enforce no-auth documentation (ensures no security schemes appear)
                c.DocumentFilter<NoAuthDocumentFilter>();

                // Remove bodies from all HEAD responses
                c.DocumentFilter<HeadResponsesNoBodyDocumentFilter>();

                // Global 500 response description for all operations (added via operation filter style hook)
                c.OperationFilter<UnhandledErrorResponseOperationFilter>();
            });
            
            // Configure MemoryCache with global cache size limit
            serviceCollection.AddMemoryCache(options =>
            {
                options.SizeLimit = AppSettings.Active.Cache.MaxSizeBytes;
            });
            
            // Register database connectors as keyed services
            serviceCollection.AddDataBaseConnectors();

            serviceCollection.AddSingleton<DatabaseCache>();
            serviceCollection.AddScoped<ICachedDataSource, CachedDataSource>();

            // Register the database connector factory
            serviceCollection.AddScoped<IDataBaseConnectorFactory, DataBaseConnectorFactoryImpl>();
        }
    }
}
