using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The one-line notes in the side panels of a project page: "Feedback" and "What improved".
// One Save button saves every note on the page.
public class NotesModel(AppDbContext db) : AdminPageModel
{
    public class NoteRow
    {
        public int Id { get; set; }              // 0 = a new note
        public NoteKind Kind { get; set; }
        public string? Heading { get; set; }
        public string? Body { get; set; }
        public int SortOrder { get; set; }
        public bool Delete { get; set; }
    }

    [BindProperty] public List<NoteRow> Rows { get; set; } = [];

    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public string ProjectSlug { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int projectId)
    {
        if (!await LoadHeaderAsync(projectId)) return NotFound();

        var notes = await db.ProjectNotes.AsNoTracking()
            .Where(n => n.ProjectId == projectId)
            .OrderBy(n => n.Kind).ThenBy(n => n.SortOrder).ThenBy(n => n.Id)
            .ToListAsync();

        Rows = notes.Select(n => new NoteRow { Id = n.Id, Kind = n.Kind, Heading = n.Heading, Body = n.Body, SortOrder = n.SortOrder }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int projectId)
    {
        if (!await LoadHeaderAsync(projectId)) return NotFound();

        var existing = await db.ProjectNotes.Where(n => n.ProjectId == projectId).ToListAsync();
        var byId = existing.ToDictionary(n => n.Id);

        var toAdd = new List<(NoteKind Kind, string Heading, string Body, int Sort)>();
        var toChange = new List<(ProjectNote Note, NoteKind Kind, string Heading, string Body, int Sort)>();
        var toRemove = new List<ProjectNote>();

        var position = 0;
        foreach (var row in Rows)
        {
            position++;
            ProjectNote? current = null;
            if (row.Id != 0 && !byId.TryGetValue(row.Id, out current)) continue;   // already gone (or not this project's)

            if (row.Delete)
            {
                if (current is not null) toRemove.Add(current);
                continue;
            }

            var heading = row.Heading?.Trim();
            var body = row.Body?.Trim();
            if (current is null && string.IsNullOrEmpty(heading) && string.IsNullOrEmpty(body)) continue;   // an empty "new" row

            if (string.IsNullOrEmpty(heading) || heading.Length > 80)
            {
                Errors.Add($"Note {position}: enter a heading of up to 80 characters.");
                continue;
            }
            if (string.IsNullOrEmpty(body) || body.Length > 400)
            {
                Errors.Add($"Note {position} (\"{heading}\"): enter text of up to 400 characters.");
                continue;
            }

            if (current is null) toAdd.Add((row.Kind, heading, body, row.SortOrder));
            else toChange.Add((current, row.Kind, heading, body, row.SortOrder));
        }

        if (Errors.Count > 0) return Page();   // nothing saved; the form keeps what was typed

        var updated = 0;
        foreach (var (note, kind, heading, body, sort) in toChange)
        {
            if (note.Kind == kind && note.Heading == heading && note.Body == body && note.SortOrder == sort) continue;
            note.Kind = kind;
            note.Heading = heading;
            note.Body = body;
            note.SortOrder = sort;
            updated++;
        }
        foreach (var (kind, heading, body, sort) in toAdd)
            db.ProjectNotes.Add(new ProjectNote { ProjectId = projectId, Kind = kind, Heading = heading, Body = body, SortOrder = sort });
        db.ProjectNotes.RemoveRange(toRemove);

        if (updated + toAdd.Count + toRemove.Count == 0)
        {
            Flash("No changes to save.");
            return RedirectToPage(new { projectId });
        }

        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash(Summarise("notes", updated, toAdd.Count, toRemove.Count));
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
