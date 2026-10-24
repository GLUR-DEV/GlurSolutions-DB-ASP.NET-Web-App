using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The slideshows of one project (normally one "Process" and one "Improvement"); the pictures live on the Slides page
public class SlideshowsModel(AppDbContext db) : AdminPageModel
{
    public record Row(int Id, SlideshowKind Kind, string Heading, int SortOrder, int Slides);

    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public List<Row> Slideshows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int projectId)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return NotFound();

        ProjectId = project.Id;
        ProjectTitle = project.Title;
        Slideshows = await db.Slideshows.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new Row(s.Id, s.Kind, s.Heading, s.SortOrder, s.Slides.Count))
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(int projectId, SlideshowKind kind, string? heading, int sortOrder)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(heading) || heading.Trim().Length > 120)
        {
            Fail("Enter a heading of up to 120 characters.");
            return RedirectToPage(new { projectId });
        }

        var slideshow = new Slideshow { ProjectId = projectId, Kind = kind, Heading = heading.Trim(), SortOrder = sortOrder };
        db.Slideshows.Add(slideshow);
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash("Slideshow added. Now add its slides.");
        return RedirectToPage("Slides", new { slideshowId = slideshow.Id });
    }

    public async Task<IActionResult> OnPostSaveAsync(int projectId, int id, SlideshowKind kind, string? heading, int sortOrder)
    {
        var slideshow = await db.Slideshows.FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
        if (slideshow is null) return NotFound();
        if (string.IsNullOrWhiteSpace(heading) || heading.Trim().Length > 120)
        {
            Fail("Enter a heading of up to 120 characters.");
            return RedirectToPage(new { projectId });
        }

        slideshow.Kind = kind;
        slideshow.Heading = heading.Trim();
        slideshow.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash("Slideshow saved.");
        return RedirectToPage(new { projectId });
    }

    // Deleting a slideshow also deletes its slides (cascade in the database)
    public async Task<IActionResult> OnPostDeleteAsync(int projectId, int id)
    {
        var slideshow = await db.Slideshows.FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
        if (slideshow is null) return NotFound();

        db.Slideshows.Remove(slideshow);
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash("Slideshow deleted.");
        return RedirectToPage(new { projectId });
    }
}
