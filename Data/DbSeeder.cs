using GlurSolutions.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Data;

/// <summary>
/// Fills an empty database with the six placeholder projects from the current site, so the pages
/// can be built against real rows. Safe to run repeatedly: it does nothing once any project exists.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Projects.AnyAsync()) return;

        // 1. Technologies. The keys match data-tech in the current HTML, so the Skills filter keeps working.
        var wanted = new (string Key, string Label)[]
        {
            ("html-css-js", "HTML / CSS / JS"),
            ("csharp", "C#"),
            ("dotnet", ".NET"),
            ("sql", "SQL"),
        };
        var tech = await db.Technologies.ToDictionaryAsync(t => t.Key);
        var order = 0;
        foreach (var (key, label) in wanted)
        {
            order++;
            if (tech.ContainsKey(key)) continue;
            var t = new Technology { Key = key, Label = label, SortOrder = order };
            db.Technologies.Add(t);
            tech[key] = t;
        }

        // 2. The six placeholder projects, tagged the same way as the cards on the current landing page
        var placeholders = new (string Slug, string Title, string[] Tech)[]
        {
            ("project-one",   "[PROJECT ONE]",   new[] { "csharp", "dotnet", "sql" }),
            ("project-two",   "[PROJECT TWO]",   new[] { "html-css-js", "csharp" }),
            ("project-three", "[PROJECT THREE]", new[] { "dotnet" }),
            ("project-four",  "[PROJECT FOUR]",  new[] { "sql" }),
            ("project-five",  "[PROJECT FIVE]",  new[] { "html-css-js", "csharp" }),
            ("project-six",   "[PROJECT SIX]",   new[] { "csharp" }),
        };

        var position = 0;
        foreach (var p in placeholders)
        {
            position++;
            db.Projects.Add(new Project
            {
                Slug = p.Slug,
                Title = p.Title,
                ShortDescription = "[Short description and tech used]",
                Summary = "[One or two sentences: what it is, who it is for and the problem it solves.]",
                CoverAlt = $"{p.Title} screenshot",
                Role = "[Designer and developer]",
                Tools = "[" + string.Join(", ", p.Tech.Select(k => tech[k].Label)) + ", Figma]",
                Timeframe = "[6 weeks]",
                SortOrder = position,
                IsPublished = true,
                ProjectTechnologies = p.Tech.Select(k => new ProjectTechnology { Technology = tech[k] }).ToList(),
                Slideshows = [ProcessSlideshow(), ImprovementSlideshow()],
                Notes = [.. FeedbackNotes(), .. ImprovedNotes()],
            });
        }

        await db.SaveChangesAsync();
    }

    // The top slideshow: five stages, in the order the stage buttons appear
    private static Slideshow ProcessSlideshow() => new()
    {
        Kind = SlideshowKind.Process,
        Heading = "Wireframe to final product",
        SortOrder = 1,
        Slides = MakeSlides(
            ("Wireframe", "Wireframe", "A grey-box layout used to settle structure, spacing and flow before any colour or styling."),
            ("Revised design", "Revised design", "A flat, coloured version used to test hierarchy, spacing and real wording before settling the look."),
            ("Final design", "Final design", "The polished design: [typography, colour and components all settled before any code was written]."),
            ("Feedback", "What people said", "[Feedback on the final design from classmates or a client: what worked, and what to fix before building.]"),
            ("Final product", "Final product", "[The finished, working project: screenshots of the live version and how it changed after the feedback.]")),
    };

    // The second slideshow: four versions
    private static Slideshow ImprovementSlideshow() => new()
    {
        Kind = SlideshowKind.Improvement,
        Heading = "Improvements over time",
        SortOrder = 2,
        Slides = MakeSlides(
            ("Version 1", "The starting point", "[Screenshot and a sentence on how it looked and worked at first.]"),
            ("Version 2", "First round of changes", "[What I changed after the first round of feedback, and why.]"),
            ("Version 3", "Refinement", "[Smaller fixes: spacing, colour, wording, speed.]"),
            ("Final", "The result", "[How the finished version compares with version 1.]")),
    };

    private static List<ProjectNote> FeedbackNotes() => MakeNotes(NoteKind.Feedback,
        ("Layout", "[Testers found the first wireframe crowded.]"),
        ("Navigation", "[Too many steps to reach the main task.]"),
        ("Readability", "[Text and contrast needed to be stronger.]"));

    private static List<ProjectNote> ImprovedNotes() => MakeNotes(NoteKind.Improved,
        ("Navigation", "from [a crowded menu with eight links] to [three clear destinations]."),
        ("Forms", "from [a long single page] to [a short three-step flow]."),
        ("Speed", "[page load] down from [4s] to [1.2s] after [optimising queries and images]."));

    private static List<Slide> MakeSlides(params (string Stage, string Title, string Caption)[] items) =>
        items.Select((s, i) => new Slide
        {
            StageLabel = s.Stage,
            Title = s.Title,
            Caption = s.Caption,
            SortOrder = i + 1,          // ImagePath stays empty, so the page shows the placeholder picture
        }).ToList();

    private static List<ProjectNote> MakeNotes(NoteKind kind, params (string Heading, string Body)[] items) =>
        items.Select((n, i) => new ProjectNote
        {
            Kind = kind,
            Heading = n.Heading,
            Body = n.Body,
            SortOrder = i + 1,
        }).ToList();
}
