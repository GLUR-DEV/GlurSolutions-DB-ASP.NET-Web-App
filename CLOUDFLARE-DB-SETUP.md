# Connecting glursolutions.com (Cloudflare) to your database

```
Local admin app ──writes──▶  Azure SQL  ◀──reads──  Hosted API (this project)  ◀──server-side──  Cloudflare Worker  ◀──  visitors
                                                    api.glursolutions.com                        glursolutions.com
```

Your live site is a Cloudflare Worker with static pages. The Worker already answers `/api/*`
(the contact form), so it also forwards three read-only content requests to the hosted API and caches the answers.
Visitors only ever talk to glursolutions.com: no CORS, and the API key never reaches a browser.

Two repos are involved:

| Repo | Change |
|---|---|
| This one (ASP.NET + EF Core) | Read-only API in `Api/PublicApi.cs`, wired up in `Program.cs` |
| The website source (Website V1.4.9) | `src/worker.js` proxy, `public/content.js`, small edits to both HTML pages and `wrangler.toml` |

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

Make up a long random key first (for example `openssl rand -base64 32`). The API and the Worker must share it.

1. Create an **Azure App Service** (Linux, .NET 10) and deploy this project to it.
2. App Service → **Environment variables**, add:
   - `ConnectionStrings__DefaultConnection` = the Azure SQL string
   - `Api__Key` = the random key
   - `ASPNETCORE_ENVIRONMENT` = `Production` (so it does not migrate or seed on start-up)
3. Check `https://YOURAPP.azurewebsites.net/api/health` returns `{"status":"ok"}`. Content endpoints answer `401` without the key.
4. Optional custom domain: in Cloudflare DNS add a `CNAME` for `api` pointing to `YOURAPP.azurewebsites.net` (**DNS only** at first)
   plus the `asuid.api` TXT record Azure asks for, then add `api.glursolutions.com` under App Service → Custom domains and create
   the free managed certificate. If you skip this, use `https://YOURAPP.azurewebsites.net` as `API_ORIGIN` below.

| Endpoint | Used for |
|---|---|
| `/api/technologies` | Skills pills |
| `/api/projects` | Landing page cards |
| `/api/projects/{slug}` | A project page, including slideshows and notes |
| `/api/health` | Uptime check (no key needed) |

## 4. Deploy the Worker changes

In the website repo:

1. Edit `API_ORIGIN` in `wrangler.toml` to your API address.
2. Store the key as a secret: `npx wrangler secret put API_KEY` (paste the same random key).
3. `npx wrangler deploy`.

How it behaves:
- Answers are cached 60 seconds. If Azure or the database is asleep or down, the last good answer (up to a day old) is served.
- The pages ship with their current placeholder HTML. If the API can't be reached at all, that HTML is shown instead of a blank page.
- Cards link to `projects.html?project=<slug>`. Old `?project=one` links still work (they try `project-one`).

## Things to know

- **Images**: `CoverImagePath` and `ImagePath` are paths inside this app's `wwwroot`. Files uploaded through your local
  app won't exist on Azure or Cloudflare. Until you move uploads to Azure Blob Storage or Cloudflare R2 and store full
  `https://` URLs, pictures show the grey placeholder (a relative path is looked up on glursolutions.com itself).
- **"Check this project out" button**: the old pages had a per-project external link, but the database has no field for it yet,
  so the button is not shown for database projects. Adding a `LiveUrl` column (plus a migration) would bring it back.
- **No writes are exposed**: the public API is read-only. Keep editing in the admin app, and add authentication before
  ever putting write endpoints on the internet.
- **Cold starts**: the free Azure SQL tier can pause when idle; the first request after a quiet spell may be slow. The app
  retries automatically, and the Worker cache hides most of it from visitors.
- **Unpublished projects** never leave the database: only rows with `IsPublished = true` are returned.
- **Code not compiled here**: the .NET code was written without an SDK available, so run `dotnet build` once. The Worker
  proxy was tested with mocked requests; the page script (`content.js`) was only syntax-checked, so load the site locally
  with `npx wrangler dev` and click through the home and project pages before deploying.
