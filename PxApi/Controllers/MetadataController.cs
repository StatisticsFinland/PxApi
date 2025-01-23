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
        [HttpGet("{database}/{file}")]
        public async Task<ActionResult<TableMeta>> GetMetadataById(
            [FromRoute] string database,
            [FromRoute] string file,
            [FromQuery] string? lang,
            [FromQuery] bool? dropValues)
        {
            AppSettings settings = AppSettings.Active;
            PathFunctions.CheckStringsForInvalidPathChars(database, file);

            TablePath? path = await dataSource.GetTablePathAsync(database, file);
            if (path is not null)
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(path);

                if (lang is null || meta.AvailableLanguages.Contains(lang))
                {
                    return Ok(ModelBuilder.BuildTableMeta(meta, settings.RootUrl, lang, dropValues));
                }
                else
                {
                    return BadRequest($"The content is not available in language: {lang}");
                }
            }

            return NotFound();
        }
    }
}
