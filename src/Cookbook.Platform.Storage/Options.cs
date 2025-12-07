namespace Cookbook.Platform.Storage;

/// <summary>
/// Configuration options for Cosmos DB.
/// </summary>
public class CosmosOptions
{
    public const string SectionName = "Cosmos";

    /// <summary>
    /// Cosmos DB endpoint URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Database name.
    /// </summary>
    public string DatabaseName { get; set; } = "cookbook-db";

    /// <summary>
    /// Whether to use the Cosmos Emulator.
    /// </summary>
    public bool UseEmulator { get; set; } = true;

    /// <summary>
    /// Cosmos DB key (development only).
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>
/// Configuration options for Blob Storage.
/// </summary>
public class BlobStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Whether to use Azurite emulator.
    /// </summary>
    public bool UseAzurite { get; set; } = true;

    /// <summary>
    /// Blob storage endpoint.
    /// </summary>
    public string? BlobEndpoint { get; set; }

    /// <summary>
    /// Container name for artifacts.
    /// </summary>
    public string ContainerName { get; set; } = "artifacts";
}
