
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
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddControllers();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            serviceCollection.AddOpenApi();

            serviceCollection.AddTransient<IDataSource, LocalFileSystemDataSource>();
        }
    }
}
