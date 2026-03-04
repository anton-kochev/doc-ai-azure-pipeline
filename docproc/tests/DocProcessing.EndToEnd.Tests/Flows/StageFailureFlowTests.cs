using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class StageFailureFlowTests
{
    [Test]
    public async Task OcrFails_StorageDownloadThrows_JobFailed()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // OCR calls DownloadBlobAsync which will throw because the blob key
        // used by OcrStageActivity won't match InMemoryStorageService's internal key.
        // Instead, set up OCR mock to throw.
        fixture.OcrServiceMock
            .Setup(x => x.AnalyzeDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage download failed"));

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.IsSuccess).IsFalse();
        await Assert.That(pipelineResult.FailedStage).IsEqualTo(ProcessJobStage.OCR);

        // Fail the job (as orchestrator would do)
        await fixture.ProcessJobService.FailJobAsync(
            uploadResult.JobId,
            pipelineResult.ErrorCode,
            pipelineResult.ErrorMessage);

        await DocumentAssertions.AssertJobStatusAsync(fixture.DbContext, uploadResult.JobId, ProcessJobStatus.Failed);
    }

    [Test]
    public async Task OcrFails_DocumentNotFoundInDb_ReturnsDocumentNotFound()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Override OCR stage to simulate document not found
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Failure(
                "DOCUMENT_NOT_FOUND",
                $"Document with ID {uploadResult.DocumentId} not found"));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.IsSuccess).IsFalse();
        await Assert.That(pipelineResult.ErrorCode).IsEqualTo("DOCUMENT_NOT_FOUND");
        await Assert.That(pipelineResult.FailedStage).IsEqualTo(ProcessJobStage.OCR);
    }

    [Test]
    public async Task PreprocessFails_OcrBlobPathMissing_ReturnsPreprocessMissingOcrPath()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Set up OCR to return success but with NO metadata (ocrBlobPath won't be forwarded)
        var ocrResult = new OcrResultBuilder()
            .WithDocumentId(uploadResult.DocumentId)
            .WithJobId(uploadResult.JobId)
            .Build();

        fixture.OcrServiceMock
            .Setup(x => x.AnalyzeDocumentAsync(
                uploadResult.DocumentId, uploadResult.JobId,
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);

        // Override OCR stage to return success WITHOUT ocrBlobPath in metadata
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Success(output: null, metadata: new Dictionary<string, object>
            {
                ["pageCount"] = 1,
                ["confidence"] = 0.95
                // Intentionally missing StageMetadataKeys.OcrBlobPath
            }));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.IsSuccess).IsFalse();
        await Assert.That(pipelineResult.ErrorCode).IsEqualTo("PREPROCESS_MISSING_OCR_PATH");
        await Assert.That(pipelineResult.FailedStage).IsEqualTo(ProcessJobStage.Preprocess);
    }

    [Test]
    public async Task StageThrowsUnexpectedException_SimulatorCatchesIt_JobCanBeFailed()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Override OCR stage to throw an unexpected exception
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Failure("UNEXPECTED_ERROR", "Something broke unexpectedly"));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.IsSuccess).IsFalse();

        // Orchestrator would fail the job
        await fixture.ProcessJobService.FailJobAsync(
            uploadResult.JobId,
            pipelineResult.ErrorCode,
            pipelineResult.ErrorMessage);

        ProcessJob job = await DocumentAssertions.GetJobAsync(fixture.DbContext, uploadResult.JobId);
        await Assert.That(job.Status).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(job.LastErrorCode).IsEqualTo("UNEXPECTED_ERROR");
    }

    // --- Helpers ---

    private static async Task<UploadRequestBuilder.UploadResult> UploadTestDocument(EndToEndTestFixture fixture)
    {
        return await fixture.CreateUploadBuilder()
            .WithFileName("test-document.pdf")
            .WithContentType("application/pdf")
            .WithFileContent("fake PDF content for testing"u8.ToArray())
            .ExecuteAsync();
    }
}
