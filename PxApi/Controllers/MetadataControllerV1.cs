using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata;
using PxApi.DataSources;
using PxApi.ModelBuilders;
using PxApi.Utilities;

namespace PxApi.Controllers
{
    [Route("api/v1/meta")]
    [ApiController]
    public class MetadataControllerV1(IDataSource dataSource) : ControllerBase
    {
        // GET api/v1/meta/{path}
        [HttpGet("{*path}")]
        public async Task<IActionResult> GetMetadataById([FromRoute] string path, [FromQuery] string? lang)
        {
            List<string> hierarchy = PathFunctions.BuildHierarchy(path);
            if(await dataSource.IsFileAsync(hierarchy))
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(hierarchy);
                if(lang is not null)
                {
                    if(meta.AvailableLanguages.Contains(lang)) return Ok(V1ModelBuilder.BuildTableV1(meta, lang));
                    else return BadRequest($"The content is not available in language: {lang}");
                }
                else
                {
                    return Ok(V1ModelBuilder.BuildTableV1(meta, meta.DefaultLanguage));
                }
            }
            return Ok("Tree view not yet implemented.");
        }
    }
}
