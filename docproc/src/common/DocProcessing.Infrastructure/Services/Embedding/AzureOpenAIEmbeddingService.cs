using System.ClientModel;
using System.Diagnostics;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Pipeline.Options;
using DocProcessing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace DocProcessing.Infrastructure.Services.Embedding;

/// <summary>
/// OpenAI embedding service. Works with both Azure OpenAI and plain OpenAI —
/// the <see cref="EmbeddingClient"/> is created in DI based on configuration.
/// </summary>
public sealed partial class OpenAIEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAIEmbeddingService> _logger;

    public OpenAIEmbeddingService(
        EmbeddingClient embeddingClient,
        IOptions<EmbeddingOptions> options,
        ILogger<OpenAIEmbeddingService> logger)
    {
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _embeddingClient = embeddingClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var stopwatch = Stopwatch.StartNew();
        LogEmbeddingGenerationStarted(texts.Count, _options.DeploymentName, _options.Dimensions);

        try
        {
            EmbeddingGenerationOptions generationOptions = new()
            {
                Dimensions = _options.Dimensions
            };

            ClientResult<OpenAIEmbeddingCollection> response = await _embeddingClient.GenerateEmbeddingsAsync(
                texts,
                generationOptions,
                cancellationToken);

            OpenAIEmbeddingCollection embeddings = response.Value;

            float[][] results = new float[embeddings.Count][];
            int index = 0;
            foreach (OpenAIEmbedding embedding in embeddings)
            {
                results[index++] = embedding.ToFloats().ToArray();
            }

            stopwatch.Stop();
            LogEmbeddingGenerationCompleted(texts.Count, stopwatch.ElapsedMilliseconds);

            return results;
        }
        catch (ClientResultException ex)
        {
            LogEmbeddingGenerationFailed(ex, texts.Count);
            throw new EmbeddingFailedException(
                texts.Count,
                $"OpenAI embedding API error (HTTP {ex.Status}): {ex.Message}",
                ex);
        }
        catch (OperationCanceledException)
        {
            LogEmbeddingGenerationCancelled(texts.Count);
            throw;
        }
        catch (Exception ex)
        {
            LogEmbeddingGenerationFailed(ex, texts.Count);
            throw new EmbeddingFailedException(
                texts.Count,
                $"Unexpected error during embedding generation: {ex.Message}",
                ex);
        }
    }

    #region Logging

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "Starting embedding generation. TextCount={TextCount}, Model={ModelName}, Dimensions={Dimensions}")]
    private partial void LogEmbeddingGenerationStarted(int textCount, string modelName, int dimensions);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information,
        Message = "Embedding generation completed. TextCount={TextCount}, DurationMs={DurationMs}")]
    private partial void LogEmbeddingGenerationCompleted(int textCount, long durationMs);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error,
        Message = "Embedding generation failed. TextCount={TextCount}")]
    private partial void LogEmbeddingGenerationFailed(Exception exception, int textCount);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning,
        Message = "Embedding generation cancelled. TextCount={TextCount}")]
    private partial void LogEmbeddingGenerationCancelled(int textCount);

    #endregion
}
