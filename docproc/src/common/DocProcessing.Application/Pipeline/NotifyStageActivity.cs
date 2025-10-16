using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public sealed class NotifyStageActivity : IJobStageActivity
{
    public string StageName => "Notify";
    public ProcessJobStage Stage => ProcessJobStage.Notify;

    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        // Implement notification logic here (e.g., send email or message)
        
        return Task.FromResult(StageResult.Success());
    }
}
