namespace PxApi.Models
{
    /// <summary>
    /// Represents the application information response.
    /// </summary>
    /// <param name="Application">The name of the application.</param>
    /// <param name="Version">The version of the application.</param>
    public record InfoResponse(string Application, string Version);
}
