using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace GlurSolutions.Web.Security;

/// <summary>The admin sign-in cookie: what goes in it and when it stops being valid.</summary>
public static class AdminAuth
{
    public const string Scheme = "GlurAdmin";

    /// <summary>Idle sessions end after 30 minutes (see Program.cs); no session lasts longer than this, however active.</summary>
    public static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(8);

    // A short fingerprint of the current password hash. Putting it in the cookie means changing the password
    // instantly signs out every existing session (including a stolen one).
    public static string StampFor(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("stamp:" + passwordHash)))[..16];

    public static ClaimsPrincipal CreatePrincipal(string username, string passwordHash)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("stamp", StampFor(passwordHash)),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>Runs on every request with a cookie: rejects it if the account, password or age no longer match.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminSettings>>().Value;
        var principal = context.Principal;

        var valid = false;
        if (principal is not null && settings.HasLogin
            && string.Equals(principal.Identity?.Name, settings.Username, StringComparison.Ordinal)
            && principal.FindFirst("stamp")?.Value == StampFor(settings.PasswordHash!)
            && long.TryParse(principal.FindFirst("auth_time")?.Value, out var issuedAt))
        {
            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAt);
            valid = age >= TimeSpan.Zero && age < MaxSessionAge;
        }

        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }
}
