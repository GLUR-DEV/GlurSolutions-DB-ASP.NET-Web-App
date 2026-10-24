using System.Threading.RateLimiting;
using GlurSolutions.Web.Api;
using GlurSolutions.Web.Data;
using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Helper commands for setting up the admin login (dotnet run -- hash-password / generate-secret).
// They run and exit before the website starts, and never touch the database.
if (AdminCli.TryRun(args)) return;

var builder = WebApplication.CreateBuilder(args);

// Pages and their links in lower case (/admin/login), so the Cloudflare Worker's /admin routes always match
builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddRazorPages(options =>
{
    // Everything under /Admin needs the admin login, except the login page itself
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
    // One log line per change made in the admin (who, where from, what)
    options.Conventions.AddFolderApplicationModelConvention("/Admin", model => model.Filters.Add(new AdminAuditFilter()));
});

// ---- Admin login (see Security/) ----
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection(AdminSettings.Section));
builder.Services.AddSingleton<LoginThrottle>();

builder.Services.AddAuthentication(AdminAuth.Scheme)
    .AddCookie(AdminAuth.Scheme, cookie =>
    {
        cookie.Cookie.Name = "__Host-glur-admin";              // the __Host- prefix makes browsers insist on Secure, Path=/ and no Domain
        cookie.Cookie.HttpOnly = true;                          // scripts on the page cannot read it
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always; // only ever sent over https
        cookie.Cookie.SameSite = SameSiteMode.Strict;           // never sent with requests started by other sites
        cookie.Cookie.Path = "/";
        cookie.ExpireTimeSpan = TimeSpan.FromMinutes(30);       // idle for 30 minutes: signed out (AdminAuth caps any session at 8 hours)
        cookie.SlidingExpiration = true;
        cookie.Events.OnValidatePrincipal = AdminAuth.ValidateAsync;
        // Always the same fixed login address: no ReturnUrl parameter, so no open-redirect trick
        cookie.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/admin/login");
            return Task.CompletedTask;
        };
        cookie.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/admin/login");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// The antiforgery token cookie (on every admin form) gets the same strict settings
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-glur-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

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

// Say plainly in the log when the admin is switched off, and why
{
    var admin = app.Services.GetRequiredService<IOptions<AdminSettings>>().Value;
    if (!admin.IsEnabled(app.Environment.IsDevelopment()))
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(admin.Username)) missing.Add("Admin:Username");
        if (string.IsNullOrWhiteSpace(admin.PasswordHash)) missing.Add("Admin:PasswordHash");
        if (!app.Environment.IsDevelopment() && !admin.HasOriginSecret) missing.Add("Admin:OriginSecret (at least 32 characters)");
        app.Logger.LogWarning("The /admin area is switched OFF (every /admin address answers 404). Missing: {Missing}", string.Join(", ", missing));
    }
}

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

// First thing for every /admin address: 404 unless configured and (on Azure) carrying the Worker's secret; adds the security headers
app.UseMiddleware<AdminGateMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();      // serves everything in wwwroot (your CSS and JS)
app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapPublicApi();

app.Run();
