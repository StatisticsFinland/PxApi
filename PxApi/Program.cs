using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using OpenTelemetry.Logs;
using Microsoft.FeatureManagement;
using Microsoft.OpenApi;
using NLog;
using NLog.Web;
using NLog.Extensions.Logging;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.Exceptions;
using PxApi.Filters;
using PxApi.OpenApi;
using PxApi.OpenApi.DocumentFilters;
using PxApi.OpenApi.SchemaFilters;
using PxApi.Services;
using PxApi.Services.Search;
using PxApi.Utilities;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PxApi
{
    /// <summary>
    /// Main entry point of the application.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "No meaningful logic to test in Main.")]
    public sealed class Program
    {
        private Program() { }

        /// <summary>
        /// Main entry point of the application.
        /// </summary>
        public static async Task Main(string[] args)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try
            {
                using IHost host = CreateHostBuilder(args).Build();
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped program because of exception");
                Environment.ExitCode = 1;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Creates the application host, allowing tests to replace configuration before services are registered.
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging => logging.ClearProviders())
                .UseNLog(new NLogAspNetCoreOptions { CaptureEventId = EventIdCaptureType.None })
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .ConfigureServices((context, services) =>
                    {
                        AppSettings.Load(context.Configuration);
                        services.AddLogging(logging => AddApplicationInsights(logging, AppSettings.Active.ApplicationInsights));
                        AddServices(services);
                        CorsConfig corsConfig = AppSettings.Active.Cors;
                        if (corsConfig.HasAllowedOrigins)
                        {
                            services.AddCors(options => options.AddDefaultPolicy(policy =>
                                policy.WithOrigins([.. corsConfig.AllowedOrigins])
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()));
                        }
                    })
                    .Configure(ConfigureApplication));

        private static void ConfigureApplication(IApplicationBuilder app)
        {
            ConfigureStartupLogging(app.ApplicationServices);

            app.UseSwagger(c =>
            {
                c.RouteTemplate = "/{documentName}/document.json";
            });
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("openapi/document.json", "PxApi");
                c.RoutePrefix = string.Empty;
            });

            app.UseRouting();
            app.UseMiddleware<QueryCompletionMiddleware>();
            app.UseExceptionHandler("/error");
            app.UseHttpsRedirection();

            if (AppSettings.Active.Cors.HasAllowedOrigins)
            {
                app.UseCors();
            }

            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        internal static void ConfigureStartupLogging(IServiceProvider services)
        {
            IHostApplicationLifetime lifetime = services.GetRequiredService<IHostApplicationLifetime>();
            ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
            lifetime.ApplicationStarted.Register(() =>
            {
                IServer server = services.GetRequiredService<IServer>();
                foreach (string address in server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [])
                {
                    logger.LogInformation("Now listening on: {listening_url}", address);
                }

                IHostEnvironment environment = services.GetRequiredService<IHostEnvironment>();
                logger.LogInformation("Application started. Hosting environment: {environment}", environment.EnvironmentName);
            });
        }

        internal static void AddApplicationInsights(ILoggingBuilder logging, ApplicationInsightsConfig configuration)
        {
            if (!configuration.IsEnabled) return;

            logging.Services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = configuration.ConnectionString;
                options.SamplingRatio = configuration.SamplingRatio;
                options.EnableTraceBasedLogsSampler = configuration.EnableTraceBasedLogsSampler;
            });
            if (!configuration.IncludeAuditLogs)
                logging.AddFilter<OpenTelemetryLoggerProvider>(typeof(AuditLogService).FullName, Microsoft.Extensions.Logging.LogLevel.None);
            logging.Services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeScopes = true);
            logging.Services.Configure<LoggerFilterOptions>(options =>
            {
                LoggerFilterRule? defaultRule = options.Rules.FirstOrDefault(rule => rule.CategoryName is null &&
                    (rule.ProviderName == typeof(OpenTelemetryLoggerProvider).FullName || rule.ProviderName == nameof(OpenTelemetryLoggerProvider)));
                if (defaultRule is not null) options.Rules.Remove(defaultRule);
            });
        }

        [ExcludeFromCodeCoverage]
        internal static void AddServices(IServiceCollection serviceCollection)
        {
            // Add feature management first and API explorer conventions to control Swagger visibility
            serviceCollection.AddFeatureManagement();

            serviceCollection.AddControllers(options =>
            {
                options.Filters.Add<LoggingScopeActionFilter>();
                options.Filters.Add<OperationCanceledExceptionFilter>();
                options.Filters.Add<QueryResponseExecutionFilter>();
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

            // Configure API behavior to handle model validation errors through custom exception
            // that gets caught by the global error handler (ErrorController)
            serviceCollection.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    throw new InvalidModelException(context.ModelState, context.HttpContext.Request.Path);
                };
            });

            serviceCollection.AddSwaggerGen(c =>
            {
                OpenApiConfig openApiConfig = AppSettings.Active.OpenApi;
                OpenApiInfo apiInfo = new()
                {
                    Title = "PxApi",
                    Version = "v1",
                    Description = "API for querying PX statistical datasets, providing JSON-stat2.0 and CSV outputs with flexible dimension filtering (code, range, positional).",
                    Contact = new OpenApiContact
                    {
                        Name = openApiConfig.ContactName,
                        Url = openApiConfig.ContactUrl,
                        Email = openApiConfig.ContactEmail
                    },
                    License = string.IsNullOrWhiteSpace(openApiConfig.LicenseName) ? null : new OpenApiLicense
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

                // Remove orphaned ProblemDetails schema from components
                c.DocumentFilter<ProblemDetailsDocumentFilter>();

                // Global 500 response description for all operations (added via operation filter style hook)
                c.OperationFilter<UnhandledErrorResponseOperationFilter>();

                // Global 499 response for operations that accept a CancellationToken
                c.OperationFilter<ClientClosedRequestOperationFilter>();

                // Attribute-based operationId assignment
                c.OperationFilter<OperationIdOperationFilter>();
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

            // Register HttpContextAccessor and audit logging service
            serviceCollection.AddHttpContextAccessor();
            serviceCollection.AddSingleton<ICacheObserver, HttpCacheObserver>();
            serviceCollection.AddScoped<IAuditLogService, AuditLogService>();

            // Register search service: use Elasticsearch when the feature is enabled,
            // otherwise register a disabled stub so the controller can still be activated
            // (the FeatureGate filter will return 404 before any action method runs).
            if (AppSettings.Active.Features.SearchController)
            {
                SearchConfig searchConfig = AppSettings.Active.Search;

                if (string.IsNullOrWhiteSpace(searchConfig.CloudId) || string.IsNullOrWhiteSpace(searchConfig.IndexPrefix))
                {
                    throw new InvalidOperationException("CloudId and IndexPrefix must be configured when the SearchController feature is enabled.");
                }

                if (string.IsNullOrWhiteSpace(searchConfig.ApiKey))
                {
                    throw new InvalidOperationException("The SEARCH_API_KEY environment variable must be set when the SearchController feature is enabled.");
                }

                ElasticsearchClientSettings esSettings = new(searchConfig.CloudId, new ApiKey(searchConfig.ApiKey));

                if (!string.IsNullOrWhiteSpace(searchConfig.ProxyAddress))
                {
                    esSettings = esSettings.Proxy(new Uri(searchConfig.ProxyAddress));
                }

                ElasticsearchClient esClient = new(esSettings);
                serviceCollection.AddSingleton(esClient);
                serviceCollection.AddSingleton(searchConfig);
                serviceCollection.AddScoped<ISearchService, ElasticSearchService>();
            }
            else
            {
                serviceCollection.AddSingleton<ISearchService, DisabledSearchService>();
            }
        }
    }
}
