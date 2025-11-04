using Microsoft.AspNetCore.Mvc;
using Px.Utils.Language;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;
using PxApi.OpenApi;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides REST endpoints for discovering available PX databases. The base route for this controller is /databases.
    /// </summary>
    /// <remarks>
    /// Current endpoints:
    /// <list type="bullet">
    /// <item>GET /databases Lists all databases including localized name, optional description, table count and a link to list tables in the database.</item>
    /// <item>HEAD /databases Returns only headers to indicate the database collection resource exists (useful for health checks / pre-flight).</item>
    /// <item>OPTIONS /databases Returns allowed HTTP methods in the Allow response header.</item>
    /// </list>
    /// Content negotiation: Only application/json is produced.
    /// </remarks>
    [Route("databases")]
    [ApiController]
    public class DatabasesController(ICachedDataSource dataSource) : ControllerBase
    {
        /// <summary>
        /// Retrieves a list of available databases. Each item contains its identifier, localized name, optional localized description, table count and HATEOAS link to the tables listing endpoint.
        /// </summary>
        /// <param name="lang">Optional language code used to resolve name and description (defaults to fi when omitted).</param>
        /// <returns>A list of <see cref="DataBaseListingItem"/> objects describing each available database.</returns>
        /// <response code="200">Successful retrieval of database listing.</response>
        /// <response code="400">Requested language not supported.</response>
        [HttpGet]
        [OperationId("listDatabases")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<DataBaseListingItem>),200, "application/json")]
        [ProducesResponseType(400)]
        public async Task<ActionResult<List<DataBaseListingItem>>> GetDatabases([FromQuery] string? lang = null)
        {
            AppSettings settings = AppSettings.Active;
            string actualLang = lang ?? settings.Localization.DefaultLanguage;
            if (!settings.Localization.SupportedLanguages.Contains(actualLang))
            {
                return BadRequest("The requested language is not supported.");
            }
            IReadOnlyCollection<DataBaseRef> dbRefs = dataSource.GetAllDataBaseReferences();
            List<DataBaseListingItem> result = [];
            foreach (DataBaseRef dbRef in dbRefs)
            {
                // Resolve localized name from alias files via cached datasource; fallback to id if missing / error
                MultilanguageString nameMulti = await dataSource.GetDatabaseNameAsync(dbRef, string.Empty);

                // Description still resolved from configuration custom values (Description.<lang>)
                DataBaseConfig? config = settings.DataBases.FirstOrDefault(c => c.Id == dbRef.Id);
                string descKey = $"Description.{actualLang}";
                string? description = config?.Custom.GetValueOrDefault(descKey);

                Task<ImmutableSortedDictionary<string, PxFileRef>> filesTask = dataSource.GetFileListCachedAsync(dbRef);
                ImmutableSortedDictionary<string, PxFileRef> files = await filesTask;
                int tableCount = files.Count;
                Uri tablesUri = settings.RootUrl.AddRelativePath("tables", dbRef.Id).AddQueryParameters(("lang", actualLang));

                // Determine available languages (intersection between name translations and supported languages)
                IEnumerable<string> languages = nameMulti.Languages.Intersect(settings.Localization.SupportedLanguages);

                DataBaseListingItem item = new()
                {
                    ID = dbRef.Id,
                    Name = nameMulti[actualLang],
                    Description = description,
                    TableCount = tableCount,
                    AvailableLanguages = [..languages],
                    Links = [
                        new Link
                        {
                            Rel = "describedby",
                            Href = tablesUri.ToString(),
                            Method = "GET"
                        }
                    ]
                };
                result.Add(item);
            }
            return Ok(result);
        }

        /// <summary>
        /// HEAD endpoint for the database collection. Returns only headers to indicate the resource exists.
        /// </summary>
        /// <returns><see cref="OkResult"/> indicating the collection resource exists.</returns>
        /// <response code="200">Resource exists.</response>
        [HttpHead]
        [OperationId("headDatabases")]
        [ProducesResponseType(200)]
        public IActionResult HeadDatabases() => Ok();

        /// <summary>
        /// Returns allowed HTTP methods for the databases resource in the Allow response header.
        /// </summary>
        /// <returns><see cref="OkResult"/> with Allow header populated.</returns>
        /// <response code="200">Allowed methods returned.</response>
        [HttpOptions]
        [OperationId("optionsDatabases")]
        [ProducesResponseType(200)]
        public IActionResult OptionsDatabases()
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            return Ok();
        }
    }
}
