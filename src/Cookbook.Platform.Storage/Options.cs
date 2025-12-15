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

/// <summary>
/// Configuration options for artifact retention policies.
/// </summary>
public class ArtifactRetentionOptions
{
    public const string SectionName = "Storage:Retention";

    /// <summary>
    /// Retention period in days for committed task artifacts.
    /// Committed tasks contain recipes that were successfully imported.
    /// Default: 180 days.
    /// </summary>
    public int CommittedRetentionDays { get; set; } = 180;

    /// <summary>
    /// Retention period in days for non-committed task artifacts.
    /// Includes Rejected, Expired, Failed, and Cancelled tasks.
    /// Default: 30 days.
    /// </summary>
    public int NonCommittedRetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether artifact retention cleanup is enabled.
    /// Default: true.
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// Interval between cleanup runs.
    /// Default: 24 hours.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum number of blobs to delete per cleanup run.
    /// Prevents long-running cleanup operations.
    /// Default: 1000.
    /// </summary>
    public int MaxDeletesPerRun { get; set; } = 1000;
}
