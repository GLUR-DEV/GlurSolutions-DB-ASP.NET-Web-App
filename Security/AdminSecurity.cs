using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlurSolutions.Web.Security;

/// <summary>
/// Everything that protects /Admin: the gate (404 for anyone who should not even know it exists),
/// cookie login, security headers and the client-IP helper. This replaces AdminAccess.cs.
/// </summary>
public static class AdminSecurity
{
    public const string OriginHeader = "X-Origin-Secret";
    public const string StampClaim = "glur:pwstamp";
    public const string LoginAtKey = "glur:login_at";
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(8);

    // The existing admin pages use inline styles and onclick="return confirm(...)", hence 'unsafe-inline'.
    // Later: move the confirms into one script file and drop 'unsafe-inline' from script-src.
    private const string Csp =
        "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; " +
        "img-src 'self' https: data:; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";

    public static IServiceCollection AddAdminSecurity(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AdminOptions>(config.GetSection(AdminOptions.Section));
        services.AddSingleton<LoginThrottle>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-glur-admin";   // __Host- : Secure, path "/", no Domain, so subdomains cannot overwrite it
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = IdleTimeout;
                options.SlidingExpiration = true;
                options.LoginPath = "/Admin/Login";
                options.AccessDeniedPath = "/Admin/Login";
                options.Events.OnValidatePrincipal = ValidateSessionAsync;
            });

        // The antiforgery cookie defaults to SameSite=Strict, which browsers refuse to store when the page was reached
        // from another site (here: the Cloudflare Access sign-in). The form then has no matching cookie and the POST
        // fails with a blank HTTP 400. Lax is enough: the form token itself still blocks cross-site forgery.
        services.AddAntiforgery(o =>
        {
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.HttpOnly = true;
        });

        services.AddAuthorization();
        services.Configure<RazorPagesOptions>(o =>
        {
            o.Conventions.AuthorizeFolder("/Admin");
            o.Conventions.AllowAnonymousToPage("/Admin/Login");
        });
        return services;
    }

    /// <summary>Add after UseForwardedHeaders and before UseAuthentication.</summary>
    public static WebApplication UseAdminSecurity(this WebApplication app)
    {
        var isDevelopment = app.Environment.IsDevelopment();

        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/Admin"))
            {
                await next();
                return;
            }

            var options = ctx.RequestServices.GetRequiredService<IOptionsMonitor<AdminOptions>>().CurrentValue;
            var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSecurity");

            // No account configured: the admin does not exist. Fails closed.
            if (!options.IsConfigured)
            {
                log.LogWarning("Admin is disabled: Admin:Username / Admin:PasswordHash are not set.");
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (isDevelopment)
            {
                // Same rule as before: your own computer only.
                if (ctx.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }
            else
            {
                // Production: only through the Cloudflare Worker. Missing setting = closed.
                if (string.IsNullOrEmpty(options.OriginSecret))
                {
                    log.LogWarning("Admin is disabled: Admin:OriginSecret is not set.");
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                if (!HasValidOriginSecret(ctx, options))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }

            var h = ctx.Response.Headers;
            h.CacheControl = "no-store, no-cache, max-age=0";
            h.Pragma = "no-cache";
            h["X-Robots-Tag"] = "noindex, nofollow";
            h["X-Frame-Options"] = "DENY";
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "no-referrer";
            h["Content-Security-Policy"] = Csp;

            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>True when the request carries the correct X-Origin-Secret (compared in constant time).</summary>
    public static bool HasValidOriginSecret(HttpContext ctx, AdminOptions options)
    {
        if (string.IsNullOrEmpty(options.OriginSecret)) return false;
        var sent = ctx.Request.Headers[OriginHeader].ToString();
        if (sent.Length == 0) return false;

        // Hash both sides first so the comparison takes the same time whatever the lengths.
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(sent));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(options.OriginSecret));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// The visitor's IP for rate limiting. The Worker's X-Client-Ip / CF-Connecting-IP is believed
    /// only when the origin secret is valid, otherwise anyone could fake it.
    /// </summary>
    public static string ClientIp(HttpContext ctx, AdminOptions options)
    {
        if (HasValidOriginSecret(ctx, options))
        {
            foreach (var name in new[] { "X-Client-Ip", "CF-Connecting-IP" })
            {
                var value = ctx.Request.Headers[name].ToString();
                if (IPAddress.TryParse(value, out var ip)) return ip.ToString();
            }
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Changes whenever the password hash changes, so changing the password signs everyone out.</summary>
    public static string StampFor(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext ctx)
    {
        var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AdminOptions>>().CurrentValue;

        var valid = options.IsConfigured
            && ctx.Principal?.FindFirst(StampClaim)?.Value == StampFor(options.PasswordHash!)
            && ctx.Properties.Items.TryGetValue(LoginAtKey, out var raw)
            && long.TryParse(raw, out var seconds)
            && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds) < MaxSessionAge;

        if (!valid)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
