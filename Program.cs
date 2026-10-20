using System.Threading.RateLimiting;
using GlurSolutions.Web.Api;
using GlurSolutions.Web.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

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

// While developing, create/update the database and fill it with the placeholder projects on start-up.
// In production run migrations deliberately (dotnet ef database update) instead of on start-up.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
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
app.UseAuthorization();
app.MapRazorPages();
app.MapPublicApi();

app.Run();
