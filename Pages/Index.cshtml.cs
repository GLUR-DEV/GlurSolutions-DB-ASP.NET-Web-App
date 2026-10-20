using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages;

// TEMPORARY page for step 1: proves the database, the seed data and the static files all work.
// Step 2 replaces it with the real landing page.
public class IndexModel(AppDbContext db) : PageModel
{
    public List<Project> Projects { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Projects = await db.Projects
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.SortOrder)
            .Include(p => p.ProjectTechnologies).ThenInclude(pt => pt.Technology)
            .Include(p => p.Slideshows).ThenInclude(s => s.Slides)
            .Include(p => p.Notes)
            .AsSplitQuery()      // avoids one huge join across several collections
            .ToListAsync();
    }
}
