using System.ComponentModel.DataAnnotations;
using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// Create or edit one project: /Admin/Project (new) or /Admin/Project/5 (edit)
public class ProjectModel(AppDbContext db) : AdminPageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "Enter a slug."), StringLength(80)]
        [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lower-case letters, numbers and single hyphens, e.g. my-project.")]
        public string Slug { get; set; } = "";

        [Required(ErrorMessage = "Enter a title."), StringLength(120)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Enter a short description."), StringLength(300)]
        public string ShortDescription { get; set; } = "";

        [StringLength(1000)] public string? Summary { get; set; }
        [StringLength(260)] public string? CoverImagePath { get; set; }
        [StringLength(200)] public string? CoverAlt { get; set; }
        [StringLength(120)] public string? Role { get; set; }
        [StringLength(200)] public string? Tools { get; set; }
        [StringLength(80)] public string? Timeframe { get; set; }

        public int SortOrder { get; set; }
        public bool IsPublished { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public List<int> SelectedTechnologyIds { get; set; } = [];

    public int? ProjectId { get; private set; }
    public bool IsNew => ProjectId is null;
    public List<Technology> AllTechnologies { get; private set; } = [];
    public int SlideshowCount { get; private set; }
    public int SlideCount { get; private set; }
    public int NoteCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        ProjectId = id;
        await LoadTechnologiesAsync();

        if (id is null)
        {
            var highest = await db.Projects.MaxAsync(p => (int?)p.SortOrder) ?? 0;
            Input = new InputModel { SortOrder = highest + 1 };
            return Page();
        }

        var project = await db.Projects
            .AsNoTracking()
            .Include(p => p.ProjectTechnologies)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        Input = new InputModel
        {
            Slug = project.Slug, Title = project.Title, ShortDescription = project.ShortDescription,
            Summary = project.Summary, CoverImagePath = project.CoverImagePath, CoverAlt = project.CoverAlt,
            Role = project.Role, Tools = project.Tools, Timeframe = project.Timeframe,
            SortOrder = project.SortOrder, IsPublished = project.IsPublished
        };
        SelectedTechnologyIds = project.ProjectTechnologies.Select(pt => pt.TechnologyId).ToList();
        await LoadCountsAsync(project.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        ProjectId = id;
        await LoadTechnologiesAsync();

        if (!AdminValidation.ImagePathOk(Input.CoverImagePath))
            ModelState.AddModelError("Input.CoverImagePath", AdminValidation.ImagePathMessage);

        var ownId = id ?? 0;
        if (await db.Projects.AnyAsync(p => p.Slug == Input.Slug && p.Id != ownId))
            ModelState.AddModelError("Input.Slug", "Another project already uses that slug.");

        if (!ModelState.IsValid)
        {
            if (id is not null) await LoadCountsAsync(id.Value);
            return Page();
        }

        Project project;
        if (id is null)
        {
            project = new Project();
            db.Projects.Add(project);
        }
        else
        {
            var existing = await db.Projects
                .Include(p => p.ProjectTechnologies)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (existing is null) return NotFound();
            project = existing;
        }

        project.Slug = Input.Slug;
        project.Title = Input.Title.Trim();
        project.ShortDescription = Input.ShortDescription.Trim();
        project.Summary = AdminValidation.Clean(Input.Summary);
        project.CoverImagePath = AdminValidation.Clean(Input.CoverImagePath);
        project.CoverAlt = AdminValidation.Clean(Input.CoverAlt);
        project.Role = AdminValidation.Clean(Input.Role);
        project.Tools = AdminValidation.Clean(Input.Tools);
        project.Timeframe = AdminValidation.Clean(Input.Timeframe);
        project.SortOrder = Input.SortOrder;
        project.IsPublished = Input.IsPublished;

        // Skills: add the newly ticked ones, remove the unticked ones (ignoring ids that don't exist)
        var validIds = AllTechnologies.Select(t => t.Id).ToHashSet();
        var chosen = SelectedTechnologyIds.Where(validIds.Contains).Distinct().ToList();
        var stale = project.ProjectTechnologies.Where(pt => !chosen.Contains(pt.TechnologyId)).ToList();
        db.ProjectTechnologies.RemoveRange(stale);
        var already = project.ProjectTechnologies.Select(pt => pt.TechnologyId).ToHashSet();
        foreach (var technologyId in chosen.Where(t => !already.Contains(t)))
        {
            project.ProjectTechnologies.Add(new ProjectTechnology { TechnologyId = technologyId });
        }

        await db.SaveChangesAsync();
        Flash(id is null
            ? $"Created \"{project.Title}\" as a draft. Add slideshows and notes below, then publish it."
            : $"Saved \"{project.Title}\".");
        return RedirectToPage("Project", new { id = project.Id });
    }

    private async Task LoadTechnologiesAsync() =>
        AllTechnologies = await db.Technologies.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Label).ToListAsync();

    private async Task LoadCountsAsync(int projectId)
    {
        SlideshowCount = await db.Slideshows.CountAsync(s => s.ProjectId == projectId);
        SlideCount = await db.Slides.CountAsync(s => s.Slideshow.ProjectId == projectId);
        NoteCount = await db.ProjectNotes.CountAsync(n => n.ProjectId == projectId);
    }
}
