namespace GlurSolutions.Web.Data.Entities;

public enum SlideshowKind
{
    /// <summary>The top slideshow: wireframe to final product.</summary>
    Process,
    /// <summary>The second slideshow: improvements over time.</summary>
    Improvement
}

/// <summary>A slideshow on a project page.</summary>
public class Slideshow
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public SlideshowKind Kind { get; set; }

    /// <summary>The heading above the slideshow, e.g. "Wireframe to final product".</summary>
    public string Heading { get; set; } = "";

    public int SortOrder { get; set; }

    public List<Slide> Slides { get; set; } = [];
}
