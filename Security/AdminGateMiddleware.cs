using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GlurSolutions.Web.Security;

/// <summary>
/// The first layer in front of every /admin URL (pages and its own CSS/JS), running before anything else can answer.
///  - Not configured (no login set up, or on Azure no origin secret)  -> 404, as if the admin did not exist.
///  - On your computer (Development)                                  -> only requests from this computer.
///  - On Azure                                                        -> only requests carrying the Worker's secret
///    header. Anyone opening the azurewebsites.net address directly gets a plain 404.
/// It also adds the security headers and works out the visitor's real address for the login throttle and audit log.
/// </summary>
public sealed class AdminGateMiddleware(RequestDelegate next, IOptions<AdminSettings> options, IWebHostEnvironment env)
{
    public const string ClientIpItem = "Admin.ClientIp";

    private readonly AdminSettings _settings = options.Value;

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin")) return next(context);

        var isDevelopment = env.IsDevelopment();
        if (!_settings.IsEnabled(isDevelopment)) return NotFound(context);

        if (isDevelopment)
        {
            if (context.Connection.RemoteIpAddress is not { } local || !IPAddress.IsLoopback(local)) return NotFound(context);
            context.Items[ClientIpItem] = local.ToString();
        }
        else
        {
            var supplied = context.Request.Headers["X-Origin-Secret"].ToString();
            if (!SecretMatches(supplied, _settings.OriginSecret!)) return NotFound(context);

            // The Worker sends the visitor's real address. It is believed only because the secret was correct,
            // and it must parse as an address, so it cannot carry anything else into the logs.
            var claimed = context.Request.Headers["X-Client-IP"].ToString();
            context.Items[ClientIpItem] = IPAddress.TryParse(claimed, out var visitor)
                ? visitor.ToString()
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        ApplySecurityHeaders(context.Response);
        return next(context);
    }

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.Headers["Cache-Control"] = "no-store";
        return Task.CompletedTask;
    }

    // Both values are hashed first so they are the same length, then compared in constant time
    private static bool SecretMatches(string supplied, string expected)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void ApplySecurityHeaders(HttpResponse response)
    {
        var headers = response.Headers;
        headers["Cache-Control"] = "no-store";                       // never kept by browsers or Cloudflare
        headers["Pragma"] = "no-cache";
        headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";    // never listed by search engines
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";                         // cannot be shown inside another site's frame
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        // Only this site's own files may run or load; no inline scripts or styles; forms may only post back here
        headers["Content-Security-Policy"] =
            "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; " +
            "connect-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
    }
}
