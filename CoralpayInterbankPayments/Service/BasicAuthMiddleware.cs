using System.Net;
using System.Text;

namespace CoralPayInterbankPayment.Service
{
    public class BasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _username;
        private readonly string _password;

        public BasicAuthMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _username = configuration["BasicAuth:Username"]!;
            _password = configuration["BasicAuth:Password"]!;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                string authHeader = context.Request.Headers["Authorization"]!;

                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic "))
                {
                    var encoded = authHeader.Substring("Basic ".Length).Trim();
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    var parts = decoded.Split(':', 2);

                    if (parts.Length == 2 && parts[0] == _username && parts[1] == _password)
                    {
                        await _next(context);
                        return;
                    }
                }

                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"STB Swagger UI\"";
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Invalid or missing Basic Authentication.");
                return;
            }

            await _next(context);
        }
    }
}
