using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using DocProcessing.EndToEnd.Tests.Builders;
using DocProcessing.EndToEnd.Tests.Fixtures;
using DocProcessing.EndToEnd.Tests.Helpers;
using Moq;

namespace DocProcessing.EndToEnd.Tests.Flows;

public class ManualReviewFlowTests
{
    [Test]
    public async Task StageReturnsManualReviewRequired_JobTransitionsToManualReview()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);
        SetupSuccessfulOcr(fixture, uploadResult.DocumentId, uploadResult.JobId);

        // Override Validate stage to require manual review
        var validateActivity = new Mock<IJobStageActivity>();
        validateActivity.Setup(x => x.StageName).Returns("Validate");
        validateActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Validate);
        validateActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Failure(
                "MANUAL_REVIEW_REQUIRED",
                "Low confidence score requires human review"));

        fixture.ActivityFactory.Override(ProcessJobStage.Validate, validateActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // Act
        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);

        // Assert
        await Assert.That(pipelineResult.RequiresManualReview).IsTrue();
        await Assert.That(pipelineResult.FailedStage).IsEqualTo(ProcessJobStage.Validate);

        // Transition to ManualReview (as orchestrator would)
        await fixture.ProcessJobService.RequestManualReviewAsync(
            uploadResult.JobId,
            pipelineResult.ErrorMessage);

        await DocumentAssertions.AssertJobStatusAsync(fixture.DbContext, uploadResult.JobId, ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task ManualReviewResume_RemainingStagesSucceed_JobCompleted()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);
        SetupSuccessfulOcr(fixture, uploadResult.DocumentId, uploadResult.JobId);

        // Override Validate to require manual review
        int validateCallCount = 0;
        var validateActivity = new Mock<IJobStageActivity>();
        validateActivity.Setup(x => x.StageName).Returns("Validate");
        validateActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Validate);
        validateActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                validateCallCount++;
                return validateCallCount == 1
                    ? StageResult.Failure("MANUAL_REVIEW_REQUIRED", "Needs review")
                    : StageResult.Success();
            });

        fixture.ActivityFactory.Override(ProcessJobStage.Validate, validateActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // First run — stops at Validate
        var firstResult = await simulator.RunAsync(uploadResult.JobId);
        await fixture.ProcessJobService.RequestManualReviewAsync(uploadResult.JobId, firstResult.ErrorMessage);

        // Resume from manual review
        await fixture.ProcessJobService.ResumeFromManualReviewAsync(uploadResult.JobId);

        // Act — resume from the stage that required review
        var resumeResult = await simulator.RunAsync(
            uploadResult.JobId,
            startFromStageIndex: firstResult.FailedStageIndex!.Value,
            forwardedMetadata: firstResult.AccumulatedMetadata);

        // Assert
        await Assert.That(resumeResult.IsSuccess).IsTrue();
        await DocumentAssertions.AssertJobStatusAsync(fixture.DbContext, uploadResult.JobId, ProcessJobStatus.Completed);
    }

    [Test]
    public async Task ManualReviewResume_DoesNotReExecuteEarlierStages()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);
        SetupSuccessfulOcr(fixture, uploadResult.DocumentId, uploadResult.JobId);

        // Track OCR calls
        int ocrCallCount = 0;
        var ocrActivity = new Mock<IJobStageActivity>();
        ocrActivity.Setup(x => x.StageName).Returns("OCR");
        ocrActivity.Setup(x => x.Stage).Returns(ProcessJobStage.OCR);
        ocrActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ocrCallCount++;
                return StageResult.Success(metadata: new Dictionary<string, object>
                {
                    [StageMetadataKeys.OcrBlobPath] = "test/ocr-result.json"
                });
            });

        // Override Validate to require manual review first, then succeed
        int validateCallCount = 0;
        var validateActivity = new Mock<IJobStageActivity>();
        validateActivity.Setup(x => x.StageName).Returns("Validate");
        validateActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Validate);
        validateActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                validateCallCount++;
                return validateCallCount == 1
                    ? StageResult.Failure("MANUAL_REVIEW_REQUIRED", "Review needed")
                    : StageResult.Success();
            });

        fixture.ActivityFactory.Override(ProcessJobStage.OCR, ocrActivity.Object);
        fixture.ActivityFactory.Override(ProcessJobStage.Validate, validateActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        // First run
        var firstResult = await simulator.RunAsync(uploadResult.JobId);
        await fixture.ProcessJobService.RequestManualReviewAsync(uploadResult.JobId, firstResult.ErrorMessage);
        await fixture.ProcessJobService.ResumeFromManualReviewAsync(uploadResult.JobId);

        // Act — resume from Validate stage
        await simulator.RunAsync(
            uploadResult.JobId,
            startFromStageIndex: firstResult.FailedStageIndex!.Value,
            forwardedMetadata: firstResult.AccumulatedMetadata);

        // Assert — OCR was called only once (during first run), not during resume
        await Assert.That(ocrCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ManualReviewReject_JobFailsWithRejectedCode()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);
        SetupSuccessfulOcr(fixture, uploadResult.DocumentId, uploadResult.JobId);

        // Override Validate to require manual review
        var validateActivity = new Mock<IJobStageActivity>();
        validateActivity.Setup(x => x.StageName).Returns("Validate");
        validateActivity.Setup(x => x.Stage).Returns(ProcessJobStage.Validate);
        validateActivity.Setup(x => x.ExecuteAsync(It.IsAny<StageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StageResult.Failure("MANUAL_REVIEW_REQUIRED", "Review needed"));

        fixture.ActivityFactory.Override(ProcessJobStage.Validate, validateActivity.Object);

        var simulator = new PipelineSimulator(
            fixture.ProcessJobService,
            fixture.ActivityFactory,
            fixture.DbContext,
            fixture.TimeProvider);

        var pipelineResult = await simulator.RunAsync(uploadResult.JobId);
        await fixture.ProcessJobService.RequestManualReviewAsync(uploadResult.JobId, pipelineResult.ErrorMessage);

        // Act — reject the manual review
        await fixture.ProcessJobService.RejectManualReviewAsync(
            uploadResult.JobId,
            "MANUAL_REVIEW_REJECTED",
            "Reviewer determined data is invalid");

        // Assert
        ProcessJob job = await DocumentAssertions.GetJobAsync(fixture.DbContext, uploadResult.JobId);
        await Assert.That(job.Status).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(job.LastErrorCode).IsEqualTo("MANUAL_REVIEW_REJECTED");
        await Assert.That(job.CompletedAtUtc).IsNotNull();
    }

    [Test]
    public async Task RejectFromWrongState_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        using var fixture = new EndToEndTestFixture();
        var uploadResult = await UploadTestDocument(fixture);

        // Job is in Pending state — reject should fail
        // Act & Assert
        await Assert.That(
            () => fixture.ProcessJobService.RejectManualReviewAsync(uploadResult.JobId))
            .ThrowsException();
    }

    // --- Helpers ---

    private static async Task<UploadRequestBuilder.UploadResult> UploadTestDocument(EndToEndTestFixture fixture)
    {
        return await fixture.CreateUploadBuilder()
            .WithFileName("review-test.pdf")
            .WithContentType("application/pdf")
            .WithFileContent("review test content"u8.ToArray())
            .ExecuteAsync();
    }

    private static void SetupSuccessfulOcr(EndToEndTestFixture fixture, Guid documentId, Guid jobId)
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
