namespace GlurSolutions.Web.Data.Entities;

/// <summary>A skill shown as a Skills pill and as a tag on project cards.</summary>
public class Technology
{
    public int Id { get; set; }

    /// <summary>The value used in data-tech on the page, e.g. "csharp". Must be unique.</summary>
    public string Key { get; set; } = "";

    /// <summary>The text people see, e.g. "C#".</summary>
    public string Label { get; set; } = "";

    public int SortOrder { get; set; }

    public List<ProjectTechnology> ProjectTechnologies { get; set; } = [];
}
