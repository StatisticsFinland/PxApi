using Px.Utils.Models.Metadata;
using Px.Utils.Models.Metadata.ExtensionMethods;
using PxApi.Configuration;

namespace PxApi.Utilities
{
    /// <summary>
    /// Selects the most efficient read strategy for accessing an underlying multidimensional blob based on
    /// the size of the blob and the shape/density of a requested sub-selection.
    /// </summary>
    /// <remarks>
    /// The selector chooses between:
    /// <list type="bullet">
    /// <item>
    /// <description><b>Streaming/sequential</b> reads (typically a single forward pass through the blob).</description>
    /// </item>
    /// <item>
    /// <description><b>Windowed/random-access</b> reads (fetching only dense windows when the selection is compact).</description>
    /// </item>
    /// </list>
    /// The decision is made by comparing:
    /// <list type="bullet">
    /// <item><description>Overall blob size.</description></item>
    /// <item><description>Linearized span of the requested indices in the blob (row-major linearization).</description></item>
    /// <item><description>Large gaps created by sparse index selection across dimensions.</description></item>
    /// </list>
    /// The implementation is intentionally heuristic and uses thresholds configured via
    /// <see cref="BlobReadModeConfig"/> to avoid the overhead of windowed reads when they are unlikely to pay off.
    /// </remarks>
    public static class BlobReadModeSelector
    {
        /// <summary>
        /// Determines whether the blob should be read using streaming (sequential) mode or
        /// windowed/random-access mode based on the blob size and the density of the requested sub-map.
        /// Thresholds are read from <see cref="AppSettings.Active"/>.<see cref="AppSettings.BlobReadMode"/>.
        /// </summary>
        /// <param name="read">Index map describing the requested sub-selection within the blob.</param>
        /// <param name="blob">Index map describing the full underlying blob.</param>
        /// <param name="startIndex">
        /// When streaming is selected, receives the 0-based starting linear index in the blob's
        /// flattened index space from which streaming should begin. Set to 0 when streaming from the
        /// beginning or when windowed reading is selected.
        /// </param>
        /// <returns>
        /// <c>true</c> to use streaming; <c>false</c> to use windowed reading.
        /// </returns>
        /// <remarks>
        /// Heuristics applied (using configured thresholds from <see cref="BlobReadModeConfig"/>):
        /// - Small blobs are always streamed: <c>blob.GetSize() &lt; SmallThreshold</c>.
        /// - Small reads at the beginning are streamed if the last linear read index is below <c>SmallThreshold</c>.
        /// - If the linear span covered by the read (<c>last - first + 1</c>) is below <c>MaxWindowedReadSize</c>,
        ///   the read is considered dense and windowed reading is preferred (returns <c>false</c>).
        /// - Large gaps (≥ <c>ReadWindowGap</c>) within and around repeating blocks are subtracted from the
        ///   span; if the effective dense length remains below <c>MaxWindowedReadSize</c>, windowed reading is preferred.
        /// - Otherwise, streaming is used and <paramref name="startIndex"/> is set to the first linear index of the
        ///   read when it is beyond <c>SmallThreshold</c> to skip initial data.
        /// Linearization of multidimensional indices uses reverse cumulative products of the dimension sizes
        /// (row-major order) to compute linear indices.
        /// </remarks>
        public static bool ReadStreaming(IMatrixMap read, IMatrixMap blob, out long startIndex)
        {
            BlobReadModeConfig config = AppSettings.Active.BlobReadMode;
            return ReadStreaming(read, blob, out startIndex, config.SmallThreshold, config.MaxWindowedReadSize, config.ReadWindowGap);
        }

        internal static bool ReadStreaming(IMatrixMap read, IMatrixMap blob, out long startIndex,
            long smallThreshold, long maxWindowedReadSize, long readWindowGap)
        {
            startIndex = 0;
            long blobSize = blob.GetSize();

            if (blobSize < smallThreshold) return true; // Small blobs always stream

            int[][] readIndices = blob.GetIndicesOfSubmap(read);
            int[] dimSizes = [.. blob.DimensionMaps.Select(dm => dm.ValueCodes.Count)];
            long[] rcsp = ComputeReverseCumulativeProducts(dimSizes);

            long lastLinearReadIndex = GetLastLinearReadIndex(readIndices, rcsp);
            if (lastLinearReadIndex < smallThreshold) return true; // Small read at start always streams

            long startLinearReadIndex = GetFirstLinearReadIndex(readIndices, rcsp);
            long readSpanLength = lastLinearReadIndex - startLinearReadIndex + 1;
            if(readSpanLength < maxWindowedReadSize) return false; // Small span indicates dense read, do not stream

            long combinedGaps = GetCombinedGaps(readIndices, dimSizes, rcsp, readWindowGap); 

            if(readSpanLength - combinedGaps < maxWindowedReadSize) return false; // Dense read after removing large gaps, do not stream

            if (startLinearReadIndex > smallThreshold) startIndex = startLinearReadIndex;
            return true; // Otherwise stream
        }

