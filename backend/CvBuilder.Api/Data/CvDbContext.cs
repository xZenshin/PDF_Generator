using CvBuilder.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CvBuilder.Api.Data;

public class CvDbContext(DbContextOptions<CvDbContext> options) : DbContext(options)
{
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<CvItem> Items => Set<CvItem>();
    public DbSet<Bullet> Bullets => Set<Bullet>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Cv>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Headline).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(60);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Website).HasMaxLength(200);
            e.Property(x => x.Summary).HasMaxLength(4000);

            e.HasMany(x => x.Sections)
                .WithOne(x => x.Cv!)
                .HasForeignKey(x => x.CvId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Section>(e =>
        {
            e.Property(x => x.Ref).HasMaxLength(40);
            e.Property(x => x.Title).HasMaxLength(120);
            e.HasIndex(x => new { x.CvId, x.SortOrder });
            e.HasIndex(x => new { x.CvId, x.Ref });

            e.HasMany(x => x.Items)
                .WithOne(x => x.Section!)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CvItem>(e =>
        {
            e.Property(x => x.Ref).HasMaxLength(40);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Organization).HasMaxLength(200);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.StartDate).HasMaxLength(40);
            e.Property(x => x.EndDate).HasMaxLength(40);
            e.HasIndex(x => new { x.SectionId, x.SortOrder });

            e.HasMany(x => x.Bullets)
                .WithOne(x => x.Item!)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Bullet>(e =>
        {
            e.Property(x => x.Ref).HasMaxLength(40);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.HasIndex(x => new { x.ItemId, x.SortOrder });
        });
    }
}
