using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
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
        public async Task<ActionResult<TableMeta>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string file,
            [FromQuery] string? lang,
            [FromQuery] bool? showValues)
        {
            AppSettings settings = AppSettings.Active;
            PathFunctions.CheckStringsForInvalidPathChars(database, file);

            TablePath? path = await dataSource.GetTablePathAsync(database, file);
            if (path is not null)
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(path);

                if (lang is null || meta.AvailableLanguages.Contains(lang))
                {
                    Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", database, Path.GetFileNameWithoutExtension(file))
                        .AddQueryParameters(("lang", lang))
                        .AddQueryParameters(("showValues", showValues));

                    return Ok(ModelBuilder.BuildTableMeta(meta, fileUri, lang, showValues));
                }
                else
                {
                    return BadRequest($"The content is not available in language: {lang}");
                }
            }

            return NotFound();
        }

        [HttpGet("{database}/{file}/{varcode}")]
        public async Task<ActionResult<TableMeta>> GetVariableMeta(
            [FromRoute] string database,
            [FromRoute] string file,
            [FromRoute] string varcode,
            [FromQuery] string? lang)
        {
            AppSettings settings = AppSettings.Active;
            PathFunctions.CheckStringsForInvalidPathChars(database, file);
            TablePath? path = await dataSource.GetTablePathAsync(database, file);
            if (path is not null)
            {
                IReadOnlyMatrixMetadata meta = await dataSource.GetTableMetadataAsync(path);
                IReadOnlyDimension? targetDim = meta.Dimensions.FirstOrDefault(d => d.Code == varcode);
                if(targetDim is not null)
                {
                    string practicalLang = lang ?? meta.DefaultLanguage;
                    if (meta.AvailableLanguages.Contains(lang))
                    { 
                        Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", database, Path.GetFileNameWithoutExtension(file))
                        .AddQueryParameters(("lang", lang));

                        if (targetDim.Type is DimensionType.Content)
                        {
                            return Ok(ModelBuilder.BuildContentVariable(meta, practicalLang, true, fileUri));
                        }
                        else if (targetDim.Type is DimensionType.Time)
                        {
                            return Ok(ModelBuilder.BuildTimeVariable(meta, practicalLang, true, fileUri));
                        }
                        else
                        {
                            return Ok(ModelBuilder.BuildVariable(targetDim, practicalLang, true, fileUri));
                        }
                    }
                    else
                    {
                        return BadRequest($"The content is not available in language: {lang}");
                    }
                }
            }

            return NotFound();
        }
    }
}
