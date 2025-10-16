using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Pipeline;

public sealed class OcrStageActivity : IJobStageActivity
{
    public string StageName => "OCR";
    public ProcessJobStage Stage => ProcessJobStage.OCR;

    public Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        // Call Azure Form Recognizer
        // Store extracted text/structure in Blob or context
        // Update job progress
        
        return Task.FromResult(StageResult.Success());
    }
}
