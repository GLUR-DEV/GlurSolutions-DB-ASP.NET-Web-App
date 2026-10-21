namespace GlurSolutions.Web.Security;

/// <summary>
/// Settings for the admin login. Bind from configuration section "Admin".
/// In Azure set them as app settings: Admin__Username, Admin__PasswordHash, Admin__OriginSecret.
/// Locally use user-secrets. Never put real values in appsettings.json or git.
/// </summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    /// <summary>The single admin account name.</summary>
    public string? Username { get; set; }

    /// <summary>Output of: dotnet run -- hash-password (never the plain password).</summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Shared secret the Cloudflare Worker sends in the X-Origin-Secret header.
    /// Required outside Development, so the *.azurewebsites.net address alone never shows the admin.
    /// </summary>
    public string? OriginSecret { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(PasswordHash);
}
