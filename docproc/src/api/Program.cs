using System.Text.Json;
using System.Text.Json.Serialization;
using DocProcessing.Api.Options;
using DocProcessing.Application;
using DocProcessing.Infrastructure;
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
                path: "appsettings.json",
                optional: false,
                reloadOnChange: true)
            .AddJsonFile(
                path: $"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                optional: true,
                reloadOnChange: true)
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

        // Configure custom options
        services.Configure<FileUploadOptions>(context.Configuration.GetSection(FileUploadOptions.SectionName));

        // Add infrastructure services (includes storage and messaging)
        services.RegisterInfrastructure(context.Configuration);

        // Register application services
        services.RegisterApplication(context.Configuration);
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
