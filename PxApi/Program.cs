using Microsoft.OpenApi.Models;
using PxApi.Configuration;
using PxApi.DataSources;

namespace PxApi
{
    public static class Program
    {
        public static void Main()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            // This enables calling AppSettings.Active to access the configuration.
            AppSettings.Load(configuration);

            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            // Add services to the container.
            AddServices(builder.Services);

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "{documentName}/document.json";
                });
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/openapi/document.json", "PxApi");
                    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddControllers();
            serviceCollection.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("openapi", new OpenApiInfo { Title = "PxApi", Version = "v1" });
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PxApi.xml"));
            });
            serviceCollection.AddTransient<IDataSource, LocalFileSystemDataSource>();
        }
    }
}
