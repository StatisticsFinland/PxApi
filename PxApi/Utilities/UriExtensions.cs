using System.Collections.Specialized;
using System.Web;

namespace PxApi.Utilities
{
    public static class UriExtensions
    {
        public static Uri AddRelativePath(this Uri baseUrl, string relativePath)
        {
            return new Uri(baseUrl, relativePath);
        }

        public static Uri AddQueryParameters(this Uri baseUrl, params (string Key, string Value)[] queryParams)
        {
            UriBuilder uriBuilder = new(baseUrl);
            NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

            foreach ((string Key, string Value) in queryParams)
            {
                query[Key] = Value;
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;
        }
    }
}
