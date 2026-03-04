using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;

namespace DocProcessing.EndToEnd.Tests.Helpers;

/// <summary>
/// Wraps IPipelineActivityFactory, delegates to real factory by default.
/// Accepts per-stage overrides for injecting custom behavior in individual tests.
/// </summary>
public sealed class ControllableActivityFactory : IPipelineActivityFactory
{
    private readonly IPipelineActivityFactory _inner;
    private readonly Dictionary<ProcessJobStage, IJobStageActivity> _overrides = new();

    public ControllableActivityFactory(IPipelineActivityFactory inner)
    {
        _inner = inner;
    }

    public void Override(ProcessJobStage stage, IJobStageActivity activity)
    {
        _overrides[stage] = activity;
    }

    public IJobStageActivity Create(ProcessJobStage stage)
    {
        return _overrides.TryGetValue(stage, out var activity)
            ? activity
            : _inner.Create(stage);
    }
}
