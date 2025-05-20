using Microsoft.AspNetCore.HttpOverrides;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;

namespace CurrencyConverterAPI.Helpers
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly string _forwardedForHeader = "X-Forwarded-For";
        private readonly string _realIpHeader = "X-Real-IP";
        private readonly string _forwardedHeader = "Forwarded";

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                string? clientIp = null;

                // 1. Check the Forwarded header
                var forwardedValues = context.Request.Headers[_forwardedHeader].FirstOrDefault()?.Split(',');
                if (forwardedValues?.Length > 0)
                {
                    var forValue = forwardedValues.FirstOrDefault(v => v.Trim().StartsWith("for=", StringComparison.OrdinalIgnoreCase));
                    if (forValue != null)
                    {
                        clientIp = forValue.Trim().Substring(4).Trim('"', ' ');
                    }
                }

                // 2. Fallback to X-Forwarded-For header
                var xff = context.Request.Headers[_forwardedForHeader].FirstOrDefault();
                if (!IPAddress.TryParse(clientIp, out _) && !string.IsNullOrEmpty(xff))
                {
                    var firstIp = xff.Split(',').FirstOrDefault()?.Trim();
                }

                // 3. Fallback to X-Real-IP header
                var realIp = context.Request.Headers[_realIpHeader].FirstOrDefault();
                if (!IPAddress.TryParse(clientIp, out _) && !string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
                {
                    clientIp = realIp;
                }

                // 4. If no forwarded headers are found, use the RemoteIpAddress from the connection
                if(!IPAddress.TryParse(clientIp, out _))
                {
                    clientIp = context.Connection.RemoteIpAddress?.ToString();
                }

                var method = context.Request.Method;
                var path = context.Request.Path;
                var statusCode = context.Response.StatusCode;
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                var clientId = context.User.Claims.FirstOrDefault(c =>
                    c.Type == "ClientId" || c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value ?? "unknown";

                //_logger.LogInformation("Request from {ClientIp} | ClientId: {ClientId} | {Method} {Path} => {StatusCode} in {Elapsed}ms",
                //    clientIp, clientId, method, path, statusCode, elapsedMs);

                _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} from {IP}", method, path, statusCode, clientIp);
            }
        }
    }
}
