namespace GlurSolutions.Web.Data.Entities;

/// <summary>One project: a card on the landing page and a page of its own.</summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>Used in the address, e.g. "project-one" gives /projects/project-one. Must be unique.</summary>
    public string Slug { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>The short text on the landing page card.</summary>
    public string ShortDescription { get; set; } = "";

    /// <summary>The longer intro at the top of the project page.</summary>
    public string? Summary { get; set; }

    /// <summary>Card picture, relative to wwwroot (e.g. "uploads/abc123.jpg"). Empty shows the placeholder.</summary>
    public string? CoverImagePath { get; set; }
    public string? CoverAlt { get; set; }

    // The "Details" panel on the project page
    public string? Role { get; set; }
    public string? Tools { get; set; }
    public string? Timeframe { get; set; }

    /// <summary>Lower numbers appear first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Only published projects appear on the public site. Lets you build one as a draft.</summary>
    public bool IsPublished { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<ProjectTechnology> ProjectTechnologies { get; set; } = [];
    public List<Slideshow> Slideshows { get; set; } = [];
    public List<ProjectNote> Notes { get; set; } = [];
}
