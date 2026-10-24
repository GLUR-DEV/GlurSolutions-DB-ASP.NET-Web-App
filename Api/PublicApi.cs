using GlurSolutions.Web.Data;
using GlurSolutions.Web.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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

        // Cheap check that the API is up and can reach the database (handy for Azure's health probe).
        // It is outside the key check below so Azure can call it without the key.
        // On failure it says which kind of problem it is (the error type and, for SQL errors, Microsoft's error
        // number, e.g. 18456 login failed, 40615 firewall, 4060 database not found, 208 table missing),
        // but never the message, so no server names, IPs or passwords are exposed.
        api.MapGet("/health", async (AppDbContext db, ILogger<AppDbContext> logger, CancellationToken ct) =>
        {
            try
            {
                await db.Projects.AsNoTracking().AnyAsync(ct);   // proves the connection works AND the tables exist
                return Results.Json(new { status = "ok" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed");
                var root = ex is Microsoft.Data.SqlClient.SqlException ? ex : ex.InnerException ?? ex;
                return Results.Json(
                    new
                    {
                        status = "error",
                        type = root.GetType().Name,
                        sqlError = (root as Microsoft.Data.SqlClient.SqlException)?.Number
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        // Everything below needs the shared key (Api:Key) when one is configured. The Cloudflare Worker
        // sends it in the X-Api-Key header, so only your site can read the content directly.
        var content = api.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var expected = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Api:Key"];
            if (!string.IsNullOrEmpty(expected))
            {
                var supplied = context.HttpContext.Request.Headers["X-Api-Key"].ToString();
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
                    return Results.Unauthorized();
            }
            return await next(context);
        });

        content.MapGet("/technologies", (SiteDataBuilder b, CancellationToken ct) =>
            b.GetTechnologiesAsync(ct));

        content.MapGet("/projects", (SiteDataBuilder b, CancellationToken ct) =>
            b.GetProjectsAsync(ct));

        content.MapGet("/projects/{slug}", async (string slug, SiteDataBuilder b, CancellationToken ct) =>
        {
            var project = await b.GetProjectAsync(slug, ct);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });
    }
}
