using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The pictures of one slideshow: each slide has a stage button, a title, a caption and a picture
public class SlidesModel(AppDbContext db) : AdminPageModel
{
    public int SlideshowId { get; private set; }
    public int ProjectId { get; private set; }
    public string ProjectTitle { get; private set; } = "";
    public string SlideshowHeading { get; private set; } = "";
    public List<Slide> Slides { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int slideshowId)
    {
        var slideshow = await db.Slideshows.AsNoTracking()
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == slideshowId);
        if (slideshow is null) return NotFound();

        SlideshowId = slideshow.Id;
        ProjectId = slideshow.ProjectId;
        ProjectTitle = slideshow.Project.Title;
        SlideshowHeading = slideshow.Heading;
        Slides = await db.Slides.AsNoTracking()
            .Where(s => s.SlideshowId == slideshowId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(int slideshowId, string? stageLabel, string? title, string? caption,
        string? imagePath, string? imageAlt, int sortOrder)
    {
        if (!await db.Slideshows.AnyAsync(s => s.Id == slideshowId)) return NotFound();
        var problem = Check(stageLabel, title, caption, imagePath, imageAlt);
        if (problem is not null) { Fail(problem); return RedirectToPage(new { slideshowId }); }

        db.Slides.Add(new Slide
        {
            SlideshowId = slideshowId, StageLabel = stageLabel!.Trim(), Title = title!.Trim(), Caption = caption!.Trim(),
            ImagePath = AdminValidation.Clean(imagePath), ImageAlt = AdminValidation.Clean(imageAlt), SortOrder = sortOrder
        });
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash("Slide added.");
        return RedirectToPage(new { slideshowId });
    }

    public async Task<IActionResult> OnPostSaveAsync(int slideshowId, int id, string? stageLabel, string? title, string? caption,
        string? imagePath, string? imageAlt, int sortOrder)
    {
        var slide = await db.Slides.FirstOrDefaultAsync(s => s.Id == id && s.SlideshowId == slideshowId);
        if (slide is null) return NotFound();
        var problem = Check(stageLabel, title, caption, imagePath, imageAlt);
        if (problem is not null) { Fail(problem); return RedirectToPage(new { slideshowId }); }

        slide.StageLabel = stageLabel!.Trim();
        slide.Title = title!.Trim();
        slide.Caption = caption!.Trim();
        slide.ImagePath = AdminValidation.Clean(imagePath);
        slide.ImageAlt = AdminValidation.Clean(imageAlt);
        slide.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash($"Saved slide \"{slide.StageLabel}\".");
        return RedirectToPage(new { slideshowId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int slideshowId, int id)
    {
        var slide = await db.Slides.FirstOrDefaultAsync(s => s.Id == id && s.SlideshowId == slideshowId);
        if (slide is null) return NotFound();

        db.Slides.Remove(slide);
        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash("Slide deleted.");
        return RedirectToPage(new { slideshowId });
    }

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
