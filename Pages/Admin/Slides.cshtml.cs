using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The pictures of one slideshow: each slide has a stage button, a title, a caption and a picture.
// One Save button saves every slide on the page.
public class SlidesModel(AppDbContext db) : AdminPageModel
{
    public class SlideRow
    {
        public int Id { get; set; }              // 0 = a new slide
        public string? StageLabel { get; set; }
        public string? Title { get; set; }
        public string? Caption { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageAlt { get; set; }
        public int SortOrder { get; set; }
        public bool Delete { get; set; }
    }

    [BindProperty] public List<SlideRow> Rows { get; set; } = [];

    public int SlideshowId { get; private set; }
    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public string ProjectSlug { get; private set; } = "";
    public string SlideshowHeading { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(int slideshowId)
    {
        if (!await LoadHeaderAsync(slideshowId)) return NotFound();

        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.SlideshowId == slideshowId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync();

        Rows = slides.Select(s => new SlideRow
        {
            Id = s.Id, StageLabel = s.StageLabel, Title = s.Title, Caption = s.Caption,
            ImagePath = s.ImagePath, ImageAlt = s.ImageAlt, SortOrder = s.SortOrder
        }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int slideshowId)
    {
        if (!await LoadHeaderAsync(slideshowId)) return NotFound();

        var existing = await db.Slides.Where(s => s.SlideshowId == slideshowId).ToListAsync();
        var byId = existing.ToDictionary(s => s.Id);

        var toAdd = new List<SlideRow>();
        var toChange = new List<(Slide Slide, SlideRow Row)>();
        var toRemove = new List<Slide>();

        var position = 0;
        foreach (var row in Rows)
        {
            position++;
            Slide? current = null;
            if (row.Id != 0 && !byId.TryGetValue(row.Id, out current)) continue;   // already gone (or not this slideshow's)

            if (row.Delete)
            {
                if (current is not null) toRemove.Add(current);
                continue;
            }

            if (current is null && IsBlank(row)) continue;   // an empty "new" row

            var problem = Check(row.StageLabel, row.Title, row.Caption, row.ImagePath, row.ImageAlt);
            if (problem is not null)
            {
                var name = string.IsNullOrWhiteSpace(row.StageLabel) ? $"Slide {position}" : $"Slide {position} (\"{row.StageLabel.Trim()}\")";
                Errors.Add($"{name}: {problem}");
                continue;
            }

            if (current is null) toAdd.Add(row);
            else toChange.Add((current, row));
        }

        if (Errors.Count > 0) return Page();   // nothing saved; the form keeps what was typed

        var updated = 0;
        foreach (var (slide, row) in toChange)
        {
            var stage = row.StageLabel!.Trim();
            var title = row.Title!.Trim();
            var caption = row.Caption!.Trim();
            var path = AdminValidation.Clean(row.ImagePath);
            var alt = AdminValidation.Clean(row.ImageAlt);
            if (slide.StageLabel == stage && slide.Title == title && slide.Caption == caption &&
                slide.ImagePath == path && slide.ImageAlt == alt && slide.SortOrder == row.SortOrder) continue;

            slide.StageLabel = stage;
            slide.Title = title;
            slide.Caption = caption;
            slide.ImagePath = path;
            slide.ImageAlt = alt;
            slide.SortOrder = row.SortOrder;
            updated++;
        }
        foreach (var row in toAdd)
        {
            db.Slides.Add(new Slide
            {
                SlideshowId = slideshowId, StageLabel = row.StageLabel!.Trim(), Title = row.Title!.Trim(), Caption = row.Caption!.Trim(),
                ImagePath = AdminValidation.Clean(row.ImagePath), ImageAlt = AdminValidation.Clean(row.ImageAlt), SortOrder = row.SortOrder
            });
        }
        db.Slides.RemoveRange(toRemove);

        if (updated + toAdd.Count + toRemove.Count == 0)
        {
            Flash("No changes to save.");
            return RedirectToPage(new { slideshowId });
        }

        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash(Summarise("slides", updated, toAdd.Count, toRemove.Count));
        return RedirectToPage(new { slideshowId });
    }

    private async Task<bool> LoadHeaderAsync(int slideshowId)
    {
        var slideshow = await db.Slideshows.AsNoTracking()
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == slideshowId);
        if (slideshow is null) return false;

        SlideshowId = slideshow.Id;
        ProjectId = slideshow.ProjectId;
        ProjectTitle = slideshow.Project.Title;
        ProjectSlug = slideshow.Project.Slug;
        SlideshowHeading = slideshow.Heading;
        return true;
    }

    private static bool IsBlank(SlideRow r) =>
        string.IsNullOrWhiteSpace(r.StageLabel) && string.IsNullOrWhiteSpace(r.Title) && string.IsNullOrWhiteSpace(r.Caption) &&
        string.IsNullOrWhiteSpace(r.ImagePath) && string.IsNullOrWhiteSpace(r.ImageAlt);

    private static string? Check(string? stageLabel, string? title, string? caption, string? imagePath, string? imageAlt)
    {
        if (string.IsNullOrWhiteSpace(stageLabel) || stageLabel.Trim().Length > 60) return "Enter a stage button label of up to 60 characters.";
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120) return "Enter a title of up to 120 characters.";
        if (string.IsNullOrWhiteSpace(caption) || caption.Trim().Length > 600) return "Enter a caption of up to 600 characters.";
        if (imagePath is { Length: > 260 }) return "The picture address is too long (max 260 characters).";
        if (!AdminValidation.ImagePathOk(imagePath)) return AdminValidation.ImagePathMessage;
        if (imageAlt is { Length: > 200 }) return "The picture description is too long (max 200 characters).";
        return null;
    }
}
