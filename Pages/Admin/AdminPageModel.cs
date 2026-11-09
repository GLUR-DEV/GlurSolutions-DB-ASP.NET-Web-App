using GlurSolutions.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace GlurSolutions.Web.Pages.Admin;

/// <summary>Base class for the admin pages: one-time messages shown at the top of the next page.</summary>
public abstract class AdminPageModel : PageModel
{
    /// <summary>Problems found when a whole page was saved. When this is not empty nothing was saved,
    /// and the page shows the list above the form with everything the admin typed still in place.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>A green "saved" message, shown once after a redirect.</summary>
    protected void Flash(string message) => TempData["Message"] = message;

    /// <summary>A red error message, shown once after a redirect.</summary>
    protected void Fail(string message) => TempData["Error"] = message;

    /// <summary>"2 updated, 1 added, 1 deleted" for the message after a one-button save.</summary>
    protected static string Summarise(string what, int updated, int added, int deleted)
    {
        var parts = new List<string>();
        if (updated > 0) parts.Add($"{updated} updated");
        if (added > 0) parts.Add($"{added} added");
        if (deleted > 0) parts.Add($"{deleted} deleted");
        return $"Saved {what}: {string.Join(", ", parts)}.";
    }

    /// <summary>Rewrites the saved copy of the public site (Azure Storage) so it matches the database.
    /// Call right after SaveChangesAsync. A failure never loses the edit; it just shows a warning.</summary>
    protected async Task RefreshSnapshotAsync()
    {
        var ok = await HttpContext.RequestServices.GetRequiredService<SnapshotService>().TryRefreshAsync();
        if (!ok)
            Fail("Saved, but the cached copy of the site could not be updated. It will catch up on the next save.");
    }
}

/// <summary>Checks shared by several admin pages.</summary>
public static class AdminValidation
{
    // Same rules as the picture check in the website's content.js: a full https:// address, or a plain path on the site.
    private static readonly Regex HttpsUrl = new(@"^https://[^\s""'()<>]+$", RegexOptions.IgnoreCase);
    private static readonly Regex RelativePath = new(@"^[\w\-./]+$");

    /// <summary>Empty is fine (the website shows a placeholder). Otherwise it must be a safe picture address.</summary>
    public static bool ImagePathOk(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (HttpsUrl.IsMatch(path)) return true;
        return RelativePath.IsMatch(path) && !path.Contains("..");
    }

    /// <summary>Empty is fine (the button is hidden). Otherwise a full https:// address.</summary>
    public static bool LinkOk(string? url) =>
        string.IsNullOrWhiteSpace(url) || HttpsUrl.IsMatch(url.Trim());

    public const string LinkMessage = "Use a full https:// address, or leave it empty to hide the button.";

    public const string ImagePathMessage =
        "Use a full https:// address (for example from Azure Blob Storage), or leave it empty for the placeholder.";

    /// <summary>Lower-case words joined by single hyphens, e.g. "my-project-2".</summary>
    public static readonly Regex Slug = new(@"^[a-z0-9]+(-[a-z0-9]+)*$");

    /// <summary>Trims text; returns null when it is empty.</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
