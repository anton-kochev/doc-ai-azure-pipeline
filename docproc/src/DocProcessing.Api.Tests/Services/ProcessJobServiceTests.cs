using DocProcessing.Api.Data;
using DocProcessing.Api.Services;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DocProcessing.Api.Tests.Services;

public class ProcessJobServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<ILogger<ProcessJobService>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ProcessJobService _service;

    public ProcessJobServiceTests()
    {
        // Create a unique database name for each test to ensure isolation
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ProcessJobService>>();
        _timeProvider = new FakeTimeProvider();
        _service = new ProcessJobService(_dbContext, _loggerMock.Object, _timeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region ComputeIdempotencyKey Tests

    [Fact]
    public void ComputeIdempotencyKey_WithAllParameters_ReturnsConsistentKey()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        string extractionProfile = "invoice";

        // Act
        string key1 = _service.ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);
        string key2 = _service.ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);

        // Assert
        Assert.NotEmpty(key1);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void ComputeIdempotencyKey_WithNullTenantId_UsesDefaultValue()
    {
        // Arrange
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        string extractionProfile = "invoice";

        // Act
        string key = _service.ComputeIdempotencyKey(null, sha256Hash, extractionProfile);

        // Assert
        Assert.NotEmpty(key);
    }

    [Fact]
    public void ComputeIdempotencyKey_WithNullExtractionProfile_UsesDefaultValue()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        string key = _service.ComputeIdempotencyKey(tenantId, sha256Hash, null);

        // Assert
        Assert.NotEmpty(key);
    }

    [Fact]
    public void ComputeIdempotencyKey_WithDifferentTenants_ReturnsDifferentKeys()
    {
        // Arrange
        Guid tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Act
        string key1 = _service.ComputeIdempotencyKey(tenant1, sha256Hash, extractionProfile);
        string key2 = _service.ComputeIdempotencyKey(tenant2, sha256Hash, extractionProfile);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ComputeIdempotencyKey_WithDifferentHashes_ReturnsDifferentKeys()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] hash1 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        byte[] hash2 = [32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
        ];
        const string extractionProfile = "invoice";

        // Act
        string key1 = _service.ComputeIdempotencyKey(tenantId, hash1, extractionProfile);
        string key2 = _service.ComputeIdempotencyKey(tenantId, hash2, extractionProfile);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ComputeIdempotencyKey_WithDifferentProfiles_ReturnsDifferentKeys()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string profile1 = "invoice";
        const string profile2 = "receipt";

        // Act
        string key1 = _service.ComputeIdempotencyKey(tenantId, sha256Hash, profile1);
        string key2 = _service.ComputeIdempotencyKey(tenantId, sha256Hash, profile2);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ComputeIdempotencyKey_ReturnsBase64StringWithoutPadding()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Act
        string key = _service.ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);

        // Assert
        Assert.DoesNotContain("=", key); // No padding characters
        Assert.Matches("^[A-Za-z0-9+/]+$", key); // Valid base64 characters
    }

    [Fact]
    public void ComputeIdempotencyKey_WithNullTenantAndNullProfile_ReturnsConsistentKey()
    {
        // Arrange
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        string key1 = _service.ComputeIdempotencyKey(null, sha256Hash, null);
        string key2 = _service.ComputeIdempotencyKey(null, sha256Hash, null);

        // Assert
        Assert.Equal(key1, key2);
    }

    #endregion

    #region GetOrCreateJobAsync Tests

    [Fact]
    public async Task GetOrCreateJobAsync_WhenNoJobExists_CreatesNewJob()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";
        const string correlationId = "test-correlation-id";
        const byte priority = 5;

        // Act
        (Guid jobId, bool isNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile,
            correlationId,
            priority);

        // Assert
        Assert.True(isNew);
        Assert.NotEqual(Guid.Empty, jobId);

        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(documentId, savedJob.DocumentId);
        Assert.Equal(ProcessJobStatus.Pending, savedJob.Status);
        Assert.Equal(ProcessJobStage.Uploaded, savedJob.Stage);
        Assert.Equal(0, savedJob.Attempts);
        Assert.Equal(correlationId, savedJob.CorrelationId);
        Assert.Equal(extractionProfile, savedJob.ExtractionProfile);
        Assert.Equal(priority, savedJob.Priority);
        Assert.NotEmpty(savedJob.IdempotencyKey);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, savedJob.CreatedAtUtc);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenPendingJobExists_ReturnsExistingJob()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Create first job
        (Guid firstJobId, bool firstIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        Assert.True(firstIsNew);
        Assert.False(secondIsNew);
        Assert.Equal(firstJobId, secondJobId);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenProcessingJobExists_ReturnsExistingJob()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Create first job and mark as processing
        (Guid firstJobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(firstJobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Processing;
        await _dbContext.SaveChangesAsync();

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        Assert.False(secondIsNew);
        Assert.Equal(firstJobId, secondJobId);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenCompletedJobExists_CreatesNewJob()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Create first job and mark as completed
        (Guid firstJobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(firstJobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync();

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        Assert.True(secondIsNew);
        Assert.NotEqual(firstJobId, secondJobId);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenFailedJobExists_CreatesNewJob()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Create first job and mark as failed
        (Guid firstJobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(firstJobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync();

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        Assert.True(secondIsNew);
        Assert.NotEqual(firstJobId, secondJobId);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WithoutCorrelationId_GeneratesCorrelationId()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash);

        // Assert
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.NotNull(savedJob.CorrelationId);
        Assert.NotEmpty(savedJob.CorrelationId);
        // Should be a valid GUID format
        Assert.True(Guid.TryParse(savedJob.CorrelationId, out _));
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WithDifferentProfiles_CreatesSeperateJobs()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid job1Id, bool job1IsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            "invoice");

        (Guid job2Id, bool job2IsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            "receipt");

        // Assert
        Assert.True(job1IsNew);
        Assert.True(job2IsNew);
        Assert.NotEqual(job1Id, job2Id);

        ProcessJob? job1 = await _dbContext.ProcessJobs.FindAsync(job1Id);
        ProcessJob? job2 = await _dbContext.ProcessJobs.FindAsync(job2Id);
        Assert.NotNull(job1);
        Assert.NotNull(job2);
        Assert.Equal("invoice", job1.ExtractionProfile);
        Assert.Equal("receipt", job2.ExtractionProfile);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WithNullProfile_HandlesCorrectly()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid jobId, bool isNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash);

        // Assert
        Assert.True(isNew);
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Null(savedJob.ExtractionProfile);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WithDefaultPriority_SetsPriorityToZero()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            null,
            sha256Hash);

        // Assert
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(0, savedJob.Priority);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WithHighPriority_SetsPriorityCorrectly()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        byte priority = 255;

        // Act
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(
            documentId,
            null,
            sha256Hash,
            priority: priority);

        // Assert
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(255, savedJob.Priority);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_LogsDebugForLookup()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Looking for existing job with idempotency key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenCreatingJob_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Created new process job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_WhenReturningExistingJob_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Create first job
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        _loggerMock.Reset();

        // Act - Try to create again
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Found existing non-terminal job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_SetsCreatedAtUtcToCurrentTime()
    {
        // Arrange
        DateTimeOffset expectedTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(expectedTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(expectedTime.UtcDateTime, savedJob.CreatedAtUtc);
    }

    [Fact]
    public async Task GetOrCreateJobAsync_InitializesAttemptsToZero()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(0, savedJob.Attempts);
    }

    #endregion

    #region StartProcessingAsync Tests

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsPending_TransitionsToProcessing()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        bool result = await _service.StartProcessingAsync(jobId);

        // Assert
        Assert.True(result);
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(ProcessJobStatus.Processing, job.Status);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsPending_SetsStartedAtUtc()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Move time forward
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await _service.StartProcessingAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, job.StartedAtUtc);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsPending_IncrementsAttempts()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        await _service.StartProcessingAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobNotFound_ReturnsFalse()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        bool result = await _service.StartProcessingAsync(nonExistentJobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsProcessing_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Processing;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.StartProcessingAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsCompleted_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.StartProcessingAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobIsFailed_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.StartProcessingAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        _loggerMock.Reset();

        // Act
        await _service.StartProcessingAsync(jobId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Job transitioned to Processing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartProcessingAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        await _service.StartProcessingAsync(nonExistentJobId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cannot start processing: Job not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region CompleteJobAsync Tests

    [Fact]
    public async Task CompleteJobAsync_WhenJobIsProcessing_TransitionsToCompleted()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        bool result = await _service.CompleteJobAsync(jobId);

        // Assert
        Assert.True(result);
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(ProcessJobStatus.Completed, job.Status);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobIsProcessing_SetsCompletedAtUtc()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Move time forward
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        // Act
        await _service.CompleteJobAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, job.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobNotFound_ReturnsFalse()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        bool result = await _service.CompleteJobAsync(nonExistentJobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobIsPending_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        bool result = await _service.CompleteJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobIsAlreadyCompleted_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.CompleteJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobIsFailed_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.CompleteJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        _loggerMock.Reset();

        // Act
        await _service.CompleteJobAsync(jobId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Job completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteJobAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        await _service.CompleteJobAsync(nonExistentJobId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cannot complete job: Job not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region FailJobAsync Tests

    [Fact]
    public async Task FailJobAsync_WhenJobIsProcessing_TransitionsToFailed()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        bool result = await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error message");

        // Assert
        Assert.True(result);
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(ProcessJobStatus.Failed, job.Status);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobIsProcessing_SetsCompletedAtUtc()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Move time forward
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await _service.FailJobAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, job.CompletedAtUtc);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobIsProcessing_SetsErrorInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        const string errorCode = "PROCESSING_ERROR";
        const string errorMessage = "Failed to process document";

        // Act
        await _service.FailJobAsync(jobId, errorCode, errorMessage);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Equal(errorCode, job.LastErrorCode);
        Assert.Equal(errorMessage, job.LastErrorMessage);
    }

    [Fact]
    public async Task FailJobAsync_WithoutErrorInfo_SetsNullErrorFields()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        await _service.FailJobAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        Assert.Null(job.LastErrorCode);
        Assert.Null(job.LastErrorMessage);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobNotFound_ReturnsFalse()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        bool result = await _service.FailJobAsync(nonExistentJobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobIsPending_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        bool result = await _service.FailJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobIsCompleted_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.FailJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobIsAlreadyFailed_ReturnsFalse()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        Assert.NotNull(job);
        job.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync();

        // Act
        bool result = await _service.FailJobAsync(jobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task FailJobAsync_WhenSuccessful_LogsError()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        _loggerMock.Reset();

        // Act
        await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Job failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FailJobAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act
        await _service.FailJobAsync(nonExistentJobId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cannot fail job: Job not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
