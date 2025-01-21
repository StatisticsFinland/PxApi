using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.Configuration;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    [Route("meta")]
    [ApiController]
    public class MetadataController(IDataSource dataSource) : ControllerBase
    {
        [HttpGet("{*path}")]
        public async Task<ActionResult<TableMeta>> GetMetadataById([FromRoute] string path, [FromQuery] string? lang)
        {
            AppSettings settings = AppSettings.Active;

            List<string> hierarchy = PathFunctions.BuildHierarchyFromRelativeUrl(path);
            if(await dataSource.IsFileAsync(hierarchy))
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(hierarchy);

                if (lang is not null)
                {
                    if (meta.AvailableLanguages.Contains(lang)) return Ok(ModelBuilder.BuildTableMeta(meta, settings.RootUrl, lang));
                    else return BadRequest($"The content is not available in language: {lang}");
                }
                else
                {
                    return Ok(ModelBuilder.BuildTableMeta(meta, settings.RootUrl));
                }
            }
            return NotFound();
        }
    }
}
