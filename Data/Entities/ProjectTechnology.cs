namespace GlurSolutions.Web.Data.Entities;

/// <summary>Links a project to a technology (many-to-many). Drives the card tags and the Skills filter.</summary>
public class ProjectTechnology
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int TechnologyId { get; set; }
    public Technology Technology { get; set; } = null!;
}
