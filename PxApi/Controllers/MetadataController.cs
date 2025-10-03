using Microsoft.AspNetCore.Mvc;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Configuration;
using PxApi.ModelBuilders;
using PxApi.Models;
using PxApi.Utilities;
using PxApi.Models.QueryFilters;

namespace PxApi.Controllers
{
    /// <summary>
    /// Controller for /meta endpoint.
    /// Contains methods for getting metadata about tables and dimensions.
    /// </summary>
    [Route("meta")]
    [ApiController]
    public class MetadataController(ICachedDataSource cachedConnector) : ControllerBase
    {
        /// <summary>
        /// Get metadata for a single table.
        /// </summary>
        /// <param name="database">Name of the database that contains the table</param>
        /// <param name="table">Name of the table</param>
        /// <param name="lang">
        /// [Optional] Language used to get the metadata.
        /// If left empty uses the default language of the table.
        /// The provided language must be available in the table.
        /// </param>
        /// <param name="showValues">[Optional] If true, will list the dimension values. If not provided, the values are not listed.</param>
        /// <returns><see cref="TableMeta"/> object containing metadata of the <paramref name="table"/>.</returns> 
        /// <response code="200">Returns the table metadata</response>
        /// <response code="400">If the metadata is not available in the specified language.</response>
        /// <response code="404">If the table or database is not found.</response>
        [HttpGet("{database}/{table}")]
        [Produces("application/json")] 
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TableMeta>> GetTableMetadataById(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromQuery] string? lang,
            [FromQuery] bool? showValues)
        {
            AppSettings settings = AppSettings.Active;
            try
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if(dbRef is null) return NotFound();
                PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound();

                IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);

                if (lang is null || meta.AvailableLanguages.Contains(lang))
                {
                    Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", database, Path.GetFileNameWithoutExtension(table))
                        .AddQueryParameters(("lang", lang))
                        .AddQueryParameters(("showValues", showValues));

                    TableMeta tableMeta = ModelBuilder.BuildTableMeta(meta, fileUri, lang, showValues);

                    string dataUri = settings.RootUrl
                        .AddRelativePath("data", database, Path.GetFileNameWithoutExtension(table), "json").ToString();
                    dataUri += MetaFiltering.GetDefaultFilteringUrlParameters(meta);

                    tableMeta.Links.Add(new Link()
                    {
                        Href = dataUri,
                        Rel = "data",
                        Method = "GET"
                    });

                    // Populate groupings from cached grouping provider
                    IReadOnlyList<TableGroup> groupings = await cachedConnector.GetGroupingsCachedAsync(fileRef.Value);
                    tableMeta.Groupings.AddRange(groupings);
                    return Ok(tableMeta);
                }
                else
                {
                    return BadRequest("The content is not available in the requested language.");
                }
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get dimension metadata.
        /// </summary>
        /// <param name="database">The name of the database.</param>
        /// <param name="table">The name of the table.</param>
        /// <param name="dimcode">The code of the dimension.</param>
        /// <param name="lang">
        /// [Optional] Language used to get the metadata.
        /// If left empty uses the default language of the table.
        /// The provided language must be available in the table.
        /// </param>
        /// <returns>Returns dimension metadata which can be of type ClassificatoryDimension, ContentDimension, or TimeDimension.</returns>
        /// <response code="200">Returns the dimenion metadata, which can be of type ClassificatoryDimension, ContentDimension, or TimeDimension.</response>
        /// <response code="400">If the content is not available in the specified language.</response>
        /// <response code="404">If the database, table or dimenion is not found.</response>
        [HttpGet("{database}/{table}/{dimcode}")]
        [Produces("application/json")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DimensionBase>> GetDimensionMeta(
            [FromRoute] string database,
            [FromRoute] string table,
            [FromRoute] string dimcode,
            [FromQuery] string? lang)
        {
            AppSettings settings = AppSettings.Active;
            try
            {
                DataBaseRef? dbRef = cachedConnector.GetDataBaseReference(database);
                if(dbRef is null) return NotFound("Database not found.");
                PxFileRef? fileRef = await cachedConnector.GetFileReferenceCachedAsync(table, dbRef.Value);
                if (fileRef is null) return NotFound("Table not found.");

                IReadOnlyMatrixMetadata meta = await cachedConnector.GetMetadataCachedAsync(fileRef.Value);
                IReadOnlyDimension? targetDim = meta.Dimensions.FirstOrDefault(d => d.Code == dimcode);
                if (targetDim is not null)
                {
                    string actualLang = lang ?? meta.DefaultLanguage;
                    if (meta.AvailableLanguages.Contains(actualLang))
                    {
                        const string rel = "self";

                        Uri fileUri = settings.RootUrl
                        .AddRelativePath("meta", database, Path.GetFileNameWithoutExtension(table))
                        .AddQueryParameters(("lang", lang));

                        if (targetDim.Type is DimensionType.Content)
                        {
                            return Ok(ModelBuilder.BuildContentDimension(meta, actualLang, true, fileUri, rel));
                        }
                        else if (targetDim.Type is DimensionType.Time)
                        {
                            return Ok(ModelBuilder.BuildTimeDimension(meta, actualLang, true, fileUri, rel));
                        }
                        else
                        {
                            return Ok(ModelBuilder.BuildDimension(targetDim, actualLang, true, fileUri, rel));
                        }
                    }
                    else
                    {
                        return BadRequest($"The content is not available in language: {lang}");
                    }
                }
                else
                {
                    return NotFound();
                }
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }
    }
}