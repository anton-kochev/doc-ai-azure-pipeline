using DocProcessing.Application.Pipeline;
using DocProcessing.Orchestrator.Functions;
using DocProcessing.Orchestrator.Functions.Executors;
using DocProcessing.Orchestrator.Functions.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace DocProcessing.Orchestrator;

public static class DependencyInjection
{
    public static IServiceCollection RegisterOrchestrator(this IServiceCollection services)
    {
        services.AddScoped<GetJob>();
        
        // Register orchestrator activities
        services.AddScoped<CompleteJob>();
        services.AddScoped<FailJob>();
        services.AddScoped<StartJob>();

        // Register ManualReview activities
        services.AddScoped<RequestManualReview>();
        services.AddScoped<ResumeFromManualReview>();
        services.AddScoped<RejectManualReview>();

        // Register orchestrator functions
        services.AddScoped<EmbedStageExecutor>();
        services.AddScoped<ExtractStageExecutor>();
        services.AddScoped<NotifyStageExecutor>();
        services.AddScoped<OcrStageExecutor>();
        services.AddScoped<PersistStageExecutor>();
        services.AddScoped<PreprocessStageExecutor>();
        services.AddScoped<ValidateStageExecutor>();
        
        return services;
    }
}
