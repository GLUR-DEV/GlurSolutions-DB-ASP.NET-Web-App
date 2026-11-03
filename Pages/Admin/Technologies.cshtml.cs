using GlurSolutions.Web.Data;
using GlurSolutions.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Pages.Admin;

// The skills: the pills on the home page and the tags on project cards. One Save button saves every row on the page.
public class TechnologiesModel(AppDbContext db) : AdminPageModel
{
    public class SkillRow
    {
        public int Id { get; set; }              // 0 = a new skill
        public string? Label { get; set; }
        public string? Key { get; set; }
        public int SortOrder { get; set; }
        public bool Delete { get; set; }
    }

    [BindProperty] public List<SkillRow> Rows { get; set; } = [];

    /// <summary>How many projects use each skill (skill id to count).</summary>
    public Dictionary<int, int> Uses { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var items = await db.Technologies
            .AsNoTracking()
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.Key, t.Label, t.SortOrder, Count = t.ProjectTechnologies.Count })
            .ToListAsync();

        Rows = items.Select(t => new SkillRow { Id = t.Id, Key = t.Key, Label = t.Label, SortOrder = t.SortOrder }).ToList();
        Uses = items.ToDictionary(t => t.Id, t => t.Count);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await db.Technologies.Include(t => t.ProjectTechnologies).ToListAsync();
        var byId = existing.ToDictionary(t => t.Id);
        Uses = existing.ToDictionary(t => t.Id, t => t.ProjectTechnologies.Count);

        // Keys of skills that were not on the submitted page still count as taken
        var submittedIds = Rows.Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();
        var takenKeys = new HashSet<string>(existing.Where(t => !submittedIds.Contains(t.Id)).Select(t => t.Key));

        var toAdd = new List<(string Key, string Label, int Sort)>();
        var toChange = new List<(Technology Skill, string Key, string Label, int Sort)>();
        var toRemove = new List<Technology>();

        var position = 0;
        foreach (var row in Rows)
        {
            position++;
            Technology? current = null;
            if (row.Id != 0 && !byId.TryGetValue(row.Id, out current)) continue;   // already gone

            if (row.Delete)
            {
                if (current is null) continue;
                // The database refuses to delete a skill that projects still use, so say so nicely
                if (current.ProjectTechnologies.Count > 0)
                    Errors.Add($"\"{current.Label}\" is still used by {current.ProjectTechnologies.Count} project(s). Untick it on those projects before deleting it.");
                else
                    toRemove.Add(current);
                continue;
            }

            var key = row.Key?.Trim().ToLowerInvariant();
            var label = row.Label?.Trim();
            if (current is null && string.IsNullOrEmpty(key) && string.IsNullOrEmpty(label)) continue;   // an empty "new" row

            var name = string.IsNullOrEmpty(label) ? $"Skill {position}" : $"\"{label}\"";
            if (string.IsNullOrEmpty(label) || label.Length > 60)
            {
                Errors.Add($"Skill {position}: enter a label of up to 60 characters.");
                continue;
            }
            if (string.IsNullOrEmpty(key) || key.Length > 40 || !AdminValidation.Slug.IsMatch(key))
            {
                Errors.Add($"{name}: the key must be lower-case letters, numbers and single hyphens (max 40), e.g. html-css-js.");
                continue;
            }
            if (!takenKeys.Add(key))
            {
                Errors.Add($"{name}: another skill already uses the key \"{key}\".");
                continue;
            }

            if (current is null) toAdd.Add((key, label, row.SortOrder));
            else toChange.Add((current, key, label, row.SortOrder));
        }

        if (Errors.Count > 0) return Page();   // nothing saved; the form keeps what was typed

        var updated = 0;
        foreach (var (skill, newKey, newLabel, newSort) in toChange)
        {
            if (skill.Key == newKey && skill.Label == newLabel && skill.SortOrder == newSort) continue;
            skill.Key = newKey;
            skill.Label = newLabel;
            skill.SortOrder = newSort;
            updated++;
        }
        foreach (var (addKey, addLabel, addSort) in toAdd)
            db.Technologies.Add(new Technology { Key = addKey, Label = addLabel, SortOrder = addSort });
        db.Technologies.RemoveRange(toRemove);

        if (updated + toAdd.Count + toRemove.Count == 0)
        {
            Flash("No changes to save.");
            return RedirectToPage();
        }

        await db.SaveChangesAsync();
        await RefreshSnapshotAsync();
        Flash(Summarise("skills", updated, toAdd.Count, toRemove.Count));
        return RedirectToPage();
    }
}
