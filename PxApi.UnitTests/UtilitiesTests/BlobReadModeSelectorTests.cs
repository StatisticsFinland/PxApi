using Px.Utils.Models.Metadata;
using PxApi.Utilities;

namespace PxApi.UnitTests.UtilitiesTests
{
    [TestFixture]
    internal class BlobReadModeSelectorTests
    {
        const long SmallThreshold = 2_000_000;
        const long MaxWindowedReadSize = 10_000_000;
        const long ReadWindowGap = 500_000;

        [Test]
        public void ReadStreamingWithSmallBlobReturnsTrueAndStartIndexZero()
        {
            // Arrange: blob size product < 2,000,000 (SmallTreshold) using multiple small dimensions
            // 20 * 20 * 20 * 20 * 2 = 320,000 < 2,000,000 -> small blob should stream
            MatrixMap blob = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", 20)),
                new DimensionMap("dim1", CreateValueCodes("dim1", 20)),
                new DimensionMap("dim2", CreateValueCodes("dim2", 20)),
                new DimensionMap("dim3", CreateValueCodes("dim3", 20)),
                new DimensionMap("dim4", CreateValueCodes("dim4", 2))
            ]);

            MatrixMap read = new([
                new DimensionMap("dim0", ["dim0-val0"]),
                new DimensionMap("dim1", ["dim1-val0"]),
                new DimensionMap("dim2", ["dim2-val0"]),
                new DimensionMap("dim3", ["dim3-val0"]),
                new DimensionMap("dim4", ["dim4-val0"]) 
            ]);

