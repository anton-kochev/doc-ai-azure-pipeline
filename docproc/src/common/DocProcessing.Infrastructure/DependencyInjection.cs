using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocProcessing.Application.Configuration;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Application.Services;
using DocProcessing.Application.Services.OCR;
using DocProcessing.Infrastructure.Factories;
using DocProcessing.Infrastructure.MessageBroker;
using DocProcessing.Infrastructure.MessageBroker.ServiceBus;
using DocProcessing.Infrastructure.Options;
using DocProcessing.Infrastructure.Services.Embedding;
using DocProcessing.Infrastructure.Services.OCR;
using DocProcessing.Infrastructure.Services.VectorStore;
using DocProcessing.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenAI;
using OpenAI.Embeddings;
using Pgvector.Npgsql;

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
        services.Configure<VectorStoreOptions>(configuration.GetSection(VectorStoreOptions.SectionName));

        // Register services
        services.AddSingleton<IServiceBusSenderFactory, ServiceBusSenderFactory>();
        services.AddScoped<IMessagingService, ServiceBusService>();
        services.AddScoped<IStorageService, BlobStorageService>();
        services.AddScoped<IOcrService, AzureDocumentIntelligenceOcrService>();

        // Embedding client — create from Azure OpenAI or plain OpenAI based on config
        services.AddSingleton<EmbeddingClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            string model = opts.DeploymentName;

            if (string.Equals(opts.Provider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = new Uri(opts.AzureEndpoint
                    ?? throw new InvalidOperationException("Embedding:AzureEndpoint must be set when Provider is 'Azure'."));
                var client = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
                return client.GetEmbeddingClient(model);
            }

            // Plain OpenAI
            var apiKey = opts.ApiKey
                ?? throw new InvalidOperationException("Embedding:ApiKey must be set when Provider is 'OpenAI'.");
            var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
            return openAiClient.GetEmbeddingClient(model);
        });
        services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();

        // Vector store — switch by config (deployment-time, not runtime)
        string provider = configuration["VectorStore:Provider"] ?? "pgvector";
        if (string.Equals(provider, "AzureSearch", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<AzureSearchOptions>(configuration.GetSection("VectorStore:AzureSearch"));
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
                var endpoint = new Uri(opts.Endpoint
                    ?? throw new InvalidOperationException("VectorStore:AzureSearch:Endpoint must be configured."));
                var credential = new DefaultAzureCredential();
                return new SearchIndexClient(endpoint, credential);
            });
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
                var endpoint = new Uri(opts.Endpoint!);
                var credential = new DefaultAzureCredential();
                return new SearchClient(endpoint, opts.IndexName, credential);
            });
            services.AddScoped<IVectorStoreService, AzureSearchVectorStoreService>();
        }
        else
        {
            services.Configure<PgVectorOptions>(configuration.GetSection("VectorStore:PgVector"));
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<PgVectorOptions>>().Value;
                var builder = new NpgsqlDataSourceBuilder(opts.ConnectionString);
                builder.UseVector();
                return builder.Build();
            });
            services.AddScoped<IVectorStoreService, PgVectorStoreService>();
        }

        // Retrieval
        services.Configure<RetrievalOptions>(configuration.GetSection(RetrievalOptions.SectionName));
        services.AddScoped<IRetrievalService, RetrievalService>();

        // Register factories
        services.AddTransient<IPipelineActivityFactory, PipelineActivityFactory>();

        return services;
    }
}
