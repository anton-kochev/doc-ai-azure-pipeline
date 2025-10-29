using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Orchestrator.Functions;

/// <summary>
/// Activity to retrieve a Document from the database.
/// </summary>
public sealed class GetDocument
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<GetDocument> _logger;

    public GetDocument(
        IDocumentService documentService,
        ILogger<GetDocument> logger)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(GetDocument))]
    public async Task<Document?> RunAsync(
        [ActivityTrigger] Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
        }

        return document;
    }
}
