using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The slideshows of one project (normally one "Process" and one "Improvement"); the pictures live on the Slides page.
// One Save button saves every row on the page.
public class SlideshowsModel(AppDbContext db) : AdminPageModel
{
    public class SlideshowRow
    {
        public int Id { get; set; }              // 0 = a new slideshow
        public SlideshowKind Kind { get; set; }
        public string? Heading { get; set; }
        public int SortOrder { get; set; }
        public bool Delete { get; set; }
    }

    [BindProperty] public List<SlideshowRow> Rows { get; set; } = [];

    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public string ProjectSlug { get; private set; } = "";

    /// <summary>How many slides each slideshow has (slideshow id to count).</summary>
    public Dictionary<int, int> SlideCounts { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int projectId)
    {
        if (!await LoadHeaderAsync(projectId)) return NotFound();

        var items = await db.Slideshows.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Kind, s.Heading, s.SortOrder, Count = s.Slides.Count })
            .ToListAsync();

        Rows = items.Select(s => new SlideshowRow { Id = s.Id, Kind = s.Kind, Heading = s.Heading, SortOrder = s.SortOrder }).ToList();
        SlideCounts = items.ToDictionary(s => s.Id, s => s.Count);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int projectId)
    {
        if (!await LoadHeaderAsync(projectId)) return NotFound();

        var existing = await db.Slideshows.Where(s => s.ProjectId == projectId).ToListAsync();
        var byId = existing.ToDictionary(s => s.Id);
        SlideCounts = await db.Slideshows.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => new { s.Id, Count = s.Slides.Count })
            .ToDictionaryAsync(s => s.Id, s => s.Count);

        var toAdd = new List<(SlideshowKind Kind, string Heading, int Sort)>();
        var toChange = new List<(Slideshow Show, SlideshowKind Kind, string Heading, int Sort)>();
        var toRemove = new List<Slideshow>();

        var position = 0;
        foreach (var row in Rows)
        {
            position++;
            Slideshow? current = null;
            if (row.Id != 0 && !byId.TryGetValue(row.Id, out current)) continue;   // already gone (or not this project's)

            if (row.Delete)
            {
                if (current is not null) toRemove.Add(current);   // its slides go too (cascade in the database)
                continue;
            }

            var heading = row.Heading?.Trim();
            if (current is null && string.IsNullOrEmpty(heading)) continue;   // an empty "new" row

            if (string.IsNullOrEmpty(heading) || heading.Length > 120)
            {
                Errors.Add($"Slideshow {position}: enter a heading of up to 120 characters.");
                continue;
            }

            if (current is null) toAdd.Add((row.Kind, heading, row.SortOrder));
            else toChange.Add((current, row.Kind, heading, row.SortOrder));
        }

        if (Errors.Count > 0) return Page();   // nothing saved; the form keeps what was typed

        var updated = 0;
        foreach (var (show, kind, heading, sort) in toChange)
        {
            if (show.Kind == kind && show.Heading == heading && show.SortOrder == sort) continue;
            show.Kind = kind;
            show.Heading = heading;
            show.SortOrder = sort;
            updated++;
        }
        foreach (var (kind, heading, sort) in toAdd)
            db.Slideshows.Add(new Slideshow { ProjectId = projectId, Kind = kind, Heading = heading, SortOrder = sort });
        db.Slideshows.RemoveRange(toRemove);

        if (updated + toAdd.Count + toRemove.Count == 0)
        {
            Flash("No changes to save.");
            return RedirectToPage(new { projectId });
        }

        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash(Summarise("slideshows", updated, toAdd.Count, toRemove.Count));
        return RedirectToPage(new { projectId });
    }

    private async Task<bool> LoadHeaderAsync(int projectId)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return false;
        ProjectId = project.Id;
        ProjectTitle = project.Title;
        ProjectSlug = project.Slug;
        return true;
    }
}
