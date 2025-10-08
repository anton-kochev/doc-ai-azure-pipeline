using api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents { get; set; }

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
    }
}
