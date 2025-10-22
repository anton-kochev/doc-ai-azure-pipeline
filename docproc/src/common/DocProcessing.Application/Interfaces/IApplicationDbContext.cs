using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Database context interface for the document processing application.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Gets or sets the Documents DbSet for accessing document records.
    /// </summary>
    DbSet<Document> Documents { get; set; }

    /// <summary>
    /// Gets or sets the ProcessJobs DbSet for accessing process job records.
    /// </summary>
    DbSet<ProcessJob> ProcessJobs { get; set; }

    /// <summary>
    /// Gets or sets the ProfileCatalogs DbSet for accessing profile catalog records.
    /// </summary>
    DbSet<ProfileCatalog> ProfileCatalogs { get; set; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when an error is encountered while saving to the database.
    /// </exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">
    /// Thrown when a concurrency violation is encountered while saving to the database.
    /// </exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets an EntityEntry for the given entity providing access to change tracking information and operations.
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
