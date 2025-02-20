using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata.ExtensionMethods;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using System.Collections.Immutable;

namespace PxApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TablesController(IDataSource dataSource, ILogger<TablesController> logger) : ControllerBase
    {
        private const int MAX_PAGE_SIZE = 100;

        [HttpGet("{dbId}")]
        [Produces("application/json")] 
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<PagedTableList>> GetTablesAsync([FromRoute] string dbId, [FromQuery] string lang = "fi", [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1 || pageSize < 1) return BadRequest();
            if (pageSize > MAX_PAGE_SIZE) pageSize = MAX_PAGE_SIZE; 

            AppSettings settings = AppSettings.Active;
            ImmutableSortedDictionary<string, PxTable> tableList = await dataSource.GetSortedTableDictCachedAsync(dbId);
            PagedTableList pagedTableList = new()
            {
                Tables = [],
                PagingInfo = new PagingInfo()
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = tableList.Count,
                    MaxPageSize = MAX_PAGE_SIZE
                }
            };

            for (int i = pageSize * (page - 1); i < pageSize * page; i++)
            {
                try
                {
                    if (i >= tableList.Count) break;
                    KeyValuePair<string, PxTable> table = tableList.ElementAt(i);
                    IReadOnlyMatrixMetadata tableMeta = await dataSource.GetMatrixMetadataCachedAsync(table.Value);

                    Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", dbId, table.Key)
                        .AddQueryParameters(("lang", lang));

                    pagedTableList.Tables.Add(new TableListingItem()
                    {
                        ID = tableMeta.AdditionalProperties.GetValueByLanguage(PxFileConstants.TABLEID, lang) ?? table.Key,
                        Name = table.Key,
                        Title = tableMeta.AdditionalProperties.GetValueByLanguage(PxFileConstants.DESCRIPTION, lang) ?? "Description not found",
                        LastUpdated = tableMeta.GetContentDimension().Values.Map(v => v.LastUpdated).Max(),
                        Links =
                        [
                            new()
                            {
                                Rel = "describedby",
                                Href = fileUri.ToString(),
                                Method = "GET"
                            }
                        ]
                    });
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Failed to get metadata for table: {Table}", tableList.ElementAt(i).Key);
                    continue;
                }
            }

            return Ok(pagedTableList);
        }
    }
}
