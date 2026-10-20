using GlurSolutions.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlurSolutions.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Technology> Technologies => Set<Technology>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTechnology> ProjectTechnologies => Set<ProjectTechnology>();
    public DbSet<Slideshow> Slideshows => Set<Slideshow>();
    public DbSet<Slide> Slides => Set<Slide>();
    public DbSet<ProjectNote> ProjectNotes => Set<ProjectNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Technology>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Label).HasMaxLength(60).IsRequired();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.ShortDescription).HasMaxLength(300).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.Property(x => x.CoverImagePath).HasMaxLength(260);
            e.Property(x => x.CoverAlt).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(120);
            e.Property(x => x.Tools).HasMaxLength(200);
            e.Property(x => x.Timeframe).HasMaxLength(80);
            e.HasIndex(x => new { x.IsPublished, x.SortOrder });   // the landing page query
        });

        // Many-to-many: deleting a project removes its links, but a technology that is still used can't be deleted
        modelBuilder.Entity<ProjectTechnology>(e =>
        {
            e.HasKey(x => new { x.ProjectId, x.TechnologyId });
            e.HasOne(x => x.Project).WithMany(p => p.ProjectTechnologies)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Technology).WithMany(t => t.ProjectTechnologies)
                .HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Slideshow>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);   // stored as text: "Process", "Improvement"
            e.Property(x => x.Heading).HasMaxLength(120).IsRequired();
            e.HasOne(x => x.Project).WithMany(p => p.Slideshows)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Slide>(e =>
        {
            e.Property(x => x.StageLabel).HasMaxLength(60).IsRequired();
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Caption).HasMaxLength(600).IsRequired();
            e.Property(x => x.ImagePath).HasMaxLength(260);
            e.Property(x => x.ImageAlt).HasMaxLength(200);
            e.HasOne(x => x.Slideshow).WithMany(s => s.Slides)
                .HasForeignKey(x => x.SlideshowId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectNote>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);   // "Feedback", "Improved"
            e.Property(x => x.Heading).HasMaxLength(80).IsRequired();
            e.Property(x => x.Body).HasMaxLength(400).IsRequired();
            e.HasOne(x => x.Project).WithMany(p => p.Notes)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    // Keeps Project.UpdatedUtc current whenever a project is added or changed
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Project>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.UpdatedUtc = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
