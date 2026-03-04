using DocProcessing.Domain.Entities;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class IdempotencyFlowTests
{
    [Test]
    public async Task SameFileAndProfile_UploadedTwice_SameDocumentAndJobReturned()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        byte[] content = "identical content"u8.ToArray();

        // Act
        var first = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithFileName("doc.pdf")
            .ExecuteAsync();

        var second = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithFileName("doc.pdf")
            .ExecuteAsync();

        // Assert — same document and job returned
        await Assert.That(second.DocumentId).IsEqualTo(first.DocumentId);
        await Assert.That(second.JobId).IsEqualTo(first.JobId);
        await Assert.That(second.IsNewDocument).IsFalse();
        await Assert.That(second.IsNewJob).IsFalse();

        // Messaging should have been called only once (for the first upload)
        fixture.MessagingServiceMock.Verify(
            x => x.EnqueueJobAsync(first.JobId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SameFile_DifferentTenant_TwoDocumentsTwoJobs()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        byte[] content = "shared content across tenants"u8.ToArray();
        Guid tenant1 = Guid.NewGuid();
        Guid tenant2 = Guid.NewGuid();

        // Act
        var first = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithTenantId(tenant1)
            .ExecuteAsync();

        var second = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithTenantId(tenant2)
            .ExecuteAsync();

        // Assert — different documents and jobs
        await Assert.That(second.DocumentId).IsNotEqualTo(first.DocumentId);
        await Assert.That(second.JobId).IsNotEqualTo(first.JobId);
        await Assert.That(first.IsNewDocument).IsTrue();
        await Assert.That(second.IsNewDocument).IsTrue();
    }

    [Test]
    public async Task SameFile_DifferentProfile_SameDocumentNewJob()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        byte[] content = "content for profile test"u8.ToArray();

        // Act
        var first = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithExtractionProfile("profile-a")
            .ExecuteAsync();

        var second = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .WithExtractionProfile("profile-b")
            .ExecuteAsync();

        // Assert — same document (same hash + tenant), different jobs (different profile)
        await Assert.That(second.DocumentId).IsEqualTo(first.DocumentId);
        await Assert.That(second.JobId).IsNotEqualTo(first.JobId);
        await Assert.That(second.IsNewDocument).IsFalse();
        await Assert.That(second.IsNewJob).IsTrue();
    }

    [Test]
    public async Task JobInProcessing_DuplicateUpload_ReturnsSameJobId()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        byte[] content = "processing job content"u8.ToArray();

        var first = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .ExecuteAsync();

        // Transition job to Processing
        await fixture.ProcessJobService.StartProcessingAsync(first.JobId);

        // Act — upload same file again
        var second = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .ExecuteAsync();

        // Assert — same job returned (Processing is a non-terminal state)
        await Assert.That(second.JobId).IsEqualTo(first.JobId);
        await Assert.That(second.IsNewJob).IsFalse();
    }

    [Test]
    public async Task JobCompleted_DuplicateUpload_CreatesNewJob()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        byte[] content = "completed job content"u8.ToArray();

        var first = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .ExecuteAsync();

        // Complete the job
        await fixture.ProcessJobService.StartProcessingAsync(first.JobId);
        await fixture.ProcessJobService.CompleteJobAsync(first.JobId);

        // Act — upload same file again
        var second = await fixture.CreateUploadBuilder()
            .WithFileContent(content)
            .ExecuteAsync();

        // Assert — new job created (Completed is a terminal state)
        await Assert.That(second.JobId).IsNotEqualTo(first.JobId);
        await Assert.That(second.IsNewJob).IsTrue();
        // Same document though
        await Assert.That(second.DocumentId).IsEqualTo(first.DocumentId);
    }
}
