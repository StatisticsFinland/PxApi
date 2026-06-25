using System.Collections.Immutable;
using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.Dimensions;
using Px.Utils.Models.Metadata.Enums;

namespace PxApi.Utilities
{
    /// <summary>
    /// Resolves decimal precision for individual data values based on content dimension metadata.
    /// Precomputes a stride to enable O(1) per-cell lookup without allocating per-cell arrays.
    /// Must be constructed from metadata that reflects the actual dimension ordering of the data array,
    /// i.e. after any <c>GetTransform</c> reordering has been applied.
    /// </summary>
    public sealed class PrecisionResolver
    {
        /// <summary>
        /// Sentinel value returned by <see cref="Resolve"/> when no content dimension is present,
        /// indicating that full double precision should be used.
        /// </summary>
        public const int NoPrecision = -1;

        /// <summary>
        /// Shared, precomputed format strings indexed by precision value.
        /// Callers use the <see cref="int"/> returned by <see cref="Resolve"/> to index into this array.
        /// </summary>
        public static readonly ImmutableArray<string> FormatStrings = [.. Enumerable.Range(0, 16).Select(i => $"F{i}")];

        private readonly int[] _precisions;
        private readonly int _stride;
        private readonly int _contentSize;

        /// <summary>
        /// Initializes a new instance of <see cref="PrecisionResolver"/> from the given matrix metadata.
        /// </summary>
        /// <param name="meta">
        /// The matrix metadata describing dimension layout and content variable precisions.
        /// Must reflect the dimension ordering that matches the data array layout.
        /// </param>
        public PrecisionResolver(IReadOnlyMatrixMetadata meta)
        {
            int contentIndex = -1;
            for (int i = 0; i < meta.Dimensions.Count; i++)
            {
                if (meta.Dimensions[i].Type == DimensionType.Content)
                {
                    contentIndex = i;
                    break;
                }
            }

            if (contentIndex == -1)
            {
                _precisions = [];
                _contentSize = 0;
                _stride = 1;
                return;
            }

            ContentDimension contentDim = (ContentDimension)meta.Dimensions[contentIndex];
            _contentSize = contentDim.Values.Count;

            _precisions = new int[_contentSize];
            int j = 0;
            foreach (ContentDimensionValue value in contentDim.Values)
            {
                _precisions[j++] = Math.Clamp(value.Precision, 0, 15);
            }

            int stride = 1;
            for (int i = contentIndex + 1; i < meta.Dimensions.Count; i++)
            {
                stride *= meta.Dimensions[i].Values.Count;
            }
            _stride = stride;
        }

        /// <summary>
        /// Resolves the number of decimal places for the data value at the given index.
        /// Returns <see cref="NoPrecision"/> if no content dimension is present,
        /// indicating that full double precision should be used.
        /// Use the returned value to index into <see cref="FormatStrings"/> for formatting.
        /// </summary>
        /// <param name="dataIndex">Zero-based index into the data array.</param>
        /// <returns>Number of decimal places, or <see cref="NoPrecision"/> for full precision.</returns>
        public int Resolve(int dataIndex)
        {
            if (_contentSize == 0) return NoPrecision;
            return _precisions[(dataIndex / _stride) % _contentSize];
        }
    }
}
