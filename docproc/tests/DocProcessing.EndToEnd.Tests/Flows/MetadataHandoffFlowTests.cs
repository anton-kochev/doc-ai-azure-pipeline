using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Pipeline;
using DocProcessing.Domain.Entities;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class MetadataHandoffFlowTests
{
    [Test]
    public async Task StageOutputDict_ForwardedToNextStage()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Override OCR stage to return data ONLY in Output dict (not Metadata)
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Success(
                output: new Dictionary<string, object>
                {
                    [StageMetadataKeys.OcrBlobPath] = "ocr-results/test/ocr-result.json",
                    ["customOutputKey"] = "outputValue"
                },
                metadata: null));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        // Override Preprocess stage to capture metadata it receives and verify forwarding
        StageContext? capturedPreprocessContext = null;
        var mockPreprocessActivity = new Mock<IJobStageActivity>();
        mockPreprocessActivity.Setup(x => x.StageName).Returns("Preprocess");
        mockPreprocessActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Preprocess);
        mockPreprocessActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .Callback<StageContext, CancellationToken>((ctx, _) => capturedPreprocessContext = ctx)
            .ReturnsAsync(StageResult.Success());

        fixture.ActivityFactory.Override(ProcessJobStage.Preprocess, mockPreprocessActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — Output dict keys from OCR were forwarded to Preprocess stage's metadata
        await Assert.That(capturedPreprocessContext).IsNotNull();
        await Assert.That(capturedPreprocessContext!.Metadata.ContainsKey(StageMetadataKeys.OcrBlobPath)).IsTrue();
        await Assert.That(capturedPreprocessContext.Metadata["customOutputKey"]).IsEqualTo("outputValue");
    }

    [Test]
    public async Task StageMetadata_ForwardedToNextStage()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Override OCR stage to return data ONLY in Metadata (not Output)
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Success(
                output: null,
                metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.OcrBlobPath] = "ocr-results/test/ocr-result.json",
                    ["metadataOnlyKey"] = "metadataValue"
                }));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        // Capture what Preprocess stage receives
        StageContext? capturedContext = null;
        var mockPreprocessActivity = new Mock<IJobStageActivity>();
        mockPreprocessActivity.Setup(x => x.StageName).Returns("Preprocess");
        mockPreprocessActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Preprocess);
        mockPreprocessActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .Callback<StageContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(StageResult.Success());

        fixture.ActivityFactory.Override(ProcessJobStage.Preprocess, mockPreprocessActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — Metadata dict keys from OCR were forwarded to Preprocess stage
        await Assert.That(capturedContext).IsNotNull();
        await Assert.That(capturedContext!.Metadata.ContainsKey(StageMetadataKeys.OcrBlobPath)).IsTrue();
        await Assert.That(capturedContext.Metadata["metadataOnlyKey"]).IsEqualTo("metadataValue");
    }

    [Test]
    public async Task OverlappingKeys_MetadataWinsOverOutput()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        const string sharedKey = "sharedKey";

        // Override OCR stage to return same key in BOTH Output and Metadata
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Success(
                output: new Dictionary<string, object>
                {
                    [sharedKey] = "fromOutput"
                },
                metadata: new Dictionary<string, object>
                {
                    [sharedKey] = "fromMetadata"
                }));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        // Capture what Preprocess stage receives
        StageContext? capturedContext = null;
        var mockPreprocessActivity = new Mock<IJobStageActivity>();
        mockPreprocessActivity.Setup(x => x.StageName).Returns("Preprocess");
        mockPreprocessActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Preprocess);
        mockPreprocessActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .Callback<StageContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(StageResult.Success());

        fixture.ActivityFactory.Override(ProcessJobStage.Preprocess, mockPreprocessActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        await simulator.RunAsync(uploadResult.JobId);

        // Assert — Metadata value wins because it's merged after Output
        await Assert.That(capturedContext).IsNotNull();
        await Assert.That(capturedContext!.Metadata[sharedKey]).IsEqualTo("fromMetadata");
    }

    [Test]
    public async Task NullOutput_MetadataStillForwarded()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Override OCR stage: null Output, non-null Metadata
        var mockOcrActivity = new Mock<IJobStageActivity>();
        mockOcrActivity.Setup(x => x.StageName).Returns("OCR");
        mockOcrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        mockOcrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Success(
                output: null,
                metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.OcrBlobPath] = "ocr-results/test/ocr-result.json",
                    ["nullOutputTest"] = 42
                }));

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, mockOcrActivity.Object);

        // Capture what Preprocess stage receives
        StageContext? capturedContext = null;
        var mockPreprocessActivity = new Mock<IJobStageActivity>();
        mockPreprocessActivity.Setup(x => x.StageName).Returns("Preprocess");
        mockPreprocessActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Preprocess);
        mockPreprocessActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .Callback<StageContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(StageResult.Success());

        fixture.ActivityFactory.Override(ProcessJobStage.Preprocess, mockPreprocessActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert — no crash from null Output, Metadata still forwarded
        // Pipeline may fail at later stages (Chunk needs preprocessBlobPath) — that's fine;
        // the point is null Output doesn't cause a crash and Metadata is forwarded.
        await Assert.That(capturedContext).IsNotNull();
        await Assert.That(capturedContext!.Metadata.ContainsKey(StageMetadataKeys.OcrBlobPath)).IsTrue();
        await Assert.That(capturedContext.Metadata["nullOutputTest"]).IsEqualTo(42);
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
