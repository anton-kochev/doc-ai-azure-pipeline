using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline;
using DocProcessing.Application.Services;
using DocProcessing.Application.Services.OCR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Register application services
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IProcessJobService, ProcessJobService>();

        // Register OCR service
        services.AddScoped<IOcrService, MockOcrService>();

        // Register pipeline stage activities
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
