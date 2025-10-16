using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public sealed class PreprocessStageActivity : IJobStageActivity
{
    public string StageName => "Preprocess";
    public ProcessJobStage Stage => ProcessJobStage.Preprocess;
    
    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StageResult.Success());
    }
}
