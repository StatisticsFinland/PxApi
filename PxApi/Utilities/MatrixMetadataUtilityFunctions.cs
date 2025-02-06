using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;
using Px.Utils.Models.Metadata.MetaProperties;
using Px.Utils.Models.Metadata;
using PxApi.ModelBuilders;
using Px.Utils.Language;

namespace PxApi.Utilities
{
    /// <summary>
    /// Contains utility functions for working with <see cref="MatrixMetadata"/> and its <see cref="Dimension"/> objects from Px.Utils
    /// </summary>
    public static class MatrixMetadataUtilityFunctions
    {
        /// <summary>
        /// Assigns ordinal or nominal dimension types to dimensions that are either of unknown or other type based on their meta-id properties.
        /// </summary>
        /// <param name="meta">The matrix metadata to assign the dimension types to.</param>
        public static void AssignOrdinalDimensionTypes(MatrixMetadata meta)
        {
            for (int i = 0; i < meta.Dimensions.Count; i++)
            {
                DimensionType newType = GetDimensionType(meta.Dimensions[i]);
                if (newType == DimensionType.Ordinal || newType == DimensionType.Nominal)
                {
                    meta.Dimensions[i] = new(
                        meta.Dimensions[i].Code,
                        meta.Dimensions[i].Name,
                        meta.Dimensions[i].AdditionalProperties,
                        meta.Dimensions[i].Values,
                        newType);
                }
            }
        }

        private static DimensionType GetDimensionType(Dimension dimension)
        {
            // If the dimension already has a defining type, ordinality should not overrun it
            if ((dimension.Type == DimensionType.Unknown ||
                dimension.Type == DimensionType.Other) &&
                dimension.AdditionalProperties.TryGetValue(PxFileConstants.META_ID, out MetaProperty? prop) &&
                prop is MultilanguageStringProperty mlsProp)
            {
                dimension.AdditionalProperties.Remove(PxFileConstants.META_ID); // OBS: Remove the property after retrieval
                if (mlsProp.Value.UniformValue().Equals(PxFileConstants.ORDINAL_VALUE)) return DimensionType.Ordinal;
                else if (mlsProp.Value.UniformValue().Equals(PxFileConstants.NOMINAL_VALUE)) return DimensionType.Nominal;
            }
            return dimension.Type;
        }
    }
}
