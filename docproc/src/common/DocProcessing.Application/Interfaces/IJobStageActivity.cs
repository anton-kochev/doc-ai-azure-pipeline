using DocProcessing.Application.Models;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Represents a discrete stage in the document processing pipeline.
/// Each stage receives input, performs work, and produces output.
/// </summary>
public interface IJobStageActivity
{
    /// <summary>
    /// Gets the name of the stage (e.g., "OCR", "Preprocessing", "Embedding").
    /// </summary>
    string StageName { get; }

    /// <summary>
    /// Gets the stage type that this activity handles.
    /// </summary>
    ProcessJobStage Stage { get; }

    /// <summary>
    /// Executes the stage activity asynchronously.
    /// </summary>
    /// <param name="context">The execution context containing the job and related data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the stage execution.</returns>
    Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the execution context for a pipeline stage.
/// </summary>
public sealed class StageContext
{
    public StageContext(ProcessJobModel job, Dictionary<string, object> metadata, string correlationId)
    {
        Job = job;
        Metadata = metadata;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Gets or sets the process job being executed.
    /// </summary>
    public ProcessJobModel Job { get; init; }

    /// <summary>
    /// Gets or sets additional metadata for the stage execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; init; }
}

/// <summary>
/// Represents the result of a pipeline stage execution.
/// </summary>
public sealed class StageResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the stage executed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets or sets the output data produced by the stage.
    /// </summary>
    public object? Output { get; init; }

    /// <summary>
    /// Gets or sets the error code if the stage failed.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets or sets the error message if the stage failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets additional metadata about the execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a successful stage result.
    /// </summary>
    /// <param name="output">The output data produced by the stage.</param>
    /// <param name="metadata">Optional metadata about the execution.</param>
    /// <returns>A successful stage result.</returns>
    public static StageResult Success(object? output = null, Dictionary<string, object>? metadata = null) =>
        new() { IsSuccess = true, Output = output, Metadata = metadata ?? new() };

    /// <summary>
    /// Creates a failed stage result.
    /// </summary>
    /// <param name="errorCode">The error code describing the failure.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="metadata">Optional metadata about the execution.</param>
    /// <returns>A failed stage result.</returns>
    public static StageResult Failure(string errorCode, string errorMessage, Dictionary<string, object>? metadata = null) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage, Metadata = metadata ?? new() };
}
