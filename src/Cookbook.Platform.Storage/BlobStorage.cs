using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cookbook.Platform.Storage;

/// <summary>
/// Abstraction for blob storage operations.
/// </summary>
public interface IBlobStorage
{
    Task<string> UploadAsync(string name, byte[] content, string? contentType = null, CancellationToken cancellationToken = default);
    Task<string> UploadAsync(string name, Stream content, string? contentType = null, CancellationToken cancellationToken = default);
    Task<string> UploadWithMetadataAsync(string name, byte[] content, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    Task<string> UploadWithMetadataAsync(string name, Stream content, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadStreamAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    Task<List<string>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);
    Task<IDictionary<string, string>?> GetMetadataAsync(string name, CancellationToken cancellationToken = default);
    Task SetMetadataAsync(string name, IDictionary<string, string> metadata, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Blob Storage implementation.
/// </summary>
public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorage> _logger;

    public AzureBlobStorage(BlobServiceClient blobServiceClient, IOptions<BlobStorageOptions> options, ILogger<AzureBlobStorage> logger)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.ContainerName);
        _logger = logger;
    }

    public Task<string> UploadAsync(string name, byte[] content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        return UploadWithMetadataAsync(name, content, contentType, null, cancellationToken);
    }

    public Task<string> UploadAsync(string name, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        return UploadWithMetadataAsync(name, content, contentType, null, cancellationToken);
    }

    public async Task<string> UploadWithMetadataAsync(string name, byte[] content, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        return await UploadWithMetadataAsync(name, stream, contentType, metadata, cancellationToken);
    }

    public async Task<string> UploadWithMetadataAsync(string name, Stream content, string? contentType = null, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        
        var blobClient = _containerClient.GetBlobClient(name);
        var options = new BlobUploadOptions();
        
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            options.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        }

        if (metadata != null && metadata.Count > 0)
        {
            options.Metadata = new Dictionary<string, string>(metadata);
        }

        await blobClient.UploadAsync(content, options, cancellationToken);
        _logger.LogInformation("Uploaded blob {BlobName}", name);
        
        return blobClient.Uri.ToString();
    }

    public async Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blobClient.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToArray();
    }

    public async Task<Stream?> DownloadStreamAsync(string name, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var response = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return response;
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted blob {BlobName}", name);
    }

    public async Task<List<string>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            results.Add(blobItem.Name);
        }

        return results;
    }

    public async Task<IDictionary<string, string>?> GetMetadataAsync(string name, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.Metadata;
    }

    public async Task SetMetadataAsync(string name, IDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(name);
        await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        _logger.LogDebug("Set metadata on blob {BlobName}", name);
    }
}
