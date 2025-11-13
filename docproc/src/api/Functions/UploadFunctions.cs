using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DocProcessing.Api.Options;
using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Exceptions;

namespace DocProcessing.Api.Functions;

/// <summary>
/// Azure Functions for upload-related operations.
/// </summary>
public sealed partial class UploadFunctions
{
    private readonly ILogger<UploadFunctions> _logger;
    private readonly IStorageService _storageService;
    private readonly IDocumentService _documentService;
    private readonly IProcessJobService _processJobService;
    private readonly IMessagingService _messagingService;
    private readonly FileUploadOptions _fileUploadOptions;

    public UploadFunctions(
        ILogger<UploadFunctions> logger,
        IStorageService storageService,
        IDocumentService documentService,
        IProcessJobService processJobService,
        IMessagingService messagingService,
        IOptions<FileUploadOptions> fileUploadOptions)
    {
        _logger = logger;
        _storageService = storageService;
        _documentService = documentService;
        _processJobService = processJobService;
        _messagingService = messagingService;
        _fileUploadOptions = fileUploadOptions.Value;
    }

    /// <summary>
    /// Uploads a file directly to Azure Blob Storage through the Function.
    /// Validates file type and size, computes SHA256 hash, creates database records,
    /// and enqueues processing job to Service Bus.
    /// </summary>
    /// <param name="req">The HTTP request containing the multipart/form-data file upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// HTTP 202 Accepted response with job details if successful,
    /// HTTP 400 Bad Request for validation errors,
    /// HTTP 500 Internal Server Error for configuration or unexpected errors.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Azure Storage or Service Bus configuration is invalid.
    /// </exception>
    [Function("UploadFile")]
    public async Task<HttpResponseData> UploadFile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upload")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LogProcessingUploadRequest();

