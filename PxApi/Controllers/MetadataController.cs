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
        [Produces("application/json")]
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

        /// <summary>
        /// Get variable metadata.
        /// </summary>
        /// <param name="database">The name of the database.</param>
        /// <param name="file">The name of the file.</param>
        /// <param name="varcode">The code of the variable.</param>
        /// <param name="lang">The language of the metadata.</param>
        /// <returns>Returns variable metadata which can be of type Variable, ContentVariable, or TimeVariable.</returns>
        /// <response code="200">Returns the variable metadata.</response>
        /// <response code="400">If the content is not available in the specified language.</response>
        /// <response code="404">If the variable is not found.</response>
        [HttpGet("{database}/{file}/{varcode}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<VariableBase>> GetVariableMeta(
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
                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (meta.AvailableLanguages.Contains(actualLang))
                    { 
                        const string rel = "self";

                        Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", database, Path.GetFileNameWithoutExtension(file))
                        .AddQueryParameters(("lang", lang));

                        if (targetDim.Type is DimensionType.Content)
                        {
                            return Ok(ModelBuilder.BuildContentVariable(meta, actualLang, true, fileUri, rel));
                        }
                        else if (targetDim.Type is DimensionType.Time)
                        {
                            return Ok(ModelBuilder.BuildTimeVariable(meta, actualLang, true, fileUri, rel));
                        }
                        else
                        {
                            return Ok(ModelBuilder.BuildVariable(targetDim, actualLang, true, fileUri, rel));
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