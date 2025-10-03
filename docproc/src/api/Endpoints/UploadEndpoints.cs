using Api.Services;
using Microsoft.AspNetCore.OpenApi;

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
    /// <param name="contentType">Optional content type of the file.</param>
    /// <param name="blobStorageService">The blob storage service.</param>
    /// <returns>A result containing the SAS URL and metadata.</returns>
    private static async Task<IResult> GenerateSasUrlAsync(
        string fileName,
        string? contentType,
        IBlobStorageService blobStorageService)
    {
        try
        {
            SasUrlResult result = await blobStorageService.GenerateSasUrlAsync(fileName, contentType);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
