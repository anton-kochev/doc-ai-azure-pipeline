using Api.Configuration;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Api.Functions;

/// <summary>
/// Azure Functions for upload-related operations.
/// </summary>
public class UploadFunctions
{
    private readonly ILogger<UploadFunctions> _logger;
    private readonly IBlobStorageService _blobStorageService;
    private readonly FileUploadOptions _fileUploadOptions;

    public UploadFunctions(
        ILogger<UploadFunctions> logger,
        IBlobStorageService blobStorageService,
        IOptions<FileUploadOptions> fileUploadOptions)
    {
        _logger = logger;
        _blobStorageService = blobStorageService;
        _fileUploadOptions = fileUploadOptions.Value;
    }

    /// <summary>
    /// Generates a SAS URL for file upload.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <returns>HTTP response with SAS URL or error.</returns>
    [Function("GenerateSasUrl")]
    public async Task<HttpResponseData> GenerateSasUrl(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/upload/sas")] HttpRequestData req)
    {
        _logger.LogInformation("Processing SAS URL generation request");

        try
        {
            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("Request body is empty");
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "Request body is required"
                }));
                return badRequestResponse;
            }

            SasUrlRequest? data = JsonSerializer.Deserialize<SasUrlRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null || string.IsNullOrEmpty(data.FileName))
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid request",
                    message = "fileName is required"
                }));
                return badRequestResponse;
            }

            // Map file extension to MIME type
            string fileExtension = Path.GetExtension(data.FileName).ToLowerInvariant();
            string? detectedMimeType = GetMimeTypeFromExtension(fileExtension);

            // Use provided contentType or detected MIME type
            string? mimeType = !string.IsNullOrEmpty(data.ContentType) ? data.ContentType : detectedMimeType;

            string[] allowedTypes = _fileUploadOptions.AllowedFileTypes
                .Select(type => type.ToLowerInvariant())
                .ToArray();

            if (string.IsNullOrEmpty(mimeType) || !allowedTypes.Contains(mimeType.ToLowerInvariant()))
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "Invalid file type",
                    message = $"File type '{mimeType ?? fileExtension}' is not allowed. Allowed types: {string.Join(", ", allowedTypes)}"
                }));
                return badRequestResponse;
            }

            // Validate file size
            long maxFileSizeBytes = _fileUploadOptions.MaxFileSizeMB * 1024 * 1024;
            if (data.FileSizeBytes > maxFileSizeBytes)
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new
                {
                    error = "File too large",
                    message = $"File size {data.FileSizeBytes / 1024.0 / 1024.0:F2} MB exceeds the maximum allowed size of {_fileUploadOptions.MaxFileSizeMB} MB"
                }));
                return badRequestResponse;
            }

            if (data.FileSizeBytes <= 0)
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

            SasUrlResult result = await _blobStorageService.GenerateSasUrlAsync(data.FileName, mimeType);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in request body");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Invalid request",
                message = "Request body must be valid JSON"
            }));
            return errorResponse;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration error while generating SAS URL");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
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
            _logger.LogError(ex, "Unexpected error while generating SAS URL");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Internal server error",
                message = "An unexpected error occurred"
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Maps file extension to MIME type.
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".pdf").</param>
    /// <returns>The corresponding MIME type, or null if not recognized.</returns>
    private static string? GetMimeTypeFromExtension(string extension)
    {
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };
    }
}

/// <summary>
/// Request model for SAS URL generation.
/// </summary>
public record SasUrlRequest(
    string FileName,
    long FileSizeBytes,
    string? ContentType
);
