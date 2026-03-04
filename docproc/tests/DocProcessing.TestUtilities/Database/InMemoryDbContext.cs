using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using DocProcessing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DocProcessing.TestUtilities.Database;

public class InMemoryDbContext : IApplicationDbContext, IDisposable, IAsyncDisposable
{
    private readonly ApplicationDbContext _context;

    public InMemoryDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Create a unique database name for each test to ensure isolation
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    public DbSet<Document> Documents { get => _context.Documents; set => _context.Documents = value; }
    public DbSet<ProcessJob> ProcessJobs { get => _context.ProcessJobs; set => _context.ProcessJobs = value; }
    public DbSet<ProfileCatalog> ProfileCatalogs { get => _context.ProfileCatalogs; set => _context.ProfileCatalogs = value; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _context.SaveChangesAsync(cancellationToken);

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
        => _context.Entry(entity);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
