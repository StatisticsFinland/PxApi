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
    /// <remarks>This controller is responsible for handling requests related to table listings in a database.
    /// It supports pagination and optional language-based metadata retrieval.</remarks>
    [Route("tables")]
    [ApiController]
    public class TablesController(ICachedDataSource cachedConnector, ILogger<TablesController> logger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// List of tables and their essential metadata in a database.
        /// </summary>
        /// <param name="databaseId">Unique identifier of the database.</param>
        /// <param name="lang">[Optional] Language used to get the metadata, default is Finnish (fi).</param>
        /// <param name="page">[Optional] Ordinal number of the page to get, default value is 1.</param>
        /// <param name="pageSize">[Optional] Number of items per page, minimum value is 1 and maximum value is 100, default value is 50.</param>
        /// <returns>Object containing the table listing and paging information.</returns>
        /// <response code="200">Returns the table listing.</response>
        /// <response code="400">Invalid query parameter was provided.</response>
        /// <response code="404">If database is not found.</response>
        [HttpGet("{databaseId}")]
        [Produces("application/json")] 
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<PagedTableList>> GetTablesAsync(
            [FromRoute] string databaseId,
            [FromQuery] string lang = "fi",
            [FromQuery][Range(1, int.MaxValue)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 50)
        {
            if (page < 1 || pageSize < 1) return BadRequest();
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE; 

            AppSettings settings = AppSettings.Active;
            try
            {
                DataBaseRef? dataBase = cachedConnector.GetDataBaseReference(databaseId);
                if (dataBase is null) return NotFound("Database not found.");
                ImmutableSortedDictionary<string, PxFileRef> tableList = await cachedConnector.GetFileListCachedAsync(dataBase.Value);
                PagedTableList pagedTableList = new()
                {
                    Tables = [],
                    PagingInfo = new PagingInfo()
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = tableList.Count,
                    }
                };

                for (int i = pageSize * (page - 1); i < pageSize * page; i++)
                {
                    if (i >= tableList.Count) break;
                    KeyValuePair<string, PxFileRef> table = tableList.ElementAt(i);

                    try
                    {
                        try
                        {
                            IReadOnlyMatrixMetadata tableMeta = await cachedConnector.GetMetadataCachedAsync(table.Value);

                            Uri fileUri = settings.RootUrl
                                .AddRelativePath("meta", databaseId, table.Key)
                                .AddQueryParameters(("lang", lang));
                            pagedTableList.Tables.Add( BuildTableListingItemFromMeta(table.Key, lang, tableMeta, fileUri));
                        }
                        catch (Exception buildEx) // If the metaobject build failed, try to get the table ID from the table itself
                        {
                            logger.LogWarning(buildEx, "Building the structured metadata object for table {Table} failed, constructing error list entry.", tableList.ElementAt(i).Key);
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
                logger.LogInformation(dnfe, "Failed to get tables for database: {Database}", databaseId);
                return NotFound();
            }
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

            return new TableListingItem()
            {
                ID = id,
                Name = tableName,
                Status = status,
                Title = meta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang) ?? null,
                LastUpdated = lastUpdated,
                Links =
                [
                    new()
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
            return new TableListingItem()
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
