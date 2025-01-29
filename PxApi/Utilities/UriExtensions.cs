using System.Collections.Specialized;
using System.Web;

namespace PxApi.Utilities
{
    /// <summary>
    /// Extension methods for the <see cref="Uri"/> class.
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Adds a relative path to the base URL.
        /// </summary>
        public static Uri AddRelativePath(this Uri baseUrl, params string[] relativePath)
        {
            string basePath = baseUrl.AbsolutePath.TrimEnd('/');
            string combinedPath = string.Join('/', relativePath.Select(p => p.Trim('/')));
            string newPath = $"{basePath}/{combinedPath}";
            return new UriBuilder(baseUrl) { Path = newPath, Query = baseUrl.Query }.Uri;
        }

        /// <summary>
        /// Adds query parameters to the URL.
        /// </summary>
        /// <typeparam name="T">The type of the query parameter value.</typeparam>
        public static Uri AddQueryParameters<T>(this Uri baseUrl, params (string Key, T Value)[] queryParams)
        {
            UriBuilder uriBuilder = new(baseUrl);
            NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach ((string Key, T Value) in queryParams)
            {
                if (Value is null) continue;
                if (Value is bool b) query[Key] = b.ToString().ToLower();
                else query[Key] = Value.ToString();
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;
        }

        /// <summary>
        /// Removes a query parameter from the URL.
        /// </summary>
        public static Uri DropQueryParameters(this Uri uri, string paramName)
        {
            NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
            query.Remove(paramName);

            UriBuilder uriBuilder = new(uri)
            {
                Query = query.ToString()
            };
            return uriBuilder.Uri;
        }
    }
}
