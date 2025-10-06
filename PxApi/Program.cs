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
                c.SwaggerDoc("openapi", new OpenApiInfo { Title = "PxApi", Version = "v1" });
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PxApi.xml"));
                c.UseOneOfForPolymorphism();
                c.SelectSubTypesUsing(baseType =>
                    typeof(Program).Assembly.GetTypes().Where(type => type.IsSubclassOf(baseType)));

                // Add API Key authentication to Swagger if configured
                if (AppSettings.Active.Authentication.IsEnabled)
                {
                    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                    {
                        Description = $"API Key needed to access the cache endpoints. Add the key in the '{AppSettings.Active.Authentication.Cache.HeaderName}' header.",
                        Type = SecuritySchemeType.ApiKey,
                        Name = AppSettings.Active.Authentication.Cache.HeaderName,
                        In = ParameterLocation.Header,
                        Scheme = "ApiKeyScheme"
                    });

                    OpenApiSecurityRequirement key = new()
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "ApiKey"
                                }
                            },
                            []
                        }
                    };
                    c.AddSecurityRequirement(key);
                }
                
                // Add the custom schema filter for DoubleDataValue to ensure it appears as number type in OpenAPI
                c.SchemaFilter<DoubleDataValueSchemaFilter>();
                
                // Add document filter to remove DoubleDataValue component schemas
                c.DocumentFilter<DoubleDataValueDocumentFilter>();
                
                // Add document filter to remove Filter subclass component schemas
                c.DocumentFilter<FilterSubclassDocumentFilter>();
                
                // Add schema filter for Filter types to document the custom JSON structure
                c.SchemaFilter<FilterSchemaFilter>();
                
                // Add document filter to enhance DataController POST endpoint documentation with request body examples
                c.DocumentFilter<DataControllerPostEndpointDocumentFilter>();
                
                // Add document filter to enhance DataController GET endpoint documentation with query parameter examples
                c.DocumentFilter<DataControllerGetEndpointDocumentFilter>();
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
