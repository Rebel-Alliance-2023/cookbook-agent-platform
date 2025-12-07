using System.ComponentModel;
using Cookbook.Platform.Storage;
using ModelContextProtocol.Server;

namespace Cookbook.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for file operations.
/// </summary>
[McpServerToolType]
public class FileTools
{
    private readonly IBlobStorage _blobStorage;

    public FileTools(IBlobStorage blobStorage)
    {
        _blobStorage = blobStorage;
    }

    /// <summary>
    /// Uploads a file to blob storage.
    /// </summary>
    [McpServerTool(Name = "files_put")]
    [Description("Uploads a file to blob storage and returns the URI.")]
    public async Task<FileUploadResult> PutFile(
        [Description("The name/path for the file in storage")] string name,
        [Description("Base64-encoded content of the file")] string contentBase64,
        [Description("Content type (e.g., application/pdf, text/markdown)")] string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var content = Convert.FromBase64String(contentBase64);
        var uri = await _blobStorage.UploadAsync(name, content, contentType, cancellationToken);

        return new FileUploadResult
        {
            Name = name,
            Uri = uri,
            SizeBytes = content.Length,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Lists files in blob storage.
    /// </summary>
    [McpServerTool(Name = "files_list")]
    [Description("Lists files in blob storage with optional prefix filter.")]
    public async Task<List<string>> ListFiles(
        [Description("Optional prefix to filter files")] string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        return await _blobStorage.ListAsync(prefix, cancellationToken);
    }

    /// <summary>
    /// Downloads a file from blob storage.
    /// </summary>
    [McpServerTool(Name = "files_get")]
    [Description("Downloads a file from blob storage and returns Base64-encoded content.")]
    public async Task<FileDownloadResult?> GetFile(
        [Description("The name/path of the file in storage")] string name,
        CancellationToken cancellationToken = default)
    {
        var content = await _blobStorage.DownloadAsync(name, cancellationToken);
        
        if (content == null)
        {
            return null;
        }

        return new FileDownloadResult
        {
            Name = name,
            ContentBase64 = Convert.ToBase64String(content),
            SizeBytes = content.Length
        };
    }

    /// <summary>
    /// Checks if a file exists in blob storage.
    /// </summary>
    [McpServerTool(Name = "files_exists")]
    [Description("Checks if a file exists in blob storage.")]
    public async Task<bool> FileExists(
        [Description("The name/path of the file in storage")] string name,
        CancellationToken cancellationToken = default)
    {
        return await _blobStorage.ExistsAsync(name, cancellationToken);
    }
}

/// <summary>
/// Result of a file upload operation.
/// </summary>
public record FileUploadResult
{
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public long SizeBytes { get; init; }
    public string? ContentType { get; init; }
}

/// <summary>
/// Result of a file download operation.
/// </summary>
public record FileDownloadResult
{
    public required string Name { get; init; }
    public required string ContentBase64 { get; init; }
    public long SizeBytes { get; init; }
}