        try
        {
            // Parse multipart form data
            string? contentType = req.Headers.GetValues("Content-Type")?.FirstOrDefault();
            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                LogInvalidContentType(contentType);
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "Content-Type must be multipart/form-data"
                }));

                return badRequestResponse;
            }

            // Extract boundary from the Content-Type header
            string? boundary = contentType.Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault(x =>
                    x.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?["boundary=".Length..]
                ?.Trim('"');

            if (string.IsNullOrEmpty(boundary))
            {
                LogNoBoundaryFound();
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");

                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "Missing boundary in multipart/form-data"
                }));

                return badRequestResponse;
            }

            // Parse multipart content
            MultipartFormDataParser parser = new(req.Body, boundary);
            FileData? fileData = await parser.GetFile();

            if (fileData == null)
            {
                LogNoFileFound();
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");

                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "No file found in request"
                }));

                return badRequestResponse;
            }

            // Validate file type
            string[] allowedTypes = _fileUploadOptions.AllowedFileTypes
                .Select(type => type.ToLowerInvariant())
                .ToArray();

            if (string.IsNullOrEmpty(fileData.ContentType) || !allowedTypes.Contains(fileData.ContentType.ToLowerInvariant()))
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");

                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid file type",
                    message = $"File type '{fileData.ContentType}' is not allowed. Allowed types: {string.Join(", ", allowedTypes)}"
                }));

                return badRequestResponse;
            }

            // Validate file size
            long maxFileSizeBytes = _fileUploadOptions.MaxFileSizeMB * 1024 * 1024;
            if (fileData.Data.Length > maxFileSizeBytes)
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");

                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "File too large",
                    message = $"File size {fileData.Data.Length / 1024.0 / 1024.0:F2} MB exceeds the maximum allowed size of {_fileUploadOptions.MaxFileSizeMB} MB"
                }));

                return badRequestResponse;
            }

            if (fileData.Data.Length == 0)
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");

                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid file size",
                    message = "File size must be greater than 0"
                }));

                return badRequestResponse;
            }

            // Calculate SHA256 hash
            byte[] sha256Hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                sha256Hash = await sha256.ComputeHashAsync(fileData.Data);
                fileData.Data.Position = 0; // Reset stream position for upload
            }

            // Generate correlation ID upfront for distributed tracing
            string correlationId = Guid.NewGuid().ToString();

            // Upload to Blob Storage using Managed Identity
            UploadResult result = await _storageService.UploadAsync(
                fileData.FileName,
                fileData.Data,
                fileData.ContentType,
                cancellationToken);

            LogFileUploadedSuccessfully(correlationId, result.FileName, result.FileSizeBytes);

            // Get existing or create new Document record in the database
            (Guid documentId, bool isNewDocument) = await _documentService.GetOrCreateDocumentAsync(
                fileName: result.FileName,
                contentType: result.ContentType ?? "application/octet-stream",
                sizeBytes: result.FileSizeBytes,
                blobContainer: result.ContainerName,
                blobPath: result.BlobPath,
                blobETag: result.ETag,
                sha256Hash: sha256Hash,
                uploadedBy: "system", // TODO: Replace with actual user identity
                tenantId: fileData.TenantId
            );

            if (isNewDocument)
            {
                LogDocumentCreated(correlationId, documentId);
            }
            else
            {
                LogDocumentAlreadyExists(correlationId, documentId);
            }

            // Get existing or create a new ProcessJob with idempotency check
            (Guid jobId, bool isNewJob) = await _processJobService.GetOrCreateJobAsync(
                documentId: documentId,
                tenantId: fileData.TenantId,
                sha256Hash: sha256Hash,
                extractionProfile: fileData.ExtractionProfile,
                correlationId: correlationId,
                cancellationToken: cancellationToken);

            if (isNewJob)
            {
                LogJobCreated(correlationId, jobId);

                // Enqueue Service Bus message for new jobs only
                await _messagingService.EnqueueJobAsync(
                    jobId: jobId,
                    correlationId: correlationId,
                    cancellationToken
                );

                LogJobMessageEnqueued(correlationId, jobId);
            }
            else
            {
                LogJobAlreadyExists(correlationId, jobId);
            }

            // Return 202 Accepted with job and document IDs
            HttpResponseData response = req.CreateResponse(HttpStatusCode.Accepted);

            await response.WriteAsJsonAsync(
                new
                {
                    jobId,
                    documentId,
                    isNewJob,
                    isNewDocument,
                    extractionProfile = fileData.ExtractionProfile,
                    blobUrl = result.BlobUrl,
                    fileName = result.FileName,
                    contentType = result.ContentType,
                    fileSizeBytes = result.FileSizeBytes
                },
                cancellationToken);
            
            return response;
        }
        catch (InvalidOperationException ex)
        {
            LogConfigurationError(ex);
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");

            await errorResponse.WriteStringAsync(
                JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = ex.Message
                }),
                cancellationToken);
            
            return errorResponse;
        }
        catch (Exception ex)
        {
            LogUnexpectedError(ex);
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            
            await errorResponse.WriteStringAsync(
                JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = "An unexpected error occurred while uploading the file"
                }),
                cancellationToken);
            
            return errorResponse;
        }
    }

    /// <summary>
    /// Retries a failed processing job by transitioning it back to Pending status.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <param name="jobId">The ID of the job to retry (from route).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// HTTP 200 OK if the job was successfully retried,
    /// HTTP 400 Bad Request if the job ID format is invalid,
    /// HTTP 404 Not Found if the job doesn't exist or is not in Failed status.
    /// </returns>
    [Function("RetryJob")]
    public async Task<HttpResponseData> RetryJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "jobs/{jobId}/retry")]
        HttpRequestData req, string jobId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(jobId, out Guid parsedJobId))
        {
            HttpResponseData badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "Invalid job ID format" }, cancellationToken);

            return badResponse;
        }

        try
        {
            string correlationId = await _processJobService.RetryFailedJobAsync(parsedJobId, cancellationToken);

            // Re-enqueue to Service Bus with the original correlation ID
            await _messagingService.EnqueueJobAsync(
                jobId: parsedJobId,
                correlationId: correlationId,
                cancellationToken);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(
                new { message = "Job re-queued for retry", jobId = parsedJobId, correlationId },
                cancellationToken);

            return response;
        }
        catch (JobNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job not found for retry. JobId={JobId}", ex.JobId);

            HttpResponseData notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                error = "Job not found",
                jobId = ex.JobId
            }, cancellationToken);
            return notFoundResponse;
        }
        catch (InvalidStateTransitionException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid state for retry. JobId={JobId}, CurrentStatus={CurrentStatus}",
                ex.JobId,
                ex.CurrentStatus);

            HttpResponseData unprocessableResponse = req.CreateResponse(HttpStatusCode.UnprocessableEntity);
            await unprocessableResponse.WriteAsJsonAsync(new
            {
                error = "Job is not in a retryable state",
                jobId = ex.JobId,
                currentStatus = ex.CurrentStatus.ToString()
            }, cancellationToken);
            return unprocessableResponse;
        }
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Processing file upload request")]
    private partial void LogProcessingUploadRequest();

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Invalid Content-Type: {ContentType}")]
    private partial void LogInvalidContentType(string? contentType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "No boundary found in Content-Type")]
    private partial void LogNoBoundaryFound();

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "No file found in request")]
    private partial void LogNoFileFound();

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "File uploaded successfully. CorrelationId: {CorrelationId}, FileName: {FileName}, Size: {Size} bytes")]
    private partial void LogFileUploadedSuccessfully(string correlationId, string fileName, long size);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Document record created. CorrelationId: {CorrelationId}, DocumentId: {DocumentId}")]
    private partial void LogDocumentCreated(string correlationId, Guid documentId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Document already exists, returning existing record. CorrelationId: {CorrelationId}, DocumentId: {DocumentId}")]
    private partial void LogDocumentAlreadyExists(string correlationId, Guid documentId);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Process job created. CorrelationId: {CorrelationId}, JobId: {JobId}")]
    private partial void LogJobCreated(string correlationId, Guid jobId);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Job message enqueued. CorrelationId: {CorrelationId}, JobId: {JobId}")]
    private partial void LogJobMessageEnqueued(string correlationId, Guid jobId);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Process job already exists, returning existing job. CorrelationId: {CorrelationId}, JobId: {JobId}")]
    private partial void LogJobAlreadyExists(string correlationId, Guid jobId);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Error,
        Message = "Configuration error while uploading file")]
    private partial void LogConfigurationError(Exception exception);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Unexpected error while uploading file")]
    private partial void LogUnexpectedError(Exception exception);
}

/// <summary>
/// Multipart form data parser for extracting file from HTTP request.
/// </summary>
internal sealed class MultipartFormDataParser
{
    private readonly Stream _stream;
    private readonly string _boundary;

    public MultipartFormDataParser(Stream stream, string boundary)
    {
        _stream = stream;
        _boundary = boundary;
    }

    public async Task<FileData?> GetFile()
    {
        using StreamReader reader = new(_stream);
        string content = await reader.ReadToEndAsync();

        // Split by boundary
        string[] parts = content.Split(new[] { $"--{_boundary}" }, StringSplitOptions.RemoveEmptyEntries);

        // Store form fields
        string? extractionProfile = null;
        Guid? tenantId = null;
        FileData? fileData = null;

        foreach (string part in parts)
        {
            if (part.Trim() == "--" || string.IsNullOrWhiteSpace(part))
                continue;

            // Parse headers and content
            string[] sections = part.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
            if (sections.Length < 2)
                continue;

            string headers = sections[0];
            string fieldContent = sections[1].TrimEnd('\r', '\n', '-');

            // Check if this is a file field
            if (headers.Contains("filename=", StringComparison.OrdinalIgnoreCase))
            {
                // Extract filename
                string? filename = ExtractValue(headers, "filename=");
                if (string.IsNullOrEmpty(filename))
                    continue;

                // Extract content type
                string? contentTypeLine = headers.Split("\r\n")
                    .FirstOrDefault(h => h.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
                string? contentType = contentTypeLine?.Substring("Content-Type:".Length).Trim();

                // Convert content to stream
                byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(fieldContent);
                MemoryStream memoryStream = new(fileBytes);

                fileData = new FileData(filename, memoryStream, contentType);
            }
            else
            {
                // This is a regular form field
                string? fieldName = ExtractValue(headers, "name=");

                if (fieldName?.Equals("extractionProfile", StringComparison.OrdinalIgnoreCase) == true)
                {
                    extractionProfile = fieldContent.Trim();
                }
                else if (fieldName?.Equals("tenantId", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (Guid.TryParse(fieldContent.Trim(), out Guid parsedTenantId))
                    {
                        tenantId = parsedTenantId;
                    }
                }
            }
        }

        // Return file with form fields
        if (fileData != null)
        {
            return fileData with
            {
                ExtractionProfile = extractionProfile,
                TenantId = tenantId
            };
        }

        return null;
    }

    private static string? ExtractValue(string headers, string key)
    {
        int startIndex = headers.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1)
            return null;

        startIndex += key.Length;
        int endIndex = headers.IndexOfAny(new[] { ';', '\r', '\n' }, startIndex);
        string value = endIndex == -1
            ? headers.Substring(startIndex)
            : headers.Substring(startIndex, endIndex - startIndex);

        return value.Trim('"', ' ');
    }
}

/// <summary>
/// Represents a file extracted from multipart form data.
/// </summary>
internal record FileData(
    string FileName,
    Stream Data,
    string? ContentType,
    string? ExtractionProfile = null,
    Guid? TenantId = null);
