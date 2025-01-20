using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    [Route("meta")]
    [ApiController]
    public class MetadataController(IDataSource dataSource, LinkGenerator linkGenerator) : ControllerBase
    {
        // GET meta/{path}
        [HttpGet("{*path}")]
        public async Task<ActionResult<TableMeta>> GetMetadataById([FromRoute] string path, [FromQuery] string? lang)
        {
            List<string> hierarchy = PathFunctions.BuildHierarchy(path);
            if(await dataSource.IsFileAsync(hierarchy))
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(hierarchy);
                string urlString = linkGenerator.GetUriByAction(
                    HttpContext,
                    action: nameof(GetMetadataById),
                    controller: nameof(MetadataController))
                    ?? throw new InvalidOperationException("Could not generate URL.");

                if (lang is not null)
                {
                    if (meta.AvailableLanguages.Contains(lang)) return ModelBuilder.BuildTableMeta(meta, new Uri(urlString), lang);
                    else return BadRequest($"The content is not available in language: {lang}");
                }
                else
                {
                    return ModelBuilder.BuildTableMeta(meta, new Uri(urlString));
                }
            }
            return NotFound();
        }
    }
}
