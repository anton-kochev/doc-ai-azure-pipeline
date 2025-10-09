using api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<ProcessJob> ProcessJobs { get; set; }
    public DbSet<ProfileCatalog> ProfileCatalogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId);
            entity.Property(e => e.DocumentId)
                .IsRequired();

            // Filtered unique index for deduplication (excludes deleted documents)
            entity.HasIndex(e => new { e.TenantId, e.Sha256Hash })
                .IsUnique()
                .HasFilter("[Status] <> 'Deleted'")
                .HasDatabaseName("IX_Documents_TenantId_Sha256Hash_Unique");

            entity.Property(e => e.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (DocumentStatus)Enum.Parse(typeof(DocumentStatus), v));
        });

        modelBuilder.Entity<ProcessJob>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId)
                .IsRequired();

            // Foreign key with cascade delete
            entity.HasOne(e => e.Document)
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (ProcessJobStatus)Enum.Parse(typeof(ProcessJobStatus), v));

            entity.Property(e => e.Stage)
                .HasConversion(
                    v => v.ToString(),
                    v => (ProcessJobStage)Enum.Parse(typeof(ProcessJobStage), v));
        });

        modelBuilder.Entity<ProfileCatalog>(entity =>
        {
            // Composite primary key
            entity.HasKey(e => new { e.ProfileName, e.Version });

            entity.Property(e => e.ProfileName)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.Version)
                .IsRequired()
                .HasMaxLength(32);

            // Unique filtered index: at most one Active default per profileName
            entity.HasIndex(e => new { e.ProfileName, e.IsDefault })
                .IsUnique()
                .HasFilter("[Status] = 'Active' AND [IsDefault] = 1")
                .HasDatabaseName("IX_ProfileCatalog_ProfileName_IsDefault_Unique");

            // Enum conversion for Status
            entity.Property(e => e.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (ProfileStatus)Enum.Parse(typeof(ProfileStatus), v));
        });
    }
}
