using DocProcessing.Application;
using DocProcessing.Infrastructure;
using DocProcessing.Orchestrator;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .AddJsonFile(
                path: "local.settings.json",
                optional: true,
                reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Configure Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register application services
        services.RegisterApplication(context.Configuration);

        // Register infrastructure services
        services.RegisterInfrastructure(context.Configuration);

        // Register orchestrator dependencies
        services.RegisterOrchestrator();
    })
    .Build();

host.Run();
