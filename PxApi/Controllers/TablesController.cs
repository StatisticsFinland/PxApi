using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Px.Utils.Models.Metadata.Dimensions;

namespace PxApi.Controllers
{
    /// <summary>
    /// Provides endpoints for retrieving tables and their metadata from a specified database.
    /// </summary>
    /// <remarks>
    /// Supports pagination and optional language-based metadata retrieval. Tables are ordered by their PX file name (ascending). If the requested page exceeds the last page an empty list is returned.
    /// </remarks>
    [Route("tables")]
    [ApiController]
    public class TablesController(ICachedDataSource cachedConnector, ILogger<TablesController> logger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Returns a paged list of tables and their essential metadata for a database.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="lang">Optional language used to get the metadata, default is Finnish (fi).</param>
        /// <param name="page">Optional 1-based page number to retrieve, default value is 1.</param>
        /// <param name="pageSize">Optional number of items per page (1-100), default value is 50.</param>
        /// <returns>Paged list containing table listing items and paging information.</returns>
        /// <response code="200">Returns the table listing.</response>
        /// <response code="400">Invalid query parameter was provided (page &lt; 1, pageSize outside 1-100).</response>
        /// <response code="404">Database not found.</response>
        [HttpGet("{database}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<PagedTableList>> GetTablesAsync(
            [FromRoute] string database,
            [FromQuery] string lang = "fi",
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 50)
        {
            if (page < 1 || pageSize < 1) return BadRequest("Invalid paging values.");
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE;

            AppSettings settings = AppSettings.Active;
            try
            {
                DataBaseRef? dataBaseRef = cachedConnector.GetDataBaseReference(database);
                if (dataBaseRef is null) return NotFound("Database not found.");
                ImmutableSortedDictionary<string, PxFileRef> tableList = await cachedConnector.GetFileListCachedAsync(dataBaseRef.Value);
                PagedTableList pagedTableList = new()
                {
                    Tables = [],
                    PagingInfo = new PagingInfo
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = tableList.Count,
                    }
                };

                int startIndex = pageSize * (page - 1);
                int endExclusive = pageSize * page;
                for (int i = startIndex; i < endExclusive; i++)
                {
                    if (i >= tableList.Count) break;
                    KeyValuePair<string, PxFileRef> table = tableList.ElementAt(i);

                    try
                    {
                        try
                        {
                            IReadOnlyMatrixMetadata tableMeta = await cachedConnector.GetMetadataCachedAsync(table.Value);

                            Uri fileUri = settings.RootUrl
                                .AddRelativePath("meta", "json", database, table.Key)
                                .AddQueryParameters(("lang", lang));
                            pagedTableList.Tables.Add(BuildTableListingItemFromMeta(table.Key, lang, tableMeta, fileUri));
                        }
                        catch (Exception buildEx)
                        {
                            logger.LogWarning(buildEx, "Building metadata for table {Table} failed, constructing error list entry.", tableList.ElementAt(i).Key);
                            string id = (await cachedConnector.GetSingleStringValueAsync(PxFileConstants.TABLEID, table.Value))
                                .Trim('"', ' ', '\r', '\n', '\t');
                            pagedTableList.Tables.Add(BuildErrorTableListingItem(table.Key, id));
                        }
                    }
                    catch (Exception idReadEx)
                    {
                        pagedTableList.Tables.Add(BuildErrorTableListingItem(table.Key, table.Key));
                        logger.LogWarning(idReadEx, "Failed to get metadata for table: {Table}", tableList.ElementAt(i).Key);
                    }
                }

                return Ok(pagedTableList);
            }
            catch (DirectoryNotFoundException dnfe)
            {
                logger.LogInformation(dnfe, "Failed to get tables for database: {Database}", database);
                return NotFound();
            }
        }

        /// <summary>
        /// HEAD endpoint to validate existence of database and optional page parameters without returning body content.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <param name="page">Optional page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <response code="200">Resource exists.</response>
        /// <response code="400">Invalid paging parameters.</response>
        /// <response code="404">Database not found.</response>
        [HttpHead("{database}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public IActionResult HeadTablesAsync(string database, int page = 1, int pageSize = 50)
        {
            if (page < 1 || pageSize < 1 || pageSize > MAX_PAGE_SIZE) return BadRequest();
            DataBaseRef? dataBaseRef = cachedConnector.GetDataBaseReference(database);
            if (dataBaseRef is null) return NotFound();
            return Ok();
        }

        /// <summary>
        /// Returns allowed HTTP methods for the tables resource.
        /// </summary>
        /// <param name="database">Unique identifier of the database.</param>
        /// <response code="200">Returns allowed methods in the Allow header.</response>
        [HttpOptions("{database}")]
        [ProducesResponseType(200)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needs to match route signature.")]
        public IActionResult OptionsTables(string database)
        {
            Response.Headers.Allow = "GET,HEAD,OPTIONS";
            return Ok();
        }

        private static TableListingItem BuildTableListingItemFromMeta(string tableName, string lang, IReadOnlyMatrixMetadata meta, Uri uri)
        {
            TableStatus status = TableStatus.Current;
            string id = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, lang) ?? tableName;
            DateTime lastUpdated = DateTime.MinValue;
            if (meta.TryGetContentDimension(out ContentDimension? contDim))
            {
                lastUpdated = contDim.Values.Map(v => v.LastUpdated).Max();
            }
            else
            {
                status = TableStatus.Error;
            }

            return new TableListingItem
            {
                ID = id,
                Name = tableName,
                Status = status,
                Title = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang) ?? null,
                LastUpdated = lastUpdated,
                Links =
                [
                    new Link
                    {
                        Rel = "describedby",
                        Href = uri.ToString(),
                        Method = "GET"
                    }
                ]
            };
        }

        private static TableListingItem BuildErrorTableListingItem(string tableName, string id)
        {
            return new TableListingItem
            {
                ID = id,
                Name = tableName,
                Status = TableStatus.Error,
                Title = null,
                LastUpdated = null,
                Links = []
            };
        }
    }
}
