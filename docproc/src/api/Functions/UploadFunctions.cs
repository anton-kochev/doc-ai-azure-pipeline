using api.Data;
using api.Data.Entities;
using Api.Configuration;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Api.Functions;

/// <summary>
/// Azure Functions for upload-related operations.
/// </summary>
public sealed class UploadFunctions
{
    private readonly ILogger<UploadFunctions> _logger;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDocumentService _documentService;
    private readonly FileUploadOptions _fileUploadOptions;

    public UploadFunctions(
        ILogger<UploadFunctions> logger,
        IBlobStorageService blobStorageService,
        IDocumentService documentService,
        IOptions<FileUploadOptions> fileUploadOptions)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
        _documentService = documentService;
        _fileUploadOptions = fileUploadOptions.Value;
    }

    /// <summary>
    /// Uploads a file directly to Azure Blob Storage through the Function.
    /// </summary>
    /// <param name="req">The HTTP request containing the file.</param>
    /// <returns>HTTP response with upload result or error.</returns>
    [Function("UploadFile")]
    public async Task<HttpResponseData> UploadFile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/upload")] HttpRequestData req)
    {
        _logger.LogInformation("Processing file upload request");

        try
        {
            // Parse multipart form data
            string? contentType = req.Headers.GetValues("Content-Type")?.FirstOrDefault();
            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "Content-Type must be multipart/form-data"
                }));
                return badRequestResponse;
            }

            // Extract boundary from Content-Type header
            string? boundary = contentType.Split(';')
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("boundary=".Length)
                .Trim('"');

            if (string.IsNullOrEmpty(boundary))
            {
                _logger.LogWarning("No boundary found in Content-Type");
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
                _logger.LogWarning("No file found in request");
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
                sha256Hash = sha256.ComputeHash(fileData.Data);
                fileData.Data.Position = 0; // Reset stream position for upload
            }

            // Upload to Blob Storage using Managed Identity
            BlobUploadResult result = await _blobStorageService.UploadBlobAsync(
                fileData.FileName,
                fileData.Data,
                fileData.ContentType);

            _logger.LogInformation("File uploaded successfully: {FileName}, Size: {Size} bytes", result.FileName, result.FileSizeBytes);

            // Get existing or create new Document record in database
            (Guid documentId, bool isNew) = await _documentService.GetOrCreateDocumentAsync(
                fileName: result.FileName,
                contentType: result.ContentType ?? "application/octet-stream",
                sizeBytes: result.FileSizeBytes,
                blobContainer: result.ContainerName,
                blobPath: result.BlobPath,
                blobETag: result.ETag,
                sha256Hash: sha256Hash,
                uploadedBy: "system" // TODO: Replace with actual user identity
            );

            if (isNew)
            {
                _logger.LogInformation("Document record created: {DocumentId}", documentId);
            }
            else
            {
                _logger.LogInformation("Document already exists, returning existing record: {DocumentId}", documentId);
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                documentId,
                isNew,
                blobUrl = result.BlobUrl,
                fileName = result.FileName,
                contentType = result.ContentType,
                fileSizeBytes = result.FileSizeBytes
            });
            return response;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration error while uploading file");
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Internal server error",
                message = ex.Message
            }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while uploading file");
            HttpResponseData errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Internal server error",
                message = "An unexpected error occurred while uploading the file"
            }));
            return errorResponse;
        }
    }
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

        foreach (string part in parts)
        {
            if (part.Trim() == "--" || string.IsNullOrWhiteSpace(part))
                continue;

            // Parse headers and content
            string[] sections = part.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
            if (sections.Length < 2)
                continue;

            string headers = sections[0];
            string fileContent = sections[1].TrimEnd('\r', '\n', '-');

            // Check if this is a file field
            if (!headers.Contains("filename=", StringComparison.OrdinalIgnoreCase))
                continue;

            // Extract filename
            string? filename = ExtractValue(headers, "filename=");
            if (string.IsNullOrEmpty(filename))
                continue;

            // Extract content type
            string? contentTypeLine = headers.Split("\r\n")
                .FirstOrDefault(h => h.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
            string? contentType = contentTypeLine?.Substring("Content-Type:".Length).Trim();

            // Convert content to stream
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
            MemoryStream memoryStream = new(fileBytes);

            return new FileData(filename, memoryStream, contentType);
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
internal record FileData(string FileName, Stream Data, string? ContentType);
