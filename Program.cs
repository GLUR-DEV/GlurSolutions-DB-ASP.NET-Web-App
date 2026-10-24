using System.Threading.RateLimiting;
using GlurSolutions.Web;
using GlurSolutions.Web.Api;
using GlurSolutions.Web.Data;
using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using GlurSolutions.Web.Services;

// dotnet run -- hash-password : prints a password hash for the admin login, then exits (no database needed)
if (args is ["hash-password"])
{
    HashPasswordCommand.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddScoped<SiteDataBuilder>();
builder.Services.AddScoped<SnapshotService>();

// Admin login: cookie sign-in, origin-secret gate and login lockout (see Security/AdminSecurity.cs)
builder.Services.AddAdminSecurity(builder.Configuration);

// The database. The connection string comes from appsettings.Development.json while developing.
// In production it must come from an environment variable or secret store, never from a file in git:
//   ConnectionStrings__DefaultConnection="Server=...;Database=GlurSolutions;User Id=...;Password=...;Encrypt=True"
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No connection string found. Set ConnectionStrings:DefaultConnection (see README).");
}
// EnableRetryOnFailure matters for Azure SQL: the free/serverless tier pauses when idle and the first
// connection after a pause can fail once while it wakes up.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

// The live site (glursolutions.com on Cloudflare) calls /api/* from the browser, so the browser needs
// permission via CORS. Only GET, and only from the origins listed in appsettings (Cors:AllowedOrigins).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PublicApi.CorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).WithMethods("GET").AllowAnyHeader()));

// Basic protection for the public API: 120 requests a minute per visitor IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PublicApi.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// Azure App Service / Cloudflare sit in front of the app; this makes the real visitor IP and https visible
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// While developing, create/update the database on start-up.
// In production run migrations deliberately (dotnet ef database update) instead of on start-up.
// The six placeholder projects are only added when Seed:Enabled is true (off by default). The database now holds
// your real content, and without this switch deleting every project in the admin would bring the placeholders back.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        await DbSeeder.SeedAsync(db);
    }
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();      // serves everything in wwwroot (your CSS and JS)
app.UseRouting();
app.UseCors();
app.UseRateLimiter();

// /Admin: 404 unless the request came through the Cloudflare Worker (X-Origin-Secret) or, in Development, from this
// computer; then a login is required. Also adds UseAuthentication and UseAuthorization, so they are not repeated here.
app.UseAdminSecurity();
app.MapRazorPages();
app.MapPublicApi();

app.MapPost("/internal/snapshot", async (HttpRequest req, SnapshotService svc, IConfiguration cfg) =>
{
    var expected = cfg["Snapshot:Key"] ?? "";
    var supplied = req.Headers["X-Snapshot-Key"].ToString();
    if (expected.Length == 0 ||
        !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
        return Results.Unauthorized();

    await svc.RefreshAsync();
    return Results.Ok(new { refreshed = DateTimeOffset.UtcNow });
}).AllowAnonymous();

app.Run();
