using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public sealed class EmbedStageActivity : IJobStageActivity
{
    public string StageName => "Embed";
    public ProcessJobStage Stage => ProcessJobStage.Embed;
    
    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        // Call Azure OpenAI to generate embeddings
        // Store embeddings in vector database
        // Update job progress
        
        return Task.FromResult(StageResult.Success());
    }
}
