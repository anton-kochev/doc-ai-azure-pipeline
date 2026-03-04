using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace DocProcessing.Infrastructure.Factories;

public sealed class PipelineActivityFactory : IPipelineActivityFactory
{
    private readonly IServiceProvider _serviceProvider;
    
    public PipelineActivityFactory(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }
    
    public IJobStageActivity Create(ProcessJobStage stage)
    {
        return stage switch
        {
            ProcessJobStage.OCR => _serviceProvider.GetRequiredService<OcrStageActivity>(),
            ProcessJobStage.Preprocess => _serviceProvider.GetRequiredService<PreprocessStageActivity>(),
            ProcessJobStage.Chunk => _serviceProvider.GetRequiredService<ChunkStageActivity>(),
            ProcessJobStage.Embed => _serviceProvider.GetRequiredService<EmbedStageActivity>(),
            ProcessJobStage.Extract => _serviceProvider.GetRequiredService<ExtractStageActivity>(),
            ProcessJobStage.Validate => _serviceProvider.GetRequiredService<ValidateStageActivity>(),
            ProcessJobStage.Persist => _serviceProvider.GetRequiredService<PersistStageActivity>(),
            ProcessJobStage.Notify => _serviceProvider.GetRequiredService<NotifyStageActivity>(),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported processing stage")
        };
    }
}