        /// <summary>
        /// Computes the total length of large gaps (in linearized index space) caused by the selection
        /// across all dimensions. Gaps smaller than <paramref name="minLen"/> are ignored.
        /// </summary>
        /// <param name="selectedIndices">For each dimension, a sorted array of selected 0-based indices.</param>
        /// <param name="dimSizes">The full size of each dimension in the blob.</param>
        /// <param name="rcsp">Reverse cumulative size products used for row-major linearization.</param>
        /// <param name="minLen">Minimum gap length in linearized space to include.</param>
        /// <returns>
        /// The sum of all qualifying gaps between consecutive selections per dimension plus the gaps
        /// before the first and after the last selected indices in less significant dimensions, each
        /// scaled by how many times they repeat for combinations of more significant dimensions.
        /// </returns>
        /// <remarks>
        /// For dimension i, an intra-dimension gap between indices a and b contributes
        /// (b - a - 1) * rcsp[i]. Gaps introduced by less significant dimensions (before-first and after-last
        /// regions) are added to each such gap via <see cref="GetGapsBetweenRepeatingBlocks"/>. The combined
        /// gap is multiplied by the cartesian product of selection counts in all more significant dimensions.
        /// Iteration stops when the spread of selected indices within a dimension, scaled by rcsp[i],
        /// falls below <paramref name="minLen"/>, because deeper dimensions cannot produce qualifying gaps.
        /// </remarks>
        internal static long GetCombinedGaps(int[][] selectedIndices, int[] dimSizes, long[] rcsp, long minLen)
        {
            long[] gapsFromRepeatingBlocks = GetGapsBetweenRepeatingBlocks(selectedIndices, dimSizes, rcsp);

            // Product of selected counts in more significant dimensions (repetition factor per dimension)
            long[] moreSignificantRepeatFactors = new long[selectedIndices.Length];
            long repeatAcc = 1;
            for (int i = 0; i < selectedIndices.Length; i++)
            {
                moreSignificantRepeatFactors[i] = repeatAcc;
                repeatAcc *= selectedIndices[i].Length;
            }

            long combinedGaps = 0;
            for (int i = 0; i < selectedIndices.Length; i++)
            {
                if ((selectedIndices[i][^1] - selectedIndices[i][0] + 1) * rcsp[i] < minLen) break; // No more relevant gaps possible
                long repeat = moreSignificantRepeatFactors[i];
                for (int j = 1; j < selectedIndices[i].Length; j++)
                {
                    int stepBetween = selectedIndices[i][j] - selectedIndices[i][j - 1] - 1;
                    if (stepBetween <= 0) continue; // no gap when consecutive
                    long gap = stepBetween * rcsp[i];
                    if (gap >= minLen)
                    {
                        combinedGaps += (gap + gapsFromRepeatingBlocks[i]) * repeat;
                    }
                }
            }
            return combinedGaps;
        }

        internal static long[] ComputeReverseCumulativeProducts(int[] sizes)
        {
            int dims = sizes.Length;
            long[] rcsp = new long[dims];
            rcsp[^1] = 1;
            for (int i = dims - 2; i >= 0; i--)
            {
                rcsp[i] = rcsp[i + 1] * sizes[i + 1];
            }
            return rcsp;
        }

        private static long GetLastLinearReadIndex(int[][] readIndices, long[] rcsp)
        {
            int[] lastReadIndices = new int[readIndices.Length];
            for (int i = 0; i < lastReadIndices.Length; i++)
            {
                lastReadIndices[i] = readIndices[i][^1];
            }

            return GetNthIndex(lastReadIndices, rcsp);
        }

        private static long GetFirstLinearReadIndex(int[][] readIndices, long[] rcsp)
        {
            int[] firstReadIndices = new int[readIndices.Length];
            for (int i = 0; i < readIndices.Length; i++)
            {
                firstReadIndices[i] = readIndices[i][0];
            }

            return GetNthIndex(firstReadIndices, rcsp);
        }

        private static long[] GetGapsBetweenRepeatingBlocks(int[][] selectedIndices, int[] dimSizes, long[] rcsp)
        {
            long[] repeatGaps = new long[selectedIndices.Length];
            long cumulativeSum = 0;
            for (int i = selectedIndices.Length - 1; i >= 0; i--)
            {
                repeatGaps[i] = cumulativeSum;
                long beforeFirst = selectedIndices[i][0] * rcsp[i];
                long afterLast = (dimSizes[i] - 1 - selectedIndices[i][^1]) * rcsp[i];
                cumulativeSum += beforeFirst + afterLast;
            }
            return repeatGaps;
        }

        private static long GetNthIndex(int[] readIndices, long[] rcsp)
        {
            long n = 0;
            for (int i = 0; i < readIndices.Length; i++)
            {
                n += readIndices[i] * rcsp[i];
            }
            return n;
        }
    }
}
