# GLUR Solutions – ASP.NET Core site (step 1: project, database and seed data)

This is step 1 of moving the portfolio onto a database. It contains the ASP.NET Core project, the
database model (Entity Framework Core + SQL Server), a seed that loads the six placeholder
projects, and a temporary page that proves it all works. Your real pages arrive in step 2.

## What you need installed
1. **.NET 10 SDK** (the current long-term-support release) – check with `dotnet --version`.
   On .NET 8 or 9 instead? Change `TargetFramework` and the `10.0.*` package versions in the .csproj.
2. **SQL Server** – on Windows the easiest is **LocalDB**, which installs with Visual Studio
   ("ASP.NET and web development" workload) or as a free standalone download. SQL Server Express also works.
3. **EF Core command-line tool:** `dotnet tool install --global dotnet-ef`
4. **HTTPS dev certificate (once):** `dotnet dev-certs https --trust`

## Run it
```
cd GlurSolutions.Web
dotnet restore
dotnet ef migrations add InitialCreate -o Data/Migrations
dotnet ef database update
dotnet run
```
Then open https://localhost:7080. You should see the site's dark theme and a table of
**6 projects, each with 2 slideshows, 9 slides and 6 notes**.

(`dotnet run` also creates and seeds the database by itself in Development, so `database update`
is optional, but running it once shows any problem early.)

## Using a different SQL Server
Edit `ConnectionStrings:DefaultConnection` in `appsettings.Development.json`:
* LocalDB: `Server=(localdb)\\MSSQLLocalDB;Database=GlurSolutions;Trusted_Connection=True;TrustServerCertificate=True`
* SQL Server Express: `Server=.\\SQLEXPRESS;Database=GlurSolutions;Trusted_Connection=True;TrustServerCertificate=True`
* Username and password: `Server=...;Database=GlurSolutions;User Id=...;Password=...;TrustServerCertificate=True`
  (keep passwords out of git: use `dotnet user-secrets` locally and environment variables in production)

## Folder guide
| Path | What it is |
|---|---|
| `Data/Entities/` | One class per table (Project, Technology, Slideshow, Slide, ProjectNote, ProjectTechnology) |
| `Data/AppDbContext.cs` | Tells EF Core how the classes map to tables: lengths, unique values, delete rules |
| `Data/DbSeeder.cs` | The six placeholder projects. Does nothing if any project already exists |
| `Data/Migrations/` | Created by the `migrations add` command: the SQL that builds the tables |
| `Pages/Index.cshtml` | TEMPORARY check page, replaced in step 2 |
| `wwwroot/` | Your CSS and JS, served as they are |
| `Reference/` | Your current index.html and projects.html, used as the templates for step 2 (not served) |

## Changing the model later
Edit an entity class, then `dotnet ef migrations add WhatChanged` and `dotnet ef database update`.
To start again from nothing during development: `dotnet ef database drop` then run it again.
