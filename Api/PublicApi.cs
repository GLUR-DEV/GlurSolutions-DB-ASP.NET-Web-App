using GlurSolutions.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Api;

// Read-only JSON for the live Cloudflare site. Only published projects are ever returned,
// and nothing here can change data: writes stay in the admin app.

public record TechnologyDto(string Key, string Label);

public record ProjectCardDto(
    string Slug, string Title, string ShortDescription,
    string? CoverImagePath, string? CoverAlt,
    IReadOnlyList<TechnologyDto> Technologies);

public record SlideDto(string StageLabel, string Title, string Caption, string? ImagePath, string? ImageAlt);

public record SlideshowDto(string Kind, string Heading, IReadOnlyList<SlideDto> Slides);

public record NoteDto(string Kind, string Heading, string Body);

public record ProjectDetailDto(
    string Slug, string Title, string ShortDescription, string? Summary,
    string? CoverImagePath, string? CoverAlt,
    string? Role, string? Tools, string? Timeframe, DateTime UpdatedUtc,
    IReadOnlyList<TechnologyDto> Technologies,
    IReadOnlyList<SlideshowDto> Slideshows,
    IReadOnlyList<NoteDto> Notes);

public static class PublicApi
{
    public const string CorsPolicy = "PublicSite";
    public const string RateLimitPolicy = "PublicApi";

    public static void MapPublicApi(this WebApplication app)
    {
        var api = app.MapGroup("/api")
            .RequireCors(CorsPolicy)
            .RequireRateLimiting(RateLimitPolicy);

        // Cheap check that the API is up and can reach the database (handy for Azure's health probe)
        api.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "ok" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        // Landing page: the Skills pills
        api.MapGet("/technologies", async (AppDbContext db, CancellationToken ct) =>
            await db.Technologies.AsNoTracking()
                .OrderBy(t => t.SortOrder)
                .Select(t => new TechnologyDto(t.Key, t.Label))
                .ToListAsync(ct));

        // Landing page: the project cards
        api.MapGet("/projects", async (AppDbContext db, CancellationToken ct) =>
            await db.Projects.AsNoTracking()
                .Where(p => p.IsPublished)
                .OrderBy(p => p.SortOrder)
                .Select(p => new ProjectCardDto(
                    p.Slug, p.Title, p.ShortDescription, p.CoverImagePath, p.CoverAlt,
                    p.ProjectTechnologies
                        .OrderBy(pt => pt.Technology.SortOrder)
                        .Select(pt => new TechnologyDto(pt.Technology.Key, pt.Technology.Label))
                        .ToList()))
                .ToListAsync(ct));

        // Project page: everything for one project, by its slug (e.g. /api/projects/project-one)
        api.MapGet("/projects/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.AsNoTracking()
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

            return project is null ? Results.NotFound() : Results.Ok(project);
        });
    }
}
