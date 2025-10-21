namespace PxApi.Configuration
{
    /// <summary>
    /// Configuration for OpenAPI metadata (contact and license info).
    /// </summary>
    public class OpenApiConfig
    {
        /// <summary>
        /// Contact name displayed in OpenAPI document.
        /// </summary>
        public string? ContactName { get; }
        /// <summary>
        /// Contact URL displayed in OpenAPI document.
        /// </summary>
        public Uri? ContactUrl { get; }
        /// <summary>
        /// Contact email displayed in OpenAPI document.
        /// </summary>
        public string? ContactEmail { get; }
        /// <summary>
        /// License name displayed in OpenAPI document.
        /// </summary>
        public string? LicenseName { get; }
        /// <summary>
        /// License URL displayed in OpenAPI document.
        /// </summary>
        public Uri? LicenseUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenApiConfig"/> class from configuration section.
        /// </summary>
        /// <param name="section">Configuration section root for OpenApi settings.</param>
        public OpenApiConfig(IConfigurationSection section)
        {
            string? contactName = section.GetValue<string>(nameof(ContactName));
            if (!string.IsNullOrWhiteSpace(contactName))
            {
                ContactName = contactName;
            }

            string? contactUrlString = section.GetValue<string>(nameof(ContactUrl));
            if (!string.IsNullOrWhiteSpace(contactUrlString) && Uri.TryCreate(contactUrlString, UriKind.Absolute, out Uri? contactUri))
            {
                ContactUrl = contactUri;
            }

            string? contactEmail = section.GetValue<string>(nameof(ContactEmail));
            if (!string.IsNullOrWhiteSpace(contactEmail))
            {
                ContactEmail = contactEmail;
            }

            string? licenseName = section.GetValue<string>(nameof(LicenseName));
            if (!string.IsNullOrWhiteSpace(licenseName))
            {
                LicenseName = licenseName;
            }

            string? licenseUrlString = section.GetValue<string>(nameof(LicenseUrl));
            if (!string.IsNullOrWhiteSpace(licenseUrlString) && Uri.TryCreate(licenseUrlString, UriKind.Absolute, out Uri? licenseUri))
            {
                LicenseUrl = licenseUri;
            }
        }
    }
}
