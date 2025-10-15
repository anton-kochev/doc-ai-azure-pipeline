using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DocProcessing.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Document> Documents { get; set; }
    DbSet<ProcessJob> ProcessJobs { get; set; }
    DbSet<ProfileCatalog> ProfileCatalogs { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets an EntityEntry for the given entity providing access to change tracking information and operations.
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
