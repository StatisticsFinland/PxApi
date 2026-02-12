namespace PxApi.Utilities
{
    /// <summary>
    /// Provides helper methods for normalizing Azure Blob Storage paths to use forward slashes.
    /// </summary>
    public static class BlobPathHelper
    {
        /// <summary>
        /// Normalizes a blob path by replacing backslashes with forward slashes, collapsing
        /// consecutive slashes, and trimming leading and trailing slashes.
        /// </summary>
        /// <param name="path">The blob path to normalize.</param>
        /// <returns>A normalized blob path suitable for use with Azure Blob Storage APIs.</returns>
        public static string NormalizeBlobPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            ReadOnlySpan<char> span = path.AsSpan();
            Span<char> buffer = stackalloc char[span.Length];
            int writeIndex = 0;
            bool previousWasSlash = false;

            foreach (char c in span)
            {
                if (c is '/' or '\\')
                {
                    if (!previousWasSlash)
                    {
                        buffer[writeIndex++] = '/';
                        previousWasSlash = true;
                    }
                }
                else
                {
                    buffer[writeIndex++] = c;
                    previousWasSlash = false;
                }
            }

            ReadOnlySpan<char> result = buffer[..writeIndex];
            result = result.Trim('/');
            return result.ToString();
        }

        /// <summary>
        /// Combines two or more blob path segments into a single normalized blob path using forward slashes.
        /// </summary>
        /// <param name="segments">The path segments to combine.</param>
        /// <returns>A normalized blob path formed by joining the segments with <c>/</c>.</returns>
        public static string CombineBlobPath(params string[] segments)
        {
            string joined = string.Join('/', segments);
            return NormalizeBlobPath(joined);
        }
    }
}
