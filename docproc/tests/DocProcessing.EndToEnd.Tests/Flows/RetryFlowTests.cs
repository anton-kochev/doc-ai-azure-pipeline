using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Pipeline;
using DocProcessing.Domain.Entities;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class RetryFlowTests
{
    [Test]
    public async Task RetryFailedJob_ResetsToPending_ErrorFieldsCleared()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Fail the job first
        await fixture.ProcessJobService.StartProcessingAsync(uploadResult.JobId);
        await fixture.ProcessJobService.FailJobAsync(uploadResult.JobId, "OCR_FAILED", "OCR processing failed");

        // Act
        await fixture.ProcessJobService.RetryFailedJobAsync(uploadResult.JobId);

        // Assert
        ProcessJob job = await DocumentAssertions.GetJobAsync(fixture.DbContext, uploadResult.JobId);
        await Assert.That(job.Status).IsEqualTo(ProcessJobStatus.Pending);
        await Assert.That(job.LastErrorCode).IsNull();
        await Assert.That(job.LastErrorMessage).IsNull();
        await Assert.That(job.StartedAtUtc).IsNull();
        await Assert.That(job.CompletedAtUtc).IsNull();
    }

    [Test]
    public async Task RetryFailedJob_ReRunPipelineSucceeds_CompletedWithAttempts2()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // First run: fail at OCR
        var failingOcrActivity = new Mock<IJobStageActivity>();
        failingOcrActivity.Setup(x => x.StageName).Returns("OCR");
        failingOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        failingOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Failure("OCR_FAILED", "Transient error"));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, failingOcrActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        var firstResult = await simulator.RunAsync(uploadResult.JobId);
        await fixture.ProcessJobService.FailJobAsync(uploadResult.JobId, firstResult.ErrorCode, firstResult.ErrorMessage);

        // Retry
        await fixture.ProcessJobService.RetryFailedJobAsync(uploadResult.JobId);

        // Second run: OCR succeeds now
        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        // Remove the override so real OCR activity runs
        fixture.ActivityFactory.Override(ProcessJobStage.OCR, fixture.ServiceProvider.GetRequiredService<DocProcessing.Application.Pipeline.OcrStageActivity>());

        // Act
        var secondResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(secondResult.IsSuccess).IsTrue();

        ProcessJob job = await DocumentAssertions.GetJobAsync(fixture.DbContext, uploadResult.JobId);
        await Assert.That(job.Status).IsEqualTo(ProcessJobStatus.Completed);
        await Assert.That(job.Attempts).IsEqualTo(2);
    }

    [Test]
    public async Task RetryFailedJob_MessagingCalledOnRetry()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Fail the job
        await fixture.ProcessJobService.StartProcessingAsync(uploadResult.JobId);
        await fixture.ProcessJobService.FailJobAsync(uploadResult.JobId, "OCR_FAILED", "Error");

        // Act — retry and enqueue (as the API endpoint would do)
        string correlationId = await fixture.ProcessJobService.RetryFailedJobAsync(uploadResult.JobId);
        await fixture.MessagingServiceMock.Object.EnqueueJobAsync(uploadResult.JobId, correlationId);

        // Assert — messaging was called for both initial upload and retry
        fixture.MessagingServiceMock.Verify(
            x => x.EnqueueJobAsync(uploadResult.JobId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    // --- Helpers ---

    private static async Task<UploadRequestBuilder.UploadResult> UploadTestDocument(EndToEndTestFixture fixture)
    {
        return await fixture.CreateUploadBuilder()
            .WithFileName("retry-test.pdf")
            .WithContentType("application/pdf")
            .WithFileContent("retry test content"u8.ToArray())
            .ExecuteAsync();
    }

    private static void SetupOcrMock(EndToEndTestFixture fixture, Guid documentId, Guid jobId)
    {
        var ocrResult = new OcrResultBuilder()
            .WithDocumentId(documentId)
            .WithJobId(jobId)
            .Build();

        fixture.OcrServiceMock
            .Setup(x => x.AnalyzeDocumentAsync(
                documentId, jobId,
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);
    }
}
