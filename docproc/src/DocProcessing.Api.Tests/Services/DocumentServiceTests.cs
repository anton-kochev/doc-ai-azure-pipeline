using DocProcessing.Api.Data;
using DocProcessing.Api.Services;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DocProcessing.Api.Tests.Services;

public class DocumentServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<ILogger<DocumentService>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        // Create a unique database name for each test to ensure isolation
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<DocumentService>>();
        _timeProvider = new FakeTimeProvider();
        _service = new DocumentService(_dbContext, _loggerMock.Object, _timeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region CreateDocumentAsync Tests

    [Fact]
    public async Task CreateDocumentAsync_WithValidParameters_CreatesDocumentSuccessfully()
    {
        // Arrange
        string fileName = "test.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "test/test.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";
        Guid? tenantId = Guid.NewGuid();

        // Act
        Guid documentId = await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy,
            tenantId);

        // Assert
        Assert.NotEqual(Guid.Empty, documentId);

        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Equal(fileName, savedDocument.FileName);
        Assert.Equal(contentType, savedDocument.ContentType);
        Assert.Equal(sizeBytes, savedDocument.SizeBytes);
        Assert.Equal(blobContainer, savedDocument.BlobContainer);
        Assert.Equal(blobPath, savedDocument.BlobPath);
        Assert.Equal(blobETag, savedDocument.BlobETag);
        Assert.Equal(sha256Hash, savedDocument.Sha256Hash);
        Assert.Equal(uploadedBy, savedDocument.UploadedBy);
        Assert.Equal(tenantId, savedDocument.TenantId);
        Assert.Equal(DocumentStatus.Uploaded, savedDocument.Status);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, savedDocument.UploadedAtUtc);
    }

    [Fact]
    public async Task CreateDocumentAsync_WithoutTenantId_CreatesDocumentWithNullTenant()
    {
        // Arrange
        string fileName = "test.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "test/test.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";

        // Act
        Guid documentId = await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Null(savedDocument.TenantId);
    }

    [Fact]
    public async Task CreateDocumentAsync_SetsUploadedAtUtcToCurrentTime()
    {
        // Arrange
        DateTimeOffset expectedTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(expectedTime);

        string fileName = "test.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "test/test.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";

        // Act
        Guid documentId = await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Equal(expectedTime.UtcDateTime, savedDocument.UploadedAtUtc);
    }

    [Fact]
    public async Task CreateDocumentAsync_SetsStatusToUploaded()
    {
        // Arrange
        string fileName = "test.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "test/test.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";

        // Act
        Guid documentId = await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Equal(DocumentStatus.Uploaded, savedDocument.Status);
    }

    [Fact]
    public async Task CreateDocumentAsync_LogsInformationAfterCreation()
    {
        // Arrange
        string fileName = "test.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "test/test.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";

        // Act
        await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Created document record")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateDocumentAsync_WithLargeFile_StoresCorrectSize()
    {
        // Arrange
        string fileName = "large-file.zip";
        string contentType = "application/zip";
        long sizeBytes = 5_000_000_000; // 5GB
        string blobContainer = "documents";
        string blobPath = "large/large-file.zip";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32];
        string uploadedBy = "user@example.com";

        // Act
        Guid documentId = await _service.CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Equal(5_000_000_000, savedDocument.SizeBytes);
    }

    #endregion

    #region GetOrCreateDocumentAsync Tests

    [Fact]
    public async Task GetOrCreateDocumentAsync_WhenDocumentDoesNotExist_CreatesNewDocument()
    {
        // Arrange
        string fileName = "new-document.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 1024;
        string blobContainer = "documents";
        string blobPath = "new/new-document.pdf";
        string blobETag = "\"0x8D9A1B2C3D4E5F6\"";
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        string uploadedBy = "user@example.com";

        // Act
        (Guid documentId, bool isNew) = await _service.GetOrCreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy);

        // Assert
        Assert.True(isNew);
        Assert.NotEqual(Guid.Empty, documentId);

        Document? savedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(savedDocument);
        Assert.Equal(fileName, savedDocument.FileName);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_WhenDocumentExistsWithSameHash_ReturnsExistingDocument()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        Guid tenantId = Guid.NewGuid();

        // Create an existing document
        Guid existingDocumentId = await _service.CreateDocumentAsync(
            "existing-document.pdf",
            "application/pdf",
            2048,
            "documents",
            "existing/existing-document.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "original-user@example.com",
            tenantId);

        // Act - Try to create another document with the same hash and tenant
        (Guid documentId, bool isNew) = await _service.GetOrCreateDocumentAsync(
            "duplicate-document.pdf", // Different name
            "application/pdf",
            1024, // Different size
            "documents",
            "duplicate/duplicate-document.pdf", // Different path
            "\"0x8D9A1B2C3D4E5F7\"", // Different ETag
            sha256Hash, // Same hash
            "different-user@example.com", // Different user
            tenantId); // Same tenant

        // Assert
        Assert.False(isNew);
        Assert.Equal(existingDocumentId, documentId);

        // Verify the original document is returned (not a new one)
        Document? returnedDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(returnedDocument);
        Assert.Equal("existing-document.pdf", returnedDocument.FileName);
        Assert.Equal("original-user@example.com", returnedDocument.UploadedBy);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_WhenDocumentExistsButDeleted_CreatesNewDocument()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        Guid tenantId = Guid.NewGuid();

        // Create and mark document as deleted
        Guid deletedDocumentId = await _service.CreateDocumentAsync(
            "deleted-document.pdf",
            "application/pdf",
            2048,
            "documents",
            "deleted/deleted-document.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com",
            tenantId);

        Document? deletedDocument = await _dbContext.Documents.FindAsync(deletedDocumentId);
        Assert.NotNull(deletedDocument);
        deletedDocument.Status = DocumentStatus.Deleted;
        await _dbContext.SaveChangesAsync();

        // Act - Try to create another document with the same hash
        (Guid documentId, bool isNew) = await _service.GetOrCreateDocumentAsync(
            "new-document.pdf",
            "application/pdf",
            1024,
            "documents",
            "new/new-document.pdf",
            "\"0x8D9A1B2C3D4E5F7\"",
            sha256Hash,
            "user@example.com",
            tenantId);

        // Assert
        Assert.True(isNew);
        Assert.NotEqual(deletedDocumentId, documentId);

        // Verify a new document was created
        Document? newDocument = await _dbContext.Documents.FindAsync(documentId);
        Assert.NotNull(newDocument);
        Assert.Equal("new-document.pdf", newDocument.FileName);
        Assert.Equal(DocumentStatus.Uploaded, newDocument.Status);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_WithDifferentTenants_CreatesNewDocument()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        Guid tenant1 = Guid.NewGuid();
        Guid tenant2 = Guid.NewGuid();

        // Create document for tenant1
        Guid document1Id = await _service.CreateDocumentAsync(
            "document.pdf",
            "application/pdf",
            1024,
            "documents",
            "doc/document.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com",
            tenant1);

        // Act - Try to create document with same hash but different tenant
        (Guid document2Id, bool isNew) = await _service.GetOrCreateDocumentAsync(
            "document.pdf",
            "application/pdf",
            1024,
            "documents",
            "doc/document.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com",
            tenant2);

        // Assert
        Assert.True(isNew);
        Assert.NotEqual(document1Id, document2Id);

        // Verify both documents exist
        Document? doc1 = await _dbContext.Documents.FindAsync(document1Id);
        Document? doc2 = await _dbContext.Documents.FindAsync(document2Id);
        Assert.NotNull(doc1);
        Assert.NotNull(doc2);
        Assert.Equal(tenant1, doc1.TenantId);
        Assert.Equal(tenant2, doc2.TenantId);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_WithNullTenantId_HandlesCorrectly()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        // Create document without tenant
        Guid existingDocumentId = await _service.CreateDocumentAsync(
            "no-tenant-document.pdf",
            "application/pdf",
            1024,
            "documents",
            "no-tenant/document.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com");

        // Act - Try to create another document with same hash and no tenant
        (Guid documentId, bool isNew) = await _service.GetOrCreateDocumentAsync(
            "another-no-tenant-document.pdf",
            "application/pdf",
            2048,
            "documents",
            "another/document.pdf",
            "\"0x8D9A1B2C3D4E5F7\"",
            sha256Hash,
            "user@example.com");

        // Assert
        Assert.False(isNew);
        Assert.Equal(existingDocumentId, documentId);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_LogsExistingDocumentFound_WhenDocumentExists()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        await _service.CreateDocumentAsync(
            "existing.pdf",
            "application/pdf",
            1024,
            "documents",
            "existing.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com");

        _loggerMock.Reset(); // Reset to clear the log from CreateDocumentAsync

        // Act
        await _service.GetOrCreateDocumentAsync(
            "duplicate.pdf",
            "application/pdf",
            1024,
            "documents",
            "duplicate.pdf",
            "\"0x8D9A1B2C3D4E5F7\"",
            sha256Hash,
            "user@example.com");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Found existing document with same hash")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_LogsCreation_WhenCreatingNewDocument()
    {
        // Arrange
        byte[] sha256Hash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        // Act
        await _service.GetOrCreateDocumentAsync(
            "new.pdf",
            "application/pdf",
            1024,
            "documents",
            "new.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash,
            "user@example.com");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Created document record")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateDocumentAsync_WithDifferentHash_CreatesNewDocument()
    {
        // Arrange
        byte[] sha256Hash1 = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        byte[] sha256Hash2 = new byte[32] { 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        // Create first document
        Guid document1Id = await _service.CreateDocumentAsync(
            "document1.pdf",
            "application/pdf",
            1024,
            "documents",
            "doc1.pdf",
            "\"0x8D9A1B2C3D4E5F6\"",
            sha256Hash1,
            "user@example.com");

        // Act - Create document with different hash
        (Guid document2Id, bool isNew) = await _service.GetOrCreateDocumentAsync(
            "document2.pdf",
            "application/pdf",
            1024,
            "documents",
            "doc2.pdf",
            "\"0x8D9A1B2C3D4E5F7\"",
            sha256Hash2,
            "user@example.com");

        // Assert
        Assert.True(isNew);
        Assert.NotEqual(document1Id, document2Id);
    }

    #endregion
}
