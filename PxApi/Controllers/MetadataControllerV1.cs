using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Models.V1;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    [Route("meta")]
    [ApiController]
    public class MetadataControllerV1(IDataSource dataSource, LinkGenerator linkGenerator) : ControllerBase
    {
        // GET meta/{path}
        [HttpGet("{*path}")]
        public async Task<ActionResult<TableV1>> GetMetadataById([FromRoute] string path, [FromQuery] string? lang)
        {
            List<string> hierarchy = PathFunctions.BuildHierarchy(path);
            if(await dataSource.IsFileAsync(hierarchy))
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(hierarchy);
                string urlBase = linkGenerator.GetUriByAction(
                    HttpContext,
                    action: nameof(GetMetadataById),
                    controller: nameof(MetadataControllerV1))
                    ?? throw new InvalidOperationException("Could not generate URL.");

                if (lang is not null)
                {
                    if (meta.AvailableLanguages.Contains(lang)) return V1ModelBuilder.BuildTableV1(meta, urlBase, lang);
                    else return BadRequest($"The content is not available in language: {lang}");
                }
                else
                {
                    return V1ModelBuilder.BuildTableV1(meta, urlBase);
                }
            }
            return NotFound();
        }
    }
}
