using System.Collections.Concurrent;
using System.Text.Json;
using DocProcessing.Application.Interfaces;

namespace DocProcessing.EndToEnd.Tests.Mocks;

/// <summary>
/// Stateful in-memory IStorageService fake for integration tests.
/// Uses ConcurrentDictionary for thread safety.
/// Returns new MemoryStream instances per download to avoid stream-position bugs.
/// </summary>
public sealed class InMemoryStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public IReadOnlyDictionary<string, byte[]> Blobs => _blobs;

    public Task<UploadResult> UploadAsync(
        string fileName,
        Stream fileStream,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        using MemoryStream ms = new();
        fileStream.CopyTo(ms);
        byte[] data = ms.ToArray();

        string containerName = "uploads";
        string blobPath = $"{Guid.NewGuid()}/{fileName}";
        string key = $"{containerName}/{blobPath}";

        _blobs[key] = data;

        var result = new UploadResult(
            BlobUrl: $"https://test.blob.core.windows.net/{key}",
            FileName: fileName,
            ContentType: contentType,
            FileSizeBytes: data.Length,
            ETag: $"\"{Guid.NewGuid():N}\"",
            ContainerName: containerName,
            BlobPath: blobPath
        );

        return Task.FromResult(result);
    }

    public Task<Stream> DownloadBlobAsync(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        string key = $"{containerName}/{blobPath}";

        if (!_blobs.TryGetValue(key, out byte[]? data))
        {
            throw new InvalidOperationException($"Blob not found: {key}");
        }

        // Return a new MemoryStream for each call to avoid stream-position issues
        Stream stream = new MemoryStream(data, writable: false);
        return Task.FromResult(stream);
    }

    public Task<string> UploadJsonAsync<T>(
        string containerName,
        string blobPath,
        T data,
        CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(data);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

        string key = $"{containerName}/{blobPath}";
        _blobs[key] = bytes;

        return Task.FromResult(blobPath);
    }

    public Task<T?> DownloadJsonAsync<T>(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken = default) where T : class
    {
        string key = $"{containerName}/{blobPath}";

        if (!_blobs.TryGetValue(key, out byte[]? data))
        {
            return Task.FromResult<T?>(null);
        }

        string json = System.Text.Encoding.UTF8.GetString(data);
        T? result = JsonSerializer.Deserialize<T>(json);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Check if a blob exists at the given path.
    /// </summary>
    public bool BlobExists(string containerName, string blobPath)
        => _blobs.ContainsKey($"{containerName}/{blobPath}");
}
