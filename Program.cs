using GlurSolutions.Web;
using GlurSolutions.Web.Data;
using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
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
// The database is only used by the signed-in admin pages (visitors read the JSON snapshots in Blob storage).
// Two settings keep it asleep the rest of the time:
//  - Pooling = false: every database call opens and closes its own connection, so nothing stays connected after a
//    request finishes. An idle pooled connection counts as an open session and stops Azure SQL serverless from
//    auto-pausing. Only one person uses the admin, so the extra connect time does not matter.
//  - ConnectTimeout of at least 60 s: the first call after a pause waits for the database to wake up.
// EnableRetryOnFailure also matters for Azure SQL: the first connection after a pause can fail once while it wakes.
var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
if (connectionStringBuilder.ConnectTimeout < 60) connectionStringBuilder.ConnectTimeout = 60;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionStringBuilder.ConnectionString, sql => sql.EnableRetryOnFailure()));

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

// /Admin: 404 unless the request came through the Cloudflare Worker (X-Origin-Secret) or, in Development, from this
// computer; then a login is required. Also adds UseAuthentication and UseAuthorization, so they are not repeated here.
app.UseAdminSecurity();
app.MapRazorPages();

// Does not touch the database, so uptime checks and Azure's keep-warm pings can never wake it.
app.MapGet("/healthz", () => Results.Text("ok")).AllowAnonymous();

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
