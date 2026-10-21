using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The one-line notes in the side panels of a project page: "Feedback" and "What improved"
public class NotesModel(AppDbContext db) : AdminPageModel
{
    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public List<ProjectNote> Notes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int projectId)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return NotFound();

        ProjectId = project.Id;
        ProjectTitle = project.Title;
        Notes = await db.ProjectNotes.AsNoTracking()
            .Where(n => n.ProjectId == projectId)
            .OrderBy(n => n.Kind).ThenBy(n => n.SortOrder).ThenBy(n => n.Id)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(int projectId, NoteKind kind, string? heading, string? body, int sortOrder)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return NotFound();
        var problem = Check(heading, body);
        if (problem is not null) { Fail(problem); return RedirectToPage(new { projectId }); }

        db.ProjectNotes.Add(new ProjectNote
        {
            ProjectId = projectId, Kind = kind, Heading = heading!.Trim(), Body = body!.Trim(), SortOrder = sortOrder
        });
        await db.SaveChangesAsync();
        Flash("Note added.");
        return RedirectToPage(new { projectId });
    }

    public async Task<IActionResult> OnPostSaveAsync(int projectId, int id, NoteKind kind, string? heading, string? body, int sortOrder)
    {
        var note = await db.ProjectNotes.FirstOrDefaultAsync(n => n.Id == id && n.ProjectId == projectId);
        if (note is null) return NotFound();
        var problem = Check(heading, body);
        if (problem is not null) { Fail(problem); return RedirectToPage(new { projectId }); }

        note.Kind = kind;
        note.Heading = heading!.Trim();
        note.Body = body!.Trim();
        note.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        Flash("Note saved.");
        return RedirectToPage(new { projectId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int projectId, int id)
    {
        var note = await db.ProjectNotes.FirstOrDefaultAsync(n => n.Id == id && n.ProjectId == projectId);
        if (note is null) return NotFound();

        db.ProjectNotes.Remove(note);
        await db.SaveChangesAsync();
        Flash("Note deleted.");
        return RedirectToPage(new { projectId });
    }

    private static string? Check(string? heading, string? body)
    {
        if (string.IsNullOrWhiteSpace(heading) || heading.Trim().Length > 80) return "Enter a heading of up to 80 characters.";
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 400) return "Enter text of up to 400 characters.";
        return null;
    }
}
