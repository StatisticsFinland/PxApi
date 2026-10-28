using Microsoft.Extensions.Primitives;
using PxApi.Utilities;

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
    /// Uses normalized audit fields; NLog routes the logger category to the dedicated audit target.
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
                    context[header] = SanitizeLogValue(value.ToString());
                }
            }

            context[LoggerConsts.Audit.Fields.Category] = LoggerConsts.Audit.CategoryValue;

            using (_logger.BeginScope(context))
            {
                _logger.LogInformation("Audit event: user={user}, client_ip={client_ip}",
                    SanitizeLogValue(httpContext.User.Identity?.Name ?? LoggerConsts.Audit.Anonymous),
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? LoggerConsts.Audit.Unknown
                    );
            }
        }

        private static string SanitizeLogValue(string value)
        {
            return string.Concat(value.Select(character => char.IsControl(character) || character is '\u2028' or '\u2029' ? ' ' : character));
        }
    }
}
