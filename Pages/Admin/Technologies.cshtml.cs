using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The skills: the pills on the home page and the tags on project cards
public class TechnologiesModel(AppDbContext db) : AdminPageModel
{
    public record Row(int Id, string Key, string Label, int SortOrder, int Uses);

    public List<Row> Technologies { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Technologies = await db.Technologies
            .AsNoTracking()
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .Select(t => new Row(t.Id, t.Key, t.Label, t.SortOrder, t.ProjectTechnologies.Count))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(string? key, string? label, int sortOrder)
    {
        var problem = await CheckAsync(key, label, null);
        if (problem is not null) { Fail(problem); return RedirectToPage(); }

        db.Technologies.Add(new Technology { Key = key!.Trim().ToLowerInvariant(), Label = label!.Trim(), SortOrder = sortOrder });
        await db.SaveChangesAsync();
        Flash($"Added \"{label!.Trim()}\".");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(int id, string? key, string? label, int sortOrder)
    {
        var technology = await db.Technologies.FindAsync(id);
        if (technology is null) return NotFound();

        var problem = await CheckAsync(key, label, id);
        if (problem is not null) { Fail(problem); return RedirectToPage(); }

        technology.Key = key!.Trim().ToLowerInvariant();
        technology.Label = label!.Trim();
        technology.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        Flash($"Saved \"{technology.Label}\".");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var technology = await db.Technologies.FindAsync(id);
        if (technology is null) return NotFound();

        // The database refuses to delete a skill that projects still use, so say so nicely instead of failing
        if (await db.ProjectTechnologies.AnyAsync(pt => pt.TechnologyId == id))
        {
            Fail($"\"{technology.Label}\" is still used by at least one project. Untick it on those projects first.");
            return RedirectToPage();
        }

        db.Technologies.Remove(technology);
        await db.SaveChangesAsync();
        Flash($"Deleted \"{technology.Label}\".");
        return RedirectToPage();
    }

    // Returns a message when the values are not acceptable, otherwise null
    private async Task<string?> CheckAsync(string? key, string? label, int? ownId)
    {
        key = key?.Trim().ToLowerInvariant();
        label = label?.Trim();

        if (string.IsNullOrEmpty(key) || key.Length > 40 || !AdminValidation.Slug.IsMatch(key))
            return "The key must be lower-case letters, numbers and single hyphens (max 40), e.g. html-css-js.";
        if (string.IsNullOrEmpty(label) || label.Length > 60)
            return "Enter a label of up to 60 characters.";

        var ownIdValue = ownId ?? 0;
        if (await db.Technologies.AnyAsync(t => t.Key == key && t.Id != ownIdValue))
            return $"Another skill already uses the key \"{key}\".";
        return null;
    }
}
