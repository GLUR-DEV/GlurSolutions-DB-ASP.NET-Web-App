namespace GlurSolutions.Web.Data.Entities;

public enum NoteKind
{
    /// <summary>The "Feedback" panel beside the top slideshow.</summary>
    Feedback,
    /// <summary>The "What improved" panel beside the second slideshow.</summary>
    Improved
}

/// <summary>One line in a side panel: a bold heading and a sentence.</summary>
public class ProjectNote
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public NoteKind Kind { get; set; }
    public string Heading { get; set; } = "";
    public string Body { get; set; } = "";
    public int SortOrder { get; set; }
}
