namespace PxApi.Models
{
    /// <summary>
    /// Represents the overall health status of the application.
    /// </summary>
    /// <param name="Status">Overall health status: "healthy" or "unhealthy".</param>
    /// <param name="Databases">Health status of each configured database connection.</param>
    public record HealthResponse(string Status, List<DatabaseHealthStatus> Databases);

    /// <summary>
    /// Represents the health status of a single database connection.
    /// </summary>
    /// <param name="Id">The database identifier.</param>
    /// <param name="Status">Connection status: "healthy" or "unhealthy".</param>
    public record DatabaseHealthStatus(string Id, string Status);
}
