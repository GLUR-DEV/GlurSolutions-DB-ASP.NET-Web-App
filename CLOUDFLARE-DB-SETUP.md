# Connecting glursolutions.com (Cloudflare) to your database

```
Local admin app ──writes──▶  Azure SQL  ◀──reads──  Hosted API (api.glursolutions.com)  ◀──fetch──  glursolutions.com (Cloudflare)
```

The Cloudflare site is static, so it can't talk to SQL Server itself. It calls a small read-only API,
and that API reads the same Azure SQL database your local app edits. This repo now contains that API
(`Api/PublicApi.cs`, wired up in `Program.cs`).

## 1. Create the Azure SQL database

1. In the Azure portal create a **SQL database** (the free offer is enough) named `GlurSolutions`, with a new SQL server and SQL authentication.
2. Server → **Networking**: tick **Allow Azure services and resources to access this server**, and **add your current client IP** so your PC can connect.
3. Your connection string looks like:

```
Server=tcp:YOURSERVER.database.windows.net,1433;Initial Catalog=GlurSolutions;User ID=YOURUSER;Password=YOURPASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Never commit it. Your home IP can change, so re-add it in Networking if the local app stops connecting.

## 2. Point the local app at Azure SQL

From the project folder:

```
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<the connection string above>"
dotnet ef database update
dotnet run
```

User secrets override the LocalDB string in `appsettings.Development.json`, and they live outside the repo.
`dotnet ef database update` creates the tables once. In Development the app also seeds the six placeholder
projects, but only if the Projects table is empty.

Anything you add or edit locally is now live data in Azure SQL. If you want a safe sandbox, create a second
database (for example `GlurSolutions-Dev`) and use that string locally.

## 3. Host the API

1. Create an **Azure App Service** (Linux, .NET 10) and deploy this project to it.
2. App Service → **Environment variables**, add:
   - `ConnectionStrings__DefaultConnection` = the Azure SQL string
   - `ASPNETCORE_ENVIRONMENT` = `Production` (so it does not migrate or seed on start-up)
3. Check `https://YOURAPP.azurewebsites.net/api/health` returns `{"status":"ok"}`.
4. Custom domain: in Cloudflare DNS add a `CNAME` for `api` pointing to `YOURAPP.azurewebsites.net` (set to **DNS only** at first)
   plus the `asuid.api` TXT record Azure asks for. Add `api.glursolutions.com` under App Service → Custom domains and create
   the free managed certificate. After that you can turn the Cloudflare proxy on with SSL mode **Full (strict)**.

Endpoints (all GET, published projects only):

| Endpoint | Used for |
|---|---|
| `/api/technologies` | Skills pills |
| `/api/projects` | Landing page cards |
| `/api/projects/{slug}` | A project page, including slideshows and notes |
| `/api/health` | Uptime check |

Allowed browser origins are in `appsettings.json` under `Cors:AllowedOrigins`.

## 4. Call it from the live site

```js
const API = "https://api.glursolutions.com/api";

async function loadProjects() {
  const res = await fetch(`${API}/projects`);
  if (!res.ok) throw new Error(`API returned ${res.status}`);
  return res.json();   // [{ slug, title, shortDescription, technologies: [{ key, label }], ... }]
}

async function loadProject(slug) {
  const res = await fetch(`${API}/projects/${encodeURIComponent(slug)}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API returned ${res.status}`);
  return res.json();
}
```

Render with `textContent` (not `innerHTML`) so database text can't inject markup. The current site links to
`projects.html?project=one`; with the database it becomes `projects.html?project=project-one` (the slug).

## Things to know

- **Images**: `CoverImagePath` and `ImagePath` are paths inside this app's `wwwroot`. Files uploaded through your local
  app won't exist on Azure or Cloudflare. Plan to move uploads to Azure Blob Storage or Cloudflare R2 and store full URLs.
- **No writes are exposed**: the public API is read-only. Keep editing in the admin app, and add authentication before
  ever putting write endpoints on the internet.
- **Cold starts**: the free Azure SQL tier can pause when idle, so the first request after a quiet spell may be slow;
  the app retries automatically (`EnableRetryOnFailure`).
- **Testing the site locally**: add `http://localhost:PORT` to `Cors:AllowedOrigins` in `appsettings.Development.json`.
- This code was written without a .NET SDK available here, so run `dotnet build` once to confirm it compiles.
