using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Infrastructure.Factories;
using DocProcessing.Infrastructure.MessageBroker;
using DocProcessing.Infrastructure.MessageBroker.ServiceBus;
using DocProcessing.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
        
        // Register ApplicationDbContext with dependency injection
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ApplicationDbContextInitializer>();
        
        // Register TimeProvider for dependency injection
        services.AddSingleton(TimeProvider.System);
        
        // Register configuration options
        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection("Ocr"));
        
        // Register services
        services.AddSingleton<IServiceBusSenderFactory, ServiceBusSenderFactory>();
        services.AddScoped<IMessagingService, ServiceBusService>();
        services.AddScoped<IStorageService, BlobStorageService>();
        
        // Register factories
        services.AddTransient<IPipelineActivityFactory, PipelineActivityFactory>();
        
        return services;
    }
}
