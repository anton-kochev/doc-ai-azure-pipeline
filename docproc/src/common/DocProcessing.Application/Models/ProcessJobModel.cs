using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Models;

public record ProcessJobModel(
    Guid JobId,
    Guid DocumentId,
    string IdempotencyKey,
    ProcessJobStatus Status,
    ProcessJobStage Stage
    // int Attempts,
    // string? LastErrorCode,
    // string? LastErrorMessage,
    // DateTime CreatedAtUtc,
    // DateTime? StartedAtUtc,
    // DateTime? CompletedAtUtc,
    // string CorrelationId,
    // string? ExtractionProfile);
);

public static class ProcessJobModelExtensions
{
    public static ProcessJobModel ToModel(this ProcessJob input)
    {
        return new ProcessJobModel(
            input.JobId,
            input.DocumentId,
            input.IdempotencyKey,
            input.Status,
            input.Stage
        );
    }
}
