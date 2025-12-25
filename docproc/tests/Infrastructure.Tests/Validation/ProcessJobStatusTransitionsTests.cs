namespace Infrastructure.Tests.Validation;

using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Validation;

/// <summary>
/// Tests for ProcessJobStatusTransitions validator.
/// </summary>
public class ProcessJobStatusTransitionsTests
{
    #region IsValidTransition Tests - Existing Transitions

    [Fact]
    public void IsValidTransition_FromPendingToProcessing_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Processing);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromProcessingToCompleted_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.Completed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromProcessingToFailed_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.Failed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromFailedToPending_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Failed,
            ProcessJobStatus.Pending);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region IsValidTransition Tests - ManualReview Transitions

    [Fact]
    public void IsValidTransition_FromProcessingToManualReview_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Processing,
            ProcessJobStatus.ManualReview);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromManualReviewToProcessing_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Processing);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromManualReviewToCompleted_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Completed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidTransition_FromManualReviewToFailed_ReturnsTrue()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Failed);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region IsValidTransition Tests - Invalid Transitions

    [Fact]
    public void IsValidTransition_FromPendingToCompleted_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Completed);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidTransition_FromPendingToFailed_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.Failed);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidTransition_FromPendingToManualReview_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.Pending,
            ProcessJobStatus.ManualReview);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidTransition_FromCompletedToAnyStatus_ReturnsFalse()
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
            Assert.False(result, $"Transition from Completed to {targetStatus} should be invalid");
        }
    }

    [Fact]
    public void IsValidTransition_FromManualReviewToPending_ReturnsFalse()
    {
        // Act
        bool result = ProcessJobStatusTransitions.IsValidTransition(
            ProcessJobStatus.ManualReview,
            ProcessJobStatus.Pending);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidTransition_SameStatus_ReturnsFalse()
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
            Assert.False(result, $"Transition from {status} to {status} should be invalid");
        }
    }

    #endregion

    #region GetValidTransitions Tests

    [Fact]
    public void GetValidTransitions_FromPending_ReturnsProcessing()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Pending);

        // Assert
        Assert.Single(validTransitions);
        Assert.Contains(ProcessJobStatus.Processing, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromProcessing_ReturnsCompletedFailedAndManualReview()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Processing);

        // Assert
        Assert.Equal(3, validTransitions.Count);
        Assert.Contains(ProcessJobStatus.Completed, validTransitions);
        Assert.Contains(ProcessJobStatus.Failed, validTransitions);
        Assert.Contains(ProcessJobStatus.ManualReview, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromFailed_ReturnsPending()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Failed);

        // Assert
        Assert.Single(validTransitions);
        Assert.Contains(ProcessJobStatus.Pending, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromManualReview_ReturnsProcessingCompletedAndFailed()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.ManualReview);

        // Assert
        Assert.Equal(3, validTransitions.Count);
        Assert.Contains(ProcessJobStatus.Processing, validTransitions);
        Assert.Contains(ProcessJobStatus.Completed, validTransitions);
        Assert.Contains(ProcessJobStatus.Failed, validTransitions);
    }

    [Fact]
    public void GetValidTransitions_FromCompleted_ReturnsEmpty()
    {
        // Act
        IReadOnlyCollection<ProcessJobStatus> validTransitions =
            ProcessJobStatusTransitions.GetValidTransitions(ProcessJobStatus.Completed);

        // Assert
        Assert.Empty(validTransitions);
    }

    #endregion
}
