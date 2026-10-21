using GlurSolutions.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

public class AdminIndexModel(AppDbContext db) : AdminPageModel
{
    public record Row(int Id, string Slug, string Title, bool IsPublished, int SortOrder,
        int Slideshows, int Slides, int Notes, DateTime UpdatedUtc);

    public List<Row> Projects { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Projects = await db.Projects
            .AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new Row(
                p.Id, p.Slug, p.Title, p.IsPublished, p.SortOrder,
                p.Slideshows.Count,
                p.Slideshows.SelectMany(s => s.Slides).Count(),
                p.Notes.Count,
                p.UpdatedUtc))
            .ToListAsync();
    }

    // Publish or hide a project on the live website
    public async Task<IActionResult> OnPostTogglePublishAsync(int id)
    {
        var project = await db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        project.IsPublished = !project.IsPublished;
        await db.SaveChangesAsync();
        Flash($"\"{project.Title}\" is now {(project.IsPublished ? "published" : "a draft (hidden from the website)")}.");
        return RedirectToPage();
    }

    // Deleting a project also deletes its slideshows, slides, notes and skill links (cascade in the database)
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var project = await db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        Flash($"Deleted \"{project.Title}\".");
        return RedirectToPage();
    }
}