            // Act
            bool streaming = BlobReadModeSelector.ReadStreaming(read, blob, out long startIndex,
                SmallThreshold, MaxWindowedReadSize, ReadWindowGap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(streaming, Is.True);
                Assert.That(startIndex, Is.Zero);
            }
        }

        [Test]
        public void ReadStreamingWithSmallReadAtStartReturnsTrue()
        {
            // Arrange: large blob using multiple dimensions
            MatrixMap blob = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", 50)),
                new DimensionMap("dim1", CreateValueCodes("dim1", 50)),
                new DimensionMap("dim2", CreateValueCodes("dim2", 50)),
                new DimensionMap("dim3", CreateValueCodes("dim3", 20)),
                new DimensionMap("dim4", CreateValueCodes("dim4", 20))
            ]);

            // Read selects small prefixes in early dimensions and single values otherwise.
            // The last linear read index is kept below 2,000,000 (SmallTreshold) to force streaming.
            MatrixMap read = new([
                new DimensionMap("dim0", ["dim0-val0"]),
                new DimensionMap("dim1", ["dim1-val0"]),
                new DimensionMap("dim2", CreateValueCodes("dim2", 5)), // small prefix to keep last index small
                new DimensionMap("dim3", ["dim3-val0"]),
                new DimensionMap("dim4", ["dim4-val0"]) 
            ]);

            // Act
            bool streaming = BlobReadModeSelector.ReadStreaming(read, blob, out long startIndex,
                SmallThreshold, MaxWindowedReadSize, ReadWindowGap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(streaming, Is.True);
                Assert.That(startIndex, Is.Zero);
            }
        }

        [Test]
        public void ReadStreamingWithDenseSmallSpanReturnsFalse()
        {
            // Arrange: blob with many dimensions
            MatrixMap blob = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", 10000)),
                new DimensionMap("dim1", CreateValueCodes("dim1", 10)),
                new DimensionMap("dim2", CreateValueCodes("dim2", 10)),
                new DimensionMap("dim3", CreateValueCodes("dim3", 10)),
                new DimensionMap("dim4", CreateValueCodes("dim4", 10))
            ]);

            // Read selects first 901 values in highest-stride dimension and single values in others to form contiguous span.
            // Span length = last - first + 1 = 900 - 0 + 1 = 901 (< MaxWindowedReadSize 10,000,000) -> prefer windowed (false).
            MatrixMap read = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", 901)), // contiguous span of 901 elements
                new DimensionMap("dim1", ["dim1-val0"]),
                new DimensionMap("dim2", ["dim2-val0"]),
                new DimensionMap("dim3", ["dim3-val0"]),
                new DimensionMap("dim4", ["dim4-val0"]) 
            ]);

            // Act
            bool streaming = BlobReadModeSelector.ReadStreaming(read, blob, out long startIndex,
                SmallThreshold, MaxWindowedReadSize, ReadWindowGap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(streaming, Is.False);
                Assert.That(startIndex, Is.Zero);
            }
        }

        [Test]
        public void ReadStreamingFromEndOfBlobStreamsAndSetsStartIndex()
        {
            // Arrange: fix most-significant dimension to last value and take all in others -> contiguous large span
            int[] sizes = [10, 1000, 1000, 10, 10];
            MatrixMap blob = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", sizes[0])),
                new DimensionMap("dim1", CreateValueCodes("dim1", sizes[1])),
                new DimensionMap("dim2", CreateValueCodes("dim2", sizes[2])),
                new DimensionMap("dim3", CreateValueCodes("dim3", sizes[3])),
                new DimensionMap("dim4", CreateValueCodes("dim4", sizes[4]))
            ]);

            MatrixMap read = new([
                new DimensionMap("dim0", ["dim0-val9"]), // select last (index 9) in most significant dimension
                new DimensionMap("dim1", CreateValueCodes("dim1", sizes[1])),
                new DimensionMap("dim2", CreateValueCodes("dim2", sizes[2])),
                new DimensionMap("dim3", CreateValueCodes("dim3", sizes[3])),
                new DimensionMap("dim4", CreateValueCodes("dim4", sizes[4]))
            ]);

            // rcsp0 = product of less significant sizes = 1000 * 1000 * 10 * 10 = 100,000,000
            // expectedStart = 9 * rcsp0 = 900,000,000 (> SmallTreshold), so streaming should start from this index.
            long rcsp0 = (long)sizes[1] * sizes[2] * sizes[3] * sizes[4];
            long expectedStart = 9L * rcsp0;

            // Act
            bool streaming = BlobReadModeSelector.ReadStreaming(read, blob, out long startIndex,
                SmallThreshold, MaxWindowedReadSize, ReadWindowGap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(streaming, Is.True);
                Assert.That(startIndex, Is.EqualTo(expectedStart));
            }
        }

        [Test]
        public void ReadStreamingWithSparseSelectionAndLargeGapsReturnsFalse()
        {
            // Arrange: sparse selection in a mid-order dimension creates large gaps >= 500,000 (ReadWindowGap)
            MatrixMap blob = new([
                new DimensionMap("dim0", CreateValueCodes("dim0", 10)),
                new DimensionMap("dim1", CreateValueCodes("dim1", 10)),
                new DimensionMap("dim2", CreateValueCodes("dim2", 1000)),
                new DimensionMap("dim3", CreateValueCodes("dim3", 200)),
                new DimensionMap("dim4", CreateValueCodes("dim4", 200))
            ]);

            // Steps of 100 in dim2 with many values in dim3 and dim4 result in large linear gaps
            MatrixMap read = new([
                new DimensionMap("dim0", ["dim0-val0"]),
                new DimensionMap("dim1", ["dim1-val0"]),
                new DimensionMap("dim2", CreateSteppedValueCodes("dim2", start: 0, step: 100, count: 10)), // large steps
                new DimensionMap("dim3", CreateValueCodes("dim3", 200)),
                new DimensionMap("dim4", CreateValueCodes("dim4", 200))
            ]);

            // Act
            bool streaming = BlobReadModeSelector.ReadStreaming(read, blob, out long startIndex,
                SmallThreshold, MaxWindowedReadSize, ReadWindowGap);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(streaming, Is.False); // windowed preferred due to large gaps reducing dense span
                Assert.That(startIndex, Is.Zero);
            }
        }

        // Grouped tests for GetCombinedGaps
        #region GetCombinedGaps
        [Test]
        public void BasicSingleDimensionAccumulatesQualifyingGaps()
        {
            int[][] selected = [[0, 5, 10]];
            int[] sizes = [20];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            long minLen = 4; // include gaps with length >= 4

            // Two intra-dimension gaps with stepBetween 4 each: (5 - 0 - 1) = 4 and (10 - 5 - 1) = 4.
            // Each gap contributes 4 * rcsp[0] = 4 * 1 = 4. No repeating block gaps (single dimension).
            long expected = 8; // 4 + 4

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void SingleDimensionBelowThresholdReturnsZero()
        {
            int[][] selected = [[0, 5, 10]];
            int[] sizes = [20];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            long minLen = 6; // excludes gaps of length 4

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void IncludesGapsFromLessSignificantDimensionsPerGap()
        {
            int[] sizes = [5, 10];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [0, 2],  // gap at i=0: stepBetween = 2 - 0 - 1 = 1 -> 1 * 10 = 10
                [2, 3]   // less significant i=1: beforeFirst = 2 * 1 = 2, afterLast = (9 - 3) * 1 = 6 -> total 8
            ];
            long minLen = 5; // includes i=0 gap (10), excludes i=1 gaps on their own

            long expected = 18; // 10 + 8

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void EarlyTerminationSkipsDeeperDimensions()
        {
            int[] sizes = [100, 100, 100];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [5, 6], // spread at i=0: (6 - 5) * 10000 = 10000
                [0, 50, 99],
                [0, 25, 50, 75, 99]
            ];
            long minLen = 20000; // spread (10000) < minLen -> break; deeper dims cannot produce qualifying gaps

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void GapEqualToThresholdIsIncluded()
        {
            // Choose indices so that stepBetween equals 10: b - a - 1 = 10 -> b - a = 11.
            int[][] selected = [[0, 11]];
            int[] sizes = [100];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            long minLen = 10; // gap equals threshold -> included

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.EqualTo(10)); // 10 * 1
        }

        [Test]
        public void ComplexScenarioCombinesGapsRepeatingBlocksAndRepeatFactors()
        {
            int[] sizes = [3, 1000, 5];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [0, 1, 2],      // i=0: continuous selections -> no intra-dimension gaps
                [0, 201, 211],  // i=1: gaps 1000 (qualifies) and 45 (does not) when using stepBetween * 5
                [0]             // i=2: single selection -> contributes only repeating block gaps
            ];
            long minLen = 1000; // include gaps >= 1000

            // Repeating blocks for i=1: from i=2 only (since i=0 has no qualifying gaps)
            // i=2: beforeFirst = 0 * 1 = 0, afterLast = (4 - 0) * 1 = 4 -> 4
            // i=1 contribution: (stepBetween for 0->201) = 200 -> 200 * 5 = 1000, plus repeating 4, repeated by len(selected[0]) = 3
            // => (1000 + 4) * 3 = 3012
            long expected = 3012;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void LargeGapsInMiddleDimensionProduceNonZeroCombinedGaps()
        {
            // Mirrors ReadStreamingWithSparseSelectionAndLargeGapsReturnsFalse scenario
            int[] sizes = [10, 10, 1000, 200, 200];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [0],
                [0],
                // dim2: indices 0,100,200,...,900
                [0, 100, 200, 300, 400, 500, 600, 700, 800, 900],
                // dim3 and dim4: dense selections increase repeating block gaps
                CreateConsecutiveIndices(200),
                CreateConsecutiveIndices(200)
            ];
            long minLen = 500000; // same order as ReadWindowGap used by streaming decision

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.GreaterThan(0));
        }
        #endregion

        // Additional targeted tests for GetCombinedGaps focusing on middle-dimension behavior
        #region GetCombinedGaps_MiddleDimensionFocus
        [Test]
        public void MiddleDimensionGapsJustBelowThresholdReturnZero()
        {
            int[] sizes = [2, 2, 1000, 10, 10];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            // dim2 stepBetween = 49 -> gap = 49 * 100 = 4900 < 5000 threshold
            int[][] selected =
            [
                [0],
                [0],
                [0, 50],
                CreateConsecutiveIndices(10),
                CreateConsecutiveIndices(10)
            ];
            long minLen = 5000;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void MiddleDimensionGapsEqualThresholdAreIncluded()
        {
            int[] sizes = [2, 2, 1000, 10, 10];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            // dim2 stepBetween = 50 - 0 - 1 = 49 -> 49*100=4900 (below)
            // Use 51 to make stepBetween=50 -> 50*100=5000 equals threshold
            int[][] selected =
            [
                [0],
                [0],
                [0, 51],
                CreateConsecutiveIndices(10),
                CreateConsecutiveIndices(10)
            ];
            long minLen = 5000;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.GreaterThan(0));
        }

        [Test]
        public void MiddleDimensionSteppedStartingAtOneStillProducesGaps()
        {
            int[] sizes = [2, 2, 1000, 200, 200];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            // Start at 1 to catch off-by-one issues: indices 1,101 -> stepBetween = 101-1-1 = 99
            int[][] selected =
            [
                [0],
                [0],
                [1, 101],
                CreateConsecutiveIndices(200),
                CreateConsecutiveIndices(200)
            ];
            long minLen = 500000; // 99*40000=3,960,000 >= threshold

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            Assert.That(result, Is.GreaterThan(0));
        }

        [Test]
        public void MiddleDimensionLargeGapsWithMoreSignificantSelectionsScaleResult()
        {
            int[] sizes = [3, 3, 1000, 100, 100];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            // More significant dims have multiple selections -> repeat factor = 3*3 = 9 for i=2
            int[][] selected =
            [
                [0, 2],
                [0, 1, 2],
                [0, 200], // stepBetween=199 -> 199*10000 = 1,990,000
                CreateConsecutiveIndices(100),
                CreateConsecutiveIndices(100)
            ];
            long minLen = 1_000_000;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            // Base gap qualifies; with repeat factor 9 it should scale beyond base
            Assert.That(result, Is.GreaterThanOrEqualTo(1_990_000 * 9));
        }
        #endregion

        // Precise expectation tests to pinpoint failures in middle-dimension gap handling
        #region GetCombinedGaps_MiddleDimensionPreciseExpectations

        [Test]
        public void MiddleDimensionGapEqualThresholdExactValue()
        {
            // sizes: [2, 2, 1000, 10, 10] => rcsp: [200000, 100000, 100, 10, 1]
            int[] sizes = [2, 2, 1000, 10, 10];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [0],
                [0],
                [0, 51], // stepBetween = 50 -> 50 * rcsp[2] = 50 * 100 = 5000
                CreateConsecutiveIndices(10),
                CreateConsecutiveIndices(10)
            ];
            long minLen = 5000;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            // Less significant dims are fully selected (0..9), so repeating block gaps = 0; repeat factor from dims 0..1 = 1
            Assert.That(result, Is.EqualTo(5000));
        }

        [Test]
        public void MiddleDimensionStartAtOneExactValue()
        {
            // sizes: [2, 2, 1000, 200, 200] => rcsp: [8000000, 4000000, 40000, 200, 1]
            int[] sizes = [2, 2, 1000, 200, 200];
            long[] rcsp = BlobReadModeSelector.ComputeReverseCumulativeProducts(sizes);
            int[][] selected =
            [
                [0],
                [0],
                [1, 101], // stepBetween = 99 -> 99 * rcsp[2] = 99 * 40000 = 3,960,000
                CreateConsecutiveIndices(200),
                CreateConsecutiveIndices(200)
            ];
            long minLen = 500000;

            long result = BlobReadModeSelector.GetCombinedGaps(selected, sizes, rcsp, minLen);

            // Less significant dims fully selected (0..199) => repeating block gaps = 0; repeat factor = 1
            Assert.That(result, Is.EqualTo(3_960_000));
        }

        #endregion

        private static List<string> CreateValueCodes(string dimCode, int count)
        {
            List<string> codes = [];
            for (int i = 0; i < count; i++)
            {
                codes.Add($"{dimCode}-val{i}");
            }
            return codes;
        }

        private static List<string> CreateSteppedValueCodes(string dimCode, int start, int step, int count)
        {
            List<string> codes = [];
            for (int i = 0; i < count; i++)
            {
                int idx = start + i * step;
                codes.Add($"{dimCode}-val{idx}");
            }
            return codes;
        }

        private static int[] CreateConsecutiveIndices(int count)
        {
            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }
            return indices;
        }
    }
}
