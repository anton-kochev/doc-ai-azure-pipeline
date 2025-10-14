using DocProcessing.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Api.Tests.Services;

public class BlobStorageServiceTests
{
    private readonly Mock<ILogger<BlobStorageService>> _loggerMock = new();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        IOptions<AzureStorageOptions> options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=testkey==;EndpointSuffix=core.windows.net",
            ContainerName = "test-container"
        });

        // Act & Assert - Should not throw
        Exception? exception = Record.Exception(() => new BlobStorageService(options, _loggerMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithAccountNameOnly_DoesNotThrow()
    {
        // Arrange
        IOptions<AzureStorageOptions> options = Options.Create(new AzureStorageOptions
        {
            AccountName = "teststorage",
            ContainerName = "test-container"
        });

        // Act & Assert - Should not throw
        Exception? exception = Record.Exception(() => new BlobStorageService(options, _loggerMock.Object));
        Assert.Null(exception);
    }

    #endregion

    #region ValidateConfiguration Tests (via UploadAsync)

    [Fact]
    public async Task UploadBlobAsync_WithNoConnectionStringOrAccountName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<AzureStorageOptions> options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = null,
            AccountName = "",
            ContainerName = "test-container"
        });

        BlobStorageService service = new(options, _loggerMock.Object);
        using MemoryStream stream = new([1, 2, 3]);

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.UploadAsync("test.txt", stream));

        Assert.Equal("Either Azure Storage connection string or account name must be configured", exception.Message);
    }

    [Fact]
    public async Task UploadBlobAsync_WithEmptyConnectionStringAndEmptyAccountName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<AzureStorageOptions> options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = "",
            AccountName = "",
            ContainerName = "test-container"
        });

        BlobStorageService service = new(options, _loggerMock.Object);
        using MemoryStream stream = new([1, 2, 3]);

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.UploadAsync("test.txt", stream));

        Assert.Equal("Either Azure Storage connection string or account name must be configured", exception.Message);
    }

    [Fact]
    public async Task UploadBlobAsync_WithNullConnectionStringAndEmptyAccountName_ThrowsInvalidOperationException()
    {
        // Arrange
        IOptions<AzureStorageOptions> options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = null,
            AccountName = "",
            ContainerName = "test-container"
        });

        BlobStorageService service = new(options, _loggerMock.Object);
        using MemoryStream stream = new([1, 2, 3]);

        // Act & Assert
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.UploadAsync("test.txt", stream));

        Assert.Equal("Either Azure Storage connection string or account name must be configured", exception.Message);
    }

    #endregion

    #region UploadAsync Integration Tests

    // NOTE: The following tests demonstrate what should be tested for UploadAsync.
    // However, due to the current implementation where BlobServiceClient, BlobContainerClient,
    // and BlobClient are created within the method (sealed classes that cannot be easily mocked),
    // true unit testing of UploadAsync requires either:
    //
    // Recommended refactoring approaches:
    // 1. Inject BlobServiceClient as a dependency (with interface or factory pattern)
    // 2. Create a testable wrapper/facade around Azure Blob Storage operations
    // 3. Use the Repository pattern to abstract storage operations
    // 4. Run integration tests against Azurite (Azure Storage Emulator)
    //
    // For comprehensive testing, consider using Azurite for integration tests:
    // - Azurite provides a local Azure Storage emulator for testing
    // - Tests can validate the complete upload flow
    // - Connection string: "UseDevelopmentStorage=true"
    //
    // Example integration test setup:
    /*
    [Fact]
    public async Task UploadBlobAsync_WithValidStream_UploadsSuccessfully()
    {
        // Arrange
        var options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true", // Azurite
            ContainerName = "test-container"
        });

        var service = new BlobStorageService(options);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));

        // Act
        UploadResult result = await service.UploadAsync("test.txt", stream, "text/plain");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test.txt", result.FileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(12, result.FileSizeBytes);
        Assert.NotEmpty(result.ETag);
        Assert.Equal("test-container", result.ContainerName);
    }

    [Fact]
    public async Task UploadBlobAsync_WithLargeFile_UploadsSuccessfully()
    {
        // This test would verify:
        // - Large files (e.g., 100MB) upload correctly
        // - File size is accurately reported
        // - Stream is properly disposed
    }

    [Fact]
    public async Task UploadBlobAsync_WithCustomContentType_SetsContentTypeCorrectly()
    {
        // This test would verify:
        // - Content type is set on the blob
        // - Content type is included in the result
    }

    [Fact]
    public async Task UploadBlobAsync_WithNoContentType_UploadsWithoutContentType()
    {
        // This test would verify:
        // - Upload succeeds without content type
        // - Result has null content type
    }

    [Fact]
    public async Task UploadBlobAsync_CreatesContainerIfNotExists()
    {
        // This test would verify:
        // - Container is created when it doesn't exist
        // - Subsequent uploads to existing container succeed
    }

    [Fact]
    public async Task UploadBlobAsync_WithExistingBlob_OverwritesBlob()
    {
        // This test would verify:
        // - Uploading to an existing blob name overwrites it
        // - ETag changes after overwrite
    }

    [Fact]
    public async Task UploadBlobAsync_WithConnectionString_UsesConnectionString()
    {
        // This test would verify:
        // - Service uses connection string when provided
        // - Upload succeeds using connection string
    }

    [Fact]
    public async Task UploadBlobAsync_WithAccountNameOnly_UsesManagedIdentity()
    {
        // This test would verify:
        // - Service uses Managed Identity when only account name is provided
        // - Proper URI is constructed
        // NOTE: This requires a real Azure environment or sophisticated mocking
    }

    [Fact]
    public async Task UploadBlobAsync_WithEmptyStream_UploadsZeroByteFile()
    {
        // This test would verify:
        // - Empty streams are handled correctly
        // - Result reports zero bytes
    }

    [Fact]
    public async Task UploadBlobAsync_WithSpecialCharactersInFileName_UploadsCorrectly()
    {
        // This test would verify:
        // - File names with spaces, special characters are handled
        // - Result contains the correct file name
    }

    [Fact]
    public async Task UploadBlobAsync_ReturnsCorrectBlobUrl()
    {
        // This test would verify:
        // - Returned URL is accessible
        // - URL format is correct
        // - URL includes container and blob name
    }

    [Fact]
    public async Task UploadBlobAsync_ReturnsValidETag()
    {
        // This test would verify:
        // - ETag is returned
        // - ETag format is valid
        // - ETag can be used for conditional operations
    }
    */

    #endregion

    #region Azurite Integration Test Example

    // To run integration tests with Azurite:
    // 1. Install Azurite: npm install -g azurite
    // 2. Start Azurite: azurite --silent --location c:\azurite --debug c:\azurite\debug.log
    // 3. Use connection string: "UseDevelopmentStorage=true"
    //
    // Alternatively, use the Docker container:
    // docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
    //
    // Mark integration tests with [Trait("Category", "Integration")] to separate from unit tests
    // This allows running: dotnet test --filter "Category!=Integration"

    /*
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Integration_UploadBlobAsync_WithAzurite_UploadsSuccessfully()
    {
        // Arrange
        var options = Options.Create(new AzureStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = $"test-{Guid.NewGuid()}"
        });

        var service = new BlobStorageService(options);
        string testContent = "Integration test content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));
        string fileName = $"test-{Guid.NewGuid()}.txt";

        // Act
        UploadResult result = await service.UploadAsync(fileName, stream, "text/plain");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileName, result.FileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(testContent.Length, result.FileSizeBytes);
        Assert.NotEmpty(result.ETag);
        Assert.Contains(fileName, result.BlobUrl);
    }
    */

    #endregion
}
