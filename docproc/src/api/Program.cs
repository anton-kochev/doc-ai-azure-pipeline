using Api.Configuration;
using Api.Services;
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

        // Configure custom options
        services.Configure<AzureStorageOptions>(
            context.Configuration.GetSection(AzureStorageOptions.SectionName));

        services.Configure<FileUploadOptions>(
            context.Configuration.GetSection(FileUploadOptions.SectionName));

        // Register services
        services.AddScoped<IBlobStorageService, BlobStorageService>();
    })
    .Build();

host.Run();
