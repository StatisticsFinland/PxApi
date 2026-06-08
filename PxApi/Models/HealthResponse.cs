using System.Text.Json.Serialization;

namespace PxApi.Models
{
    /// <summary>
    /// String constants for health status values.
    /// </summary>
    public static class HealthStatus
    {
        /// <summary>
        /// Indicates the component is operating normally.
        /// </summary>
        public const string Healthy = "healthy";

        /// <summary>
        /// Indicates the component is not operating normally.
        /// </summary>
        public const string Unhealthy = "unhealthy";
    }

    /// <summary>
    /// Represents the overall health status of the application.
    /// </summary>
    /// <param name="Status">Overall health status: <see cref="HealthStatus.Healthy"/> or <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="Databases">Health status of each configured database connection.</param>
    public record HealthResponse(string Status, List<DatabaseHealthStatus> Databases)
    {
        /// <summary>
        /// Health status of the search backend. Null when the search feature is disabled.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SearchHealthStatus? Search { get; init; }
    }

    /// <summary>
    /// Represents the health status of a single database connection.
    /// </summary>
    /// <param name="Id">The database identifier.</param>
    /// <param name="Status">Connection status: <see cref="HealthStatus.Healthy"/> or <see cref="HealthStatus.Unhealthy"/>.</param>
    public record DatabaseHealthStatus(string Id, string Status);

    /// <summary>
    /// Represents the health status of the search backend.
    /// </summary>
    /// <param name="Status">Search backend status: <see cref="HealthStatus.Healthy"/> or <see cref="HealthStatus.Unhealthy"/>.</param>
    public record SearchHealthStatus(string Status);
}
