using System.Text.Json;
using System.Text.Json.Serialization;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Services;
using DocProcessing.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Configure Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configure JSON serialization with the camelCase naming policy
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // Add infrastructure services (includes storage and messaging)
        services.AddInfrastructure(context.Configuration);

        // Register application services
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IProcessJobService, ProcessJobService>();
    })
    .Build();

// Apply database migrations at startup (if enabled)
bool applyMigrationsOnStartup = host.Services
    .GetRequiredService<IConfiguration>()
    .GetValue("Database:ApplyMigrationsOnStartup", false);

if (applyMigrationsOnStartup)
{
    await host.InitialiseDatabaseAsync();
}

host.Run();
