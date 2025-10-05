using Api.Configuration;
using Api.Services;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;

namespace Api.Endpoints;

/// <summary>
/// Extension methods for mapping upload-related endpoints.
/// </summary>
public static class UploadEndpoints
{
    /// <summary>
    /// Maps upload endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapUploadEndpoints(this WebApplication app)
    {
        RouteGroupBuilder uploadGroup = app.MapGroup("/api/upload")
            .WithTags("Upload");

        uploadGroup.MapPost("/sas", GenerateSasUrlAsync)
            .WithName("GenerateSasUrl")
            .WithOpenApi();
    }

    /// <summary>
    /// Generates a SAS URL for file upload.
    /// </summary>
    /// <param name="fileName">Name of the file to upload.</param>
    /// <param name="fileSizeBytes">Size of the file in bytes.</param>
    /// <param name="contentType">Optional content type of the file.</param>
    /// <param name="blobStorageService">The blob storage service.</param>
    /// <param name="fileUploadOptions">File upload configuration options.</param>
    /// <returns>A result containing the SAS URL and metadata.</returns>
    private static async Task<IResult> GenerateSasUrlAsync(
        string fileName,
        long fileSizeBytes,
        string? contentType,
        IBlobStorageService blobStorageService,
        IOptions<FileUploadOptions> fileUploadOptions)
    {
        try
        {
            // Validate file extension
            string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
            string[] allowedTypes = fileUploadOptions.Value.AllowedFileTypes
                .Select(type => type.ToLowerInvariant())
                .ToArray();

            if (!allowedTypes.Contains(fileExtension))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid file type",
                    message = $"File type '{fileExtension}' is not allowed. Allowed types: {string.Join(", ", allowedTypes)}"
                });
            }

            // Validate file size
            long maxFileSizeBytes = fileUploadOptions.Value.MaxFileSizeMB * 1024 * 1024;
            if (fileSizeBytes > maxFileSizeBytes)
            {
                return Results.BadRequest(new
                {
                    error = "File too large",
                    message = $"File size {fileSizeBytes / 1024.0 / 1024.0:F2} MB exceeds the maximum allowed size of {fileUploadOptions.Value.MaxFileSizeMB} MB"
                });
            }

            if (fileSizeBytes <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "Invalid file size",
                    message = "File size must be greater than 0"
                });
            }

            SasUrlResult result = await blobStorageService.GenerateSasUrlAsync(fileName, contentType);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
