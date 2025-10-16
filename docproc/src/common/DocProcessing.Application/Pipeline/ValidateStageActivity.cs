using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public class ValidateStageActivity : IJobStageActivity
{
    public string StageName => "Validate";
    public ProcessJobStage Stage => ProcessJobStage.Validate;

    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        // Implement validation logic here
        
        return Task.FromResult(StageResult.Success());
    }
}
