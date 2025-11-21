using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PxApi.Exceptions
{
    /// <summary>
    /// Exception thrown when model validation fails in API controllers.
    /// This exception is designed to be caught by the global error handler and converted to appropriate HTTP responses.
    /// </summary>
    public class InvalidModelException : Exception
    {
        /// <summary>
        /// Gets the model state dictionary containing validation errors.
        /// </summary>
        public ModelStateDictionary ModelState { get; }

        /// <summary>
        /// Gets the HTTP context path where the validation error occurred.
        /// </summary>
        public string RequestPath { get; }

        /// <summary>
        /// Initializes a new instance of the InvalidModelException class.
        /// </summary>
        /// <param name="modelState">The model state dictionary containing validation errors.</param>
        /// <param name="requestPath">The HTTP context path where the validation error occurred.</param>
        public InvalidModelException(ModelStateDictionary modelState, string requestPath)
            : base("Model validation failed")
        {
            ModelState = modelState;
            RequestPath = requestPath;
        }

        /// <summary>
        /// Initializes a new instance of the InvalidModelException class with a custom message.
        /// </summary>
        /// <param name="message">The custom error message.</param>
        /// <param name="modelState">The model state dictionary containing validation errors.</param>
        /// <param name="requestPath">The HTTP context path where the validation error occurred.</param>
        public InvalidModelException(string message, ModelStateDictionary modelState, string requestPath)
            : base(message)
        {
            ModelState = modelState;
            RequestPath = requestPath;
        }
    }
}