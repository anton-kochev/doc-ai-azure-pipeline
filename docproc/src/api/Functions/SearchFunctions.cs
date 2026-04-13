using System.Net;
using System.Text.Json;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.Retrieval;
using DocProcessing.Domain.Entities;
using DocProcessing.Domain.Exceptions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Api.Functions;

/// <summary>
/// Azure Functions for vector search / retrieval operations.
/// </summary>
public sealed partial class SearchFunctions
{
    private readonly IRetrievalService _retrievalService;
    private readonly ILogger<SearchFunctions> _logger;

    public SearchFunctions(IRetrievalService retrievalService, ILogger<SearchFunctions> logger)
    {
        _retrievalService = retrievalService;
        _logger = logger;
    }

    /// <summary>
    /// Searches for relevant document chunks using vector similarity.
    /// </summary>
    /// <remarks>
    /// POST /api/search
    /// Body: { "queryText": "...", "documentId": "...", "topK": 10, "scoreThreshold": 0.3, "chunkTypes": ["Text", "Table"] }
    /// </remarks>
    [Function("Search")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "search")]
        HttpRequestData req, CancellationToken cancellationToken)
    {
        SearchRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SearchRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid JSON body" }, cancellationToken);
            return badResponse;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.QueryText) || body.DocumentId == Guid.Empty)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(
                new { error = "queryText and documentId are required" }, cancellationToken);
            return badResponse;
        }

        try
        {
            var query = new RetrievalQuery
            {
                QueryText = body.QueryText,
                DocumentId = body.DocumentId,
                TopK = body.TopK,
                ScoreThreshold = body.ScoreThreshold,
                ChunkTypeFilter = body.ChunkTypes?.Select(t => Enum.Parse<ChunkType>(t, ignoreCase: true)).ToList()
            };

            RetrievalResult result = await _retrievalService.RetrieveAsync(query, cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                queryText = result.QueryText,
                documentId = result.DocumentId,
                totalCandidates = result.TotalCandidates,
                totalTokens = result.TotalTokens,
                embeddingDurationMs = result.EmbeddingDuration.TotalMilliseconds,
                searchDurationMs = result.SearchDuration.TotalMilliseconds,
                chunks = result.Chunks.Select(c => new
                {
                    c.ChunkId,
                    c.ChunkIndex,
                    c.Content,
                    chunkType = c.ChunkType.ToString(),
                    c.PageNumbers,
                    c.TokenCount,
                    c.Score
                })
            }, cancellationToken);

            return response;
        }
        catch (RetrievalFailedException ex)
        {
            _logger.LogError(ex, "Retrieval failed for DocumentId={DocumentId}", body.DocumentId);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(
                new { error = "Retrieval failed", message = ex.Message }, cancellationToken);
            return errorResponse;
        }
    }

    private sealed class SearchRequest
    {
        public string QueryText { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public int? TopK { get; set; }
        public double? ScoreThreshold { get; set; }
        public string[]? ChunkTypes { get; set; }
    }
}
