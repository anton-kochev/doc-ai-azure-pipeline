using System.Text.Json;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Domain.Entities;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class HappyPathFlowTests
{
    [Test]
    public async Task FullPipeline_AllStagesSucceed_JobCompleted()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.IsSuccess).IsTrue();
        await DocumentAssertions.AssertJobStatusAsync(fixture.DbContext, uploadResult.JobId, ProcessJobStatus.Completed);
    }

    [Test]
    public async Task FullPipeline_OcrCalledWithCorrectDocumentBlobPath()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — OCR was called exactly once with correct IDs
        fixture.OcrServiceMock.Verify(
            x => x.AnalyzeDocumentAsync(
                uploadResult.DocumentId,
                uploadResult.JobId,
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task FullPipeline_OcrResultStoredInBlobStorage()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — OCR result JSON was stored in blob storage
        string expectedBlobPath = $"default/{uploadResult.DocumentId}/ocr-result.json";
        await Assert.That(fixture.StorageService.BlobExists("ocr-results", expectedBlobPath)).IsTrue();
    }

    [Test]
    public async Task FullPipeline_DocumentMetadataUpdatedAfterOcr()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — Document metadata contains expected OCR keys
        Document doc = await DocumentAssertions.GetDocumentAsync(fixture.DbContext, uploadResult.DocumentId);
        await Assert.That(doc.MetadataJson).IsNotNull();

        using var jsonDoc = JsonDocument.Parse(doc.MetadataJson!);
        await Assert.That(jsonDoc.RootElement.TryGetProperty("ocrCompleted", out _)).IsTrue();
        await Assert.That(jsonDoc.RootElement.TryGetProperty("pageCount", out _)).IsTrue();
        await Assert.That(jsonDoc.RootElement.TryGetProperty("ocrBlobPath", out _)).IsTrue();
    }

    [Test]
    public async Task FullPipeline_JobAttemptsAndTimestampsSetCorrectly()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert
        ProcessJob job = await DocumentAssertions.GetJobAsync(fixture.DbContext, uploadResult.JobId);
        await Assert.That(job.Attempts).IsEqualTo(1);
        await Assert.That(job.StartedAtUtc).IsNotNull();
        await Assert.That(job.CompletedAtUtc).IsNotNull();
        await Assert.That(job.CompletedAtUtc!.Value).IsGreaterThanOrEqualTo(job.StartedAtUtc!.Value);
    }

    [Test]
    public async Task FullPipeline_OcrBlobPathPresentInMetadataAfterOcrStage()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        SetupOcrMock(fixture, uploadResult.DocumentId, uploadResult.JobId);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert — documents the data-flow contract: ocrBlobPath is forwarded
        await Assert.That(pipelineResult.AccumulatedMetadata.ContainsKey("ocrBlobPath")).IsTrue();
    }

    [Test]
    public async Task PipelineSimulatorStages_MatchOrchestratorStageSequence()
    {
        // This guard test verifies our simulator uses the same stage order as the orchestrator.
        // If this test fails, PipelineSimulator.Stages is out of sync.
        var expected = new[]
        {
            ProcessJobStage.OCR,
            ProcessJobStage.Preprocess,
            ProcessJobStage.Embed,
            ProcessJobStage.Extract,
            ProcessJobStage.Validate,
            ProcessJobStage.Persist,
            ProcessJobStage.Notify
        };

        await Assert.That(PipelineSimulator.Stages).IsEquivalentTo(expected);
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

    private static void SetupOcrMock(EndToEndTestFixture fixture, Guid documentId, Guid jobId)
    {
        var ocrResult = new OcrResultBuilder()
            .WithDocumentId(documentId)
            .WithJobId(jobId)
            .WithPageCount(2)
            .WithConfidence(0.95)
            .Build();

        fixture.OcrServiceMock
            .Setup(x => x.AnalyzeDocumentAsync(
                documentId,
                jobId,
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrResult);
    }
}
