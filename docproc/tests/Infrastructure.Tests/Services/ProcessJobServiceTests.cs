using DocProcessing.Application.Services;
using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using DocProcessing.TestUtilities.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Infrastructure.Tests.Services;

public class ProcessJobServiceTests : IDisposable
{
    private readonly InMemoryDbContext _dbContext;
    private readonly FakeLogger<ProcessJobService> _logger;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ProcessJobService _service;

    public ProcessJobServiceTests()
    {
        _dbContext = new InMemoryDbContext();
        _logger = new FakeLogger<ProcessJobService>();
        _timeProvider = new FakeTimeProvider();
        _service = new ProcessJobService(_dbContext, _logger, _timeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region ComputeIdempotencyKey Tests

    [Test]
    public async Task ComputeIdempotencyKey_WithAllParameters_ReturnsConsistentKey()
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
        await Assert.That(key1).IsNotEmpty();
        await Assert.That(key1).IsEqualTo(key2);
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithNullTenantId_UsesDefaultValue()
    {
        // Arrange
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        string extractionProfile = "invoice";

        // Act
        string key = _service.ComputeIdempotencyKey(null, sha256Hash, extractionProfile);

        // Assert
        await Assert.That(key).IsNotEmpty();
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithNullExtractionProfile_UsesDefaultValue()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        string key = _service.ComputeIdempotencyKey(tenantId, sha256Hash, null);

        // Assert
        await Assert.That(key).IsNotEmpty();
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithDifferentTenants_ReturnsDifferentKeys()
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
        await Assert.That(key1).IsNotEqualTo(key2);
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithDifferentHashes_ReturnsDifferentKeys()
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
        await Assert.That(key1).IsNotEqualTo(key2);
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithDifferentProfiles_ReturnsDifferentKeys()
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
        await Assert.That(key1).IsNotEqualTo(key2);
    }

    [Test]
    public async Task ComputeIdempotencyKey_ReturnsBase64StringWithoutPadding()
    {
        // Arrange
        Guid tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string extractionProfile = "invoice";

        // Act
        string key = _service.ComputeIdempotencyKey(tenantId, sha256Hash, extractionProfile);

        // Assert
        await Assert.That(key).DoesNotContain("="); // No padding characters
        await Assert.That(key).Matches("^[A-Za-z0-9+/]+$"); // Valid base64 characters
    }

    [Test]
    public async Task ComputeIdempotencyKey_WithNullTenantAndNullProfile_ReturnsConsistentKey()
    {
        // Arrange
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        string key1 = _service.ComputeIdempotencyKey(null, sha256Hash, null);
        string key2 = _service.ComputeIdempotencyKey(null, sha256Hash, null);

        // Assert
        await Assert.That(key1).IsEqualTo(key2);
    }

    #endregion

    #region GetOrCreateJobAsync Tests

    [Test]
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
        await Assert.That(isNew).IsTrue();
        await Assert.That(jobId).IsNotEqualTo(Guid.Empty);

        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.DocumentId).IsEqualTo(documentId);
        await Assert.That(savedJob.Status).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(savedJob.Stage).IsEqualTo(ProcessJobStage.Uploaded);
        await Assert.That(savedJob.Attempts).IsEqualTo(0);
        await Assert.That(savedJob.CorrelationId).IsEqualTo(correlationId);
        await Assert.That(savedJob.ExtractionProfile).IsEqualTo(extractionProfile);
        await Assert.That(savedJob.Priority).IsEqualTo(priority);
        await Assert.That(savedJob.IdempotencyKey).IsNotEmpty();
        await Assert.That(savedJob.CreatedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
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
        await Assert.That(firstIsNew).IsTrue();
        await Assert.That(secondIsNew).IsFalse();
        await Assert.That(secondJobId).IsEqualTo(firstJobId);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Processing;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        await Assert.That(secondIsNew).IsFalse();
        await Assert.That(secondJobId).IsEqualTo(firstJobId);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        await Assert.That(secondIsNew).IsTrue();
        await Assert.That(secondJobId).IsNotEqualTo(firstJobId);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act - Try to create another job with same parameters
        (Guid secondJobId, bool secondIsNew) = await _service.GetOrCreateJobAsync(
            documentId,
            tenantId,
            sha256Hash,
            extractionProfile);

        // Assert
        await Assert.That(secondIsNew).IsTrue();
        await Assert.That(secondJobId).IsNotEqualTo(firstJobId);
    }

    [Test]
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
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.CorrelationId).IsNotNull();
        await Assert.That(savedJob.CorrelationId).IsNotEmpty();
        // Should be a valid GUID format
        await Assert.That(Guid.TryParse(savedJob.CorrelationId, out _)).IsTrue();
    }

    [Test]
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
        await Assert.That(job1IsNew).IsTrue();
        await Assert.That(job2IsNew).IsTrue();
        await Assert.That(job1Id).IsNotEqualTo(job2Id);

        ProcessJob? job1 = await _dbContext.ProcessJobs.FindAsync(job1Id);
        ProcessJob? job2 = await _dbContext.ProcessJobs.FindAsync(job2Id);
        await Assert.That(job1).IsNotNull();
        await Assert.That(job2).IsNotNull();
        await Assert.That(job1!.ExtractionProfile).IsEqualTo("invoice");
        await Assert.That(job2!.ExtractionProfile).IsEqualTo("receipt");
    }

    [Test]
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
        await Assert.That(isNew).IsTrue();
        ProcessJob? savedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.ExtractionProfile).IsNull();
    }

    [Test]
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
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.Priority).IsEqualTo((byte)0);
    }

    [Test]
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
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.Priority).IsEqualTo((byte)255);
    }

    [Test]
    public async Task GetOrCreateJobAsync_LogsDebugForLookup()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Debug, "Looking for existing job with idempotency key");
    }

    [Test]
    public async Task GetOrCreateJobAsync_WhenCreatingJob_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Act
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Information, "Created new process job");
    }

    [Test]
    public async Task GetOrCreateJobAsync_WhenReturningExistingJob_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        ];

        // Create first job
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act - Try to create again
        _logger.Collector.Clear(); // Clear logs from first creation
        await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Information, "Found existing non-terminal job");
    }

    [Test]
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
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.CreatedAtUtc).IsEqualTo(expectedTime.UtcDateTime);
    }

    [Test]
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
        await Assert.That(savedJob).IsNotNull();
        await Assert.That(savedJob!.Attempts).IsEqualTo(0);
    }

    #endregion

    #region StartProcessingAsync Tests

    [Test]
    public async Task StartProcessingAsync_WhenJobIsPending_TransitionsToProcessing()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        await _service.StartProcessingAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.StartedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Attempts).IsEqualTo(1);
    }

    [Test]
    public async Task StartProcessingAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.StartProcessingAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task StartProcessingAsync_WhenJobIsProcessing_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Processing;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.StartProcessingAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Processing);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task StartProcessingAsync_WhenJobIsCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.StartProcessingAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task StartProcessingAsync_WhenJobIsFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.StartProcessingAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task StartProcessingAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act
        _logger.Collector.Clear();
        await _service.StartProcessingAsync(jobId);

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Information, "Job transitioned to Processing");
    }

    [Test]
    public async Task StartProcessingAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        await Assert.That(
            () => _service.StartProcessingAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Warning, "Cannot update job. Job not found");
    }

    #endregion

    #region CompleteJobAsync Tests

    [Test]
    public async Task CompleteJobAsync_WhenJobIsProcessing_TransitionsToCompleted()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        await _service.CompleteJobAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.Completed);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.CompletedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task CompleteJobAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.CompleteJobAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CompleteJobAsync_WhenJobIsPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.CompleteJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Completed);
    }

    [Test]
    public async Task CompleteJobAsync_WhenJobIsAlreadyCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.CompleteJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Completed);
    }

    [Test]
    public async Task CompleteJobAsync_WhenJobIsFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.CompleteJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Completed);
    }

    [Test]
    public async Task CompleteJobAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        _logger.Collector.Clear();
        await _service.CompleteJobAsync(jobId);

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Information, "Job completed successfully");
    }

    [Test]
    public async Task CompleteJobAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        await Assert.That(
            () => _service.CompleteJobAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Warning, "Cannot update job. Job not found");
    }

    #endregion

    #region FailJobAsync Tests

    [Test]
    public async Task FailJobAsync_WhenJobIsProcessing_TransitionsToFailed()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error message");

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.CompletedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsEqualTo(errorCode);
        await Assert.That(job.LastErrorMessage).IsEqualTo(errorMessage);
    }

    [Test]
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
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsNull();
        await Assert.That(job.LastErrorMessage).IsNull();
    }

    [Test]
    public async Task FailJobAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.FailJobAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task FailJobAsync_WhenJobIsPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.FailJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task FailJobAsync_WhenJobIsCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Completed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.FailJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task FailJobAsync_WhenJobIsAlreadyFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        job!.Status = ProcessJobStatus.Failed;
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.FailJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task FailJobAsync_WhenSuccessful_LogsError()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash =
        [
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
            30, 31, 32
        ];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        _logger.Collector.Clear();
        await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error");

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Error, "Job failed");
    }

    [Test]
    public async Task FailJobAsync_WhenJobNotFound_LogsWarning()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        await Assert.That(
            () => _service.FailJobAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        // Assert - Verify log entry was created with expected message
        _logger.VerifyWasCalled(LogLevel.Warning, "Cannot update job. Job not found");
    }

    #endregion

    #region RetryFailedJobAsync Tests

    [Test]
    public async Task RetryFailedJobAsync_WhenJobIsFailed_ResetsJobToPendingAndReturnsCorrelationId()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Move job to Failed state
        await _service.StartProcessingAsync(jobId);
        await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error message");

        ProcessJob? failedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(failedJob).IsNotNull();
        await Assert.That(failedJob!.Status).IsEqualTo(ProcessJobStatus.Failed);

        // Act
        string correlationId = await _service.RetryFailedJobAsync(jobId);

        // Assert
        ProcessJob? retriedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(retriedJob).IsNotNull();
        await Assert.That(retriedJob!.Status).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(retriedJob.Stage).IsEqualTo(ProcessJobStage.Uploaded);
        await Assert.That(correlationId).IsNotEmpty();
        await Assert.That(retriedJob.CorrelationId).IsEqualTo(correlationId);
        await Assert.That(Guid.TryParse(correlationId, out _)).IsTrue(); // Should be a valid GUID
    }

    [Test]
    public async Task RetryFailedJobAsync_WhenJobIsFailed_ClearsErrorInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        await _service.StartProcessingAsync(jobId);
        await _service.FailJobAsync(jobId, "ORIGINAL_ERROR", "Original error message");

        ProcessJob? failedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(failedJob).IsNotNull();
        await Assert.That(failedJob!.LastErrorCode).IsEqualTo("ORIGINAL_ERROR");
        await Assert.That(failedJob.LastErrorMessage).IsEqualTo("Original error message");

        // Act
        await _service.RetryFailedJobAsync(jobId);

        // Assert
        ProcessJob? retriedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(retriedJob).IsNotNull();
        await Assert.That(retriedJob!.LastErrorCode).IsNull();
        await Assert.That(retriedJob.LastErrorMessage).IsNull();
    }

    [Test]
    public async Task RetryFailedJobAsync_WhenJobIsFailed_ClearsTimestamps()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        await _service.StartProcessingAsync(jobId);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await _service.FailJobAsync(jobId, "TEST_ERROR", "Test error");

        ProcessJob? failedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(failedJob).IsNotNull();
        await Assert.That(failedJob!.StartedAtUtc).IsNotNull();
        await Assert.That(failedJob.CompletedAtUtc).IsNotNull();

        // Act
        await _service.RetryFailedJobAsync(jobId);

        // Assert
        ProcessJob? retriedJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(retriedJob).IsNotNull();
        await Assert.That(retriedJob!.StartedAtUtc).IsNull();
        await Assert.That(retriedJob.CompletedAtUtc).IsNull();
    }

    [Test]
    public async Task RetryFailedJobAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            async () => await _service.RetryFailedJobAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task RetryFailedJobAsync_WhenJobIsPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act & Assert
        var exception = await Assert.That(
            async () => await _service.RetryFailedJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Pending);
    }

    [Test]
    public async Task RetryFailedJobAsync_WhenJobIsProcessing_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            async () => await _service.RetryFailedJobAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Processing);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Pending);
    }

    #endregion

    #region RequestManualReviewAsync Tests

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsProcessing_TransitionsToManualReview()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        await _service.RequestManualReviewAsync(jobId, "Low confidence score");

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsProcessing_SetsCompletedAtUtc()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await _service.RequestManualReviewAsync(jobId, "Data validation failed");

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.CompletedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsProcessing_SetsReviewReason()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        const string reviewReason = "Suspicious document format";

        // Act
        await _service.RequestManualReviewAsync(jobId, reviewReason);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsEqualTo("MANUAL_REVIEW_REQUIRED");
        await Assert.That(job.LastErrorMessage).IsEqualTo(reviewReason);
    }

    [Test]
    public async Task RequestManualReviewAsync_WithoutReason_SetsDefaultReviewReason()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act
        await _service.RequestManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsEqualTo("MANUAL_REVIEW_REQUIRED");
        await Assert.That(job.LastErrorMessage).IsEqualTo("Manual review required");
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RequestManualReviewAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RequestManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.CompleteJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RequestManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.FailJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RequestManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task RequestManualReviewAsync_WhenJobIsManualReview_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "First review");

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RequestManualReviewAsync(jobId, "Second review")).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.ManualReview);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.ManualReview);
    }

    #endregion

    #region ResumeFromManualReviewAsync Tests

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsManualReview_TransitionsToProcessing()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        // Act
        await _service.ResumeFromManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsManualReview_ClearsCompletedAtUtc()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        ProcessJob? reviewJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(reviewJob).IsNotNull();
        await Assert.That(reviewJob!.CompletedAtUtc).IsNotNull();

        // Act
        await _service.ResumeFromManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.CompletedAtUtc).IsNull();
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsManualReview_ClearsErrorInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Low confidence");

        ProcessJob? reviewJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(reviewJob).IsNotNull();
        await Assert.That(reviewJob!.LastErrorCode).IsEqualTo("MANUAL_REVIEW_REQUIRED");
        await Assert.That(reviewJob.LastErrorMessage).IsEqualTo("Low confidence");

        // Act
        await _service.ResumeFromManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsNull();
        await Assert.That(job.LastErrorMessage).IsNull();
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsManualReview_IncrementsAttempts()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        ProcessJob? processingJob = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(processingJob).IsNotNull();
        int attemptsBefore = processingJob!.Attempts;

        await _service.RequestManualReviewAsync(jobId, "Needs review");

        // Act
        await _service.ResumeFromManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Attempts).IsEqualTo(attemptsBefore + 1);
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.ResumeFromManualReviewAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsProcessing_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.ResumeFromManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Processing);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.CompleteJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.ResumeFromManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task ResumeFromManualReviewAsync_WhenJobIsFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.FailJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.ResumeFromManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Processing);
    }

    #endregion

    #region RejectManualReviewAsync Tests

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsManualReview_TransitionsToFailed()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        // Act
        await _service.RejectManualReviewAsync(jobId, "INVALID_DOCUMENT", "Document is invalid");

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.Status).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsManualReview_SetsCompletedAtUtc()
    {
        // Arrange
        DateTimeOffset startTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(startTime);

        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        _timeProvider.Advance(TimeSpan.FromHours(1));

        // Act
        await _service.RejectManualReviewAsync(jobId, "REJECTED", "Manual rejection");

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.CompletedAtUtc).IsEqualTo(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsManualReview_SetsErrorInformation()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        const string errorCode = "INVALID_FORMAT";
        const string errorMessage = "Document format is not supported";

        // Act
        await _service.RejectManualReviewAsync(jobId, errorCode, errorMessage);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsEqualTo(errorCode);
        await Assert.That(job.LastErrorMessage).IsEqualTo(errorMessage);
    }

    [Test]
    public async Task RejectManualReviewAsync_WithoutErrorInfo_SetsDefaultErrorFields()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.RequestManualReviewAsync(jobId, "Needs review");

        // Act
        await _service.RejectManualReviewAsync(jobId);

        // Assert
        ProcessJob? job = await _dbContext.ProcessJobs.FindAsync(jobId);
        await Assert.That(job).IsNotNull();
        await Assert.That(job!.LastErrorCode).IsEqualTo("MANUAL_REVIEW_REJECTED");
        await Assert.That(job.LastErrorMessage).IsEqualTo("Manually rejected during review");
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobNotFound_ThrowsJobNotFoundException()
    {
        // Arrange
        Guid nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RejectManualReviewAsync(nonExistentJobId)).ThrowsExactly<JobNotFoundException>();

        await Assert.That(exception!.JobId).IsEqualTo(nonExistentJobId);
        await Assert.That(exception!.Message).Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RejectManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsCompleted_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.CompleteJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RejectManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task RejectManualReviewAsync_WhenJobIsFailed_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();
        byte[] sha256Hash = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
        (Guid jobId, _) = await _service.GetOrCreateJobAsync(documentId, null, sha256Hash);
        await _service.StartProcessingAsync(jobId);
        await _service.FailJobAsync(jobId);

        // Act & Assert
        var exception = await Assert.That(
            () => _service.RejectManualReviewAsync(jobId)).ThrowsExactly<InvalidStateTransitionException>();

        await Assert.That(exception!.JobId).IsEqualTo(jobId);
        await Assert.That(exception!.CurrentStatus).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(exception!.AttemptedStatus).IsEqualTo(ProcessJobStatus.Failed);
    }

    #endregion
}
