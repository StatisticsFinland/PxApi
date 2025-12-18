using Microsoft.Extensions.Primitives;

namespace PxApi.Services
{
    /// <summary>
    /// Service for writing audit log entries for data and metadata retrieval operations.
    /// The logs are written only when audit logging is enabled via configuration (LogOptions:AuditLog:Enabled).
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// Writes an audit log entry describing an action performed on a resource.
        /// </summary>
        void LogAuditEvent();
    }

    /// <summary>
    /// Implementation of <see cref="IAuditLogService"/> that gathers selected request header values and contextual information.
    /// Uses a logging scope with the Category set to "Audit" so that NLog can route the event to the dedicated audit target.
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;
        private readonly bool _auditEnabled;
        private readonly IReadOnlyList<string> _headerWhitelist;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogService"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="configuration">Application configuration for reading audit settings.</param>
        public AuditLogService(IHttpContextAccessor httpContextAccessor, ILogger<AuditLogService> logger, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _auditEnabled = configuration.GetValue<bool>("LogOptions:AuditLog:Enabled");
            IEnumerable<string>? headers = configuration.GetSection("LogOptions:AuditLog:Headers").Get<IEnumerable<string>>();
            _headerWhitelist = headers is null ? [] : headers.ToList();
        }

        /// <inheritdoc />
        public void LogAuditEvent()
        {
            if (!_auditEnabled) return;

            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            Dictionary<string, string> context = [];
            foreach (string header in _headerWhitelist)
            {
                if (httpContext.Request.Headers.TryGetValue(header, out StringValues value))
                {
                    context[header] = value.ToString();
                }
            }

            context["Category"] = "Audit";

            using (_logger.BeginScope(context))
            {
                _logger.LogInformation("Audit Event: user={User}, clientIP={ClientIP}",
                    httpContext.User.Identity?.Name ?? "Anonymous",
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                    );
            }
        }
    }
}
