using DocProcessing.Domain.Entities;

namespace DocProcessing.Application.Interfaces;

public interface IPipelineActivityFactory
{
    IJobStageActivity Create(ProcessJobStage stage);
}
