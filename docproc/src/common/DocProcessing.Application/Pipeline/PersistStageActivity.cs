using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public sealed class PersistStageActivity : IJobStageActivity
{
    public string StageName => "Persist";
    public ProcessJobStage Stage => ProcessJobStage.Persist;

    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        // Implement persistence logic here
        
        return Task.FromResult(StageResult.Success());
    }
}
