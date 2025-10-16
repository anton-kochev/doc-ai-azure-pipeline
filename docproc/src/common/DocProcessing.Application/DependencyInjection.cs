using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IProcessJobService, ProcessJobService>();

        services.AddScoped<EmbedStageActivity>();
        services.AddScoped<ExtractStageActivity>();
        services.AddScoped<NotifyStageActivity>();
        services.AddScoped<OcrStageActivity>();
        services.AddScoped<PersistStageActivity>();
        services.AddScoped<PreprocessStageActivity>();
        services.AddScoped<ValidateStageActivity>();
        
        return services;
    }
}
