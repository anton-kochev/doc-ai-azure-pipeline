using DocProcessing.TestUtilities.Database;

namespace Infrastructure.Tests;

// Kept for backward compatibility — delegates to the shared implementation in TestUtilities.
public class InMemoryDbContext : DocProcessing.TestUtilities.Database.InMemoryDbContext;
