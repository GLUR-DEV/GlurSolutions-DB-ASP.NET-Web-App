namespace GlurSolutions.Web.Data.Entities;

/// <summary>One picture in a slideshow, with its stage button and caption.</summary>
public class Slide
{
    public int Id { get; set; }

    public int SlideshowId { get; set; }
    public Slideshow Slideshow { get; set; } = null!;

    /// <summary>The text on the stage button, e.g. "Wireframe".</summary>
    public string StageLabel { get; set; } = "";

    public string Title { get; set; } = "";
    public string Caption { get; set; } = "";

    /// <summary>Picture path relative to wwwroot. Empty shows the placeholder picture.</summary>
    public string? ImagePath { get; set; }
    public string? ImageAlt { get; set; }

    public int SortOrder { get; set; }
}
