using System.Text.Json;
using System.Text.Json.Serialization;
using api.Data;
using Api.Configuration;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // Configure custom options
        services.Configure<AzureStorageOptions>(
            context.Configuration.GetSection(AzureStorageOptions.SectionName));

        services.Configure<FileUploadOptions>(
            context.Configuration.GetSection(FileUploadOptions.SectionName));

        // Register DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                context.Configuration.GetConnectionString("SQLAZURECONNSTR_DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        // Register services
        services.AddScoped<IBlobStorageService, BlobStorageService>();
    })
    .Build();

// Apply database migrations at startup (if enabled)
bool applyMigrationsOnStartup = host.Services
    .GetRequiredService<IConfiguration>()
    .GetValue("Database:ApplyMigrationsOnStartup", false);

if (applyMigrationsOnStartup)
{
    using IServiceScope scope = host.Services.CreateScope();
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations");
        throw;
    }
}

host.Run();
