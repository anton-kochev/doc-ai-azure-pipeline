namespace Infrastructure.Tests.Validation;

using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Validation;

/// <summary>
/// Tests for ProcessJobStatusTransitions validator.
/// </summary>
public class ProcessJobStatusTransitionsTests
{
    #region IsValidTransition Tests - Existing Transitions

    [Test]
    public async Task IsValidTransition_FromPendingToProcessing_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Processing);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromProcessingToCompleted_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.Completed);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromProcessingToFailed_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.Failed);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromFailedToPending_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Failed,
            ProcessJobStatus.Pending);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region IsValidTransition Tests - ManualReview Transitions

    [Test]
    public async Task IsValidTransition_FromProcessingToManualReview_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.ManualReview);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromManualReviewToProcessing_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Processing);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromManualReviewToCompleted_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Completed);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValidTransition_FromManualReviewToFailed_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Failed);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region IsValidTransition Tests - Invalid Transitions

    [Test]
    public async Task IsValidTransition_FromPendingToCompleted_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Completed);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsValidTransition_FromPendingToFailed_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Failed);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsValidTransition_FromPendingToManualReview_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.ManualReview);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsValidTransition_FromCompletedToAnyStatus_ReturnsFalse()
    {
        // Arrange - Completed is a terminal state
        ProcessJobStatus[] targetStatuses =
        [
            ProcessJobStatus.Pending,
            ProcessJobStatus.Processing,
            ProcessJobStatus.Failed,
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Completed
        ];

        // Act & Assert
        foreach (ProcessJobStatus targetStatus in targetStatuses)
        {
            bool result = ProcessJobStatusTransitions.IsValidTransition(
                ProcessJobStatus.Completed,
                targetStatus);
            await Assert.That(result).IsFalse();
        }
    }

    [Test]
    public async Task IsValidTransition_FromManualReviewToPending_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Pending);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsValidTransition_SameStatus_ReturnsFalse()
    {
        // Arrange
        ProcessJobStatus[] allStatuses =
        [
            ProcessJobStatus.Pending,
            ProcessJobStatus.Processing,
            ProcessJobStatus.Completed,
            ProcessJobStatus.Failed,
            ProcessJobStatus.ManualReview
        ];

        // Act & Assert - transitioning to the same status should always be invalid
        foreach (ProcessJobStatus status in allStatuses)
        {
            bool result = ProcessJobStatusTransitions.IsValidTransition(status, status);
            await Assert.That(result).IsFalse();
        }
    }

    #endregion

    #region GetValidTransitions Tests

    [Test]
    public async Task GetValidTransitions_FromPending_ReturnsProcessing()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Pending);

        // Assert
        await Assert.That(validTransitions).HasSingleItem();
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Processing);
    }

    [Test]
    public async Task GetValidTransitions_FromProcessing_ReturnsCompletedFailedAndManualReview()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Processing);

        // Assert
        await Assert.That(validTransitions.Count).IsEqualTo(3);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Completed);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Failed);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.ManualReview);
    }

    [Test]
    public async Task GetValidTransitions_FromFailed_ReturnsPending()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Failed);

        // Assert
        await Assert.That(validTransitions).HasSingleItem();
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Pending);
    }

    [Test]
    public async Task GetValidTransitions_FromManualReview_ReturnsProcessingCompletedAndFailed()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.ManualReview);

        // Assert
        await Assert.That(validTransitions.Count).IsEqualTo(3);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Processing);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Completed);
        await Assert.That(validTransitions).Contains(ProcessJobStatus.Failed);
    }

    [Test]
    public async Task GetValidTransitions_FromCompleted_ReturnsEmpty()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Completed);

        // Assert
        await Assert.That(validTransitions).IsEmpty();
    }

    #endregion
}
