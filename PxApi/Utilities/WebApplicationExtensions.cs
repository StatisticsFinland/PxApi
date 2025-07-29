using PxApi.Configuration;
using PxApi.DataSources;

namespace PxApi.Utilities
{
    /// <summary>
    /// Extension methods for WebApplication related to database connections.
    /// </summary>
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Validates all database connections and fails fast if any connection is invalid.
        /// </summary>
        /// <param name="app">The web application containing the service provider.</param>
        /// <returns>A task representing the asynchronous validation operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if any database connection fails.</exception>
        public static async Task ValidateDatabaseConnectionsAsync(this WebApplication app)
        {
            ILogger<object> logger = app.Services.GetRequiredService<ILogger<object>>();
            logger.LogInformation("Validating database connections...");
            
            foreach (DataBaseConfig dbConfig in AppSettings.Active.DataBases)
            {
                string dbId = dbConfig.Id;
                using (IServiceScope scope = app.Services.CreateScope())
                {
                    logger.LogInformation("Testing connection to database {DatabaseId}", dbId);
                    IDataBaseConnector connector = scope.ServiceProvider.GetRequiredKeyedService<IDataBaseConnector>(dbId);
                    
                    try
                    {
                        // Test connection by getting all files - if it returns any files, connection is working
                        string[] files = await connector.GetAllFilesAsync();
                        
                        if (files.Length == 0)
                        {
                            logger.LogWarning("Database {DatabaseId} connection successful but no files were found", dbId);
                        }
                        else
                        {
                            logger.LogInformation("Successfully connected to database {DatabaseId} and found {FileCount} files", dbId, files.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogCritical(ex, "Failed to connect to database {DatabaseId}", dbId);
                        throw new InvalidOperationException($"Failed to connect to database {dbId}. Application startup aborted.", ex);
                    }
                }
            }
            
            logger.LogInformation("All database connections validated successfully");
        }
    }
}