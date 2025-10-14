using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Document> Documents { get; set; }
    DbSet<ProcessJob> ProcessJobs { get; set; }
    DbSet<ProfileCatalog> ProfileCatalogs { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
