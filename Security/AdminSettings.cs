namespace GlurSolutions.Web.Security;

/// <summary>
/// The admin login settings, read from the "Admin" section of configuration. None of these may be committed to git:
/// locally they live in user-secrets, on Azure in App Service environment variables (Admin__Username, ...).
/// </summary>
public class AdminSettings
{
    public const string Section = "Admin";

    /// <summary>The one admin account's user name.</summary>
    public string? Username { get; set; }

    /// <summary>The PBKDF2 hash of the password, made with: dotnet run -- hash-password. Never the password itself.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// A long random value that only the Cloudflare Worker knows (made with: dotnet run -- generate-secret).
    /// On Azure the admin answers only requests that carry it, so the Azure address on its own shows nothing.
    /// </summary>
    public string? OriginSecret { get; set; }

    public bool HasLogin => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(PasswordHash);

    public bool HasOriginSecret => OriginSecret is { Length: >= 32 };

    /// <summary>
    /// The admin area is switched on only when fully configured (fail closed). On Azure that includes the origin
    /// secret; on your own computer (Development) the login alone is enough.
    /// </summary>
    public bool IsEnabled(bool isDevelopment) => HasLogin && (isDevelopment || HasOriginSecret);
}
