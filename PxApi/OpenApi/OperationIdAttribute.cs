namespace PxApi.OpenApi
{
    /// <summary>
    /// Attribute used to explicitly set the OpenAPI operationId for a controller action.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OperationIdAttribute : Attribute
    {
        /// <summary>
        /// Gets the operation identifier to use in the generated OpenAPI document.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationIdAttribute"/> class.
        /// </summary>
        /// <param name="id">The desired operation identifier.</param>
        public OperationIdAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Operation id cannot be null or whitespace.", nameof(id));
            }

            Id = id.Trim();
        }
    }
}
