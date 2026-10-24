using GlurSolutions.Web.Api;
using GlurSolutions.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Services;

public class SiteDataBuilder(AppDbContext db)
{
    public Task<List<TechnologyDto>> GetTechnologiesAsync(CancellationToken ct = default) =>
        db.Technologies.AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .Select(t => new TechnologyDto(t.Key, t.Label))
            .ToListAsync(ct);

    public Task<List<ProjectCardDto>> GetProjectsAsync(CancellationToken ct = default) =>
        db.Projects.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.SortOrder)
            .Select(p => new ProjectCardDto(
                p.Slug, p.Title, p.ShortDescription, p.CoverImagePath, p.CoverAlt,
                p.ProjectTechnologies
                    .OrderBy(pt => pt.Technology.SortOrder)
                    .Select(pt => new TechnologyDto(pt.Technology.Key, pt.Technology.Label))
                    .ToList()))
            .ToListAsync(ct);

    public Task<ProjectDetailDto?> GetProjectAsync(string slug, CancellationToken ct = default) =>
        db.Projects.AsNoTracking()
            .Where(p => p.IsPublished && p.Slug == slug)
            .Select(p => new ProjectDetailDto(
                p.Slug, p.Title, p.ShortDescription, p.Summary,
                p.CoverImagePath, p.CoverAlt,
                p.Role, p.Tools, p.Timeframe, p.UpdatedUtc,
                p.ProjectTechnologies
                    .OrderBy(pt => pt.Technology.SortOrder)
                    .Select(pt => new TechnologyDto(pt.Technology.Key, pt.Technology.Label))
                    .ToList(),
                p.Slideshows
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new SlideshowDto(
                        s.Kind.ToString(), s.Heading,
                        s.Slides.OrderBy(sl => sl.SortOrder)
                            .Select(sl => new SlideDto(sl.StageLabel, sl.Title, sl.Caption, sl.ImagePath, sl.ImageAlt))
                            .ToList()))
                    .ToList(),
                p.Notes
                    .OrderBy(n => n.SortOrder)
                    .Select(n => new NoteDto(n.Kind.ToString(), n.Heading, n.Body))
                    .ToList()))
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
}