using GlurSolutions.Web.Data;
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
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

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
app.UseAuthorization();
app.MapRazorPages();

app.Run();
