using Cookbook.Platform.Shared.Models.Ingest.Search;

namespace Cookbook.Platform.Orchestrator.Services.Ingest.Search;

/// <summary>
/// Resolves and manages search providers.
/// </summary>
public interface ISearchProviderResolver
{
    /// <summary>
    /// Gets the default provider ID.
    /// </summary>
    string DefaultProviderId { get; }

    /// <summary>
    /// Resolves a search provider by its ID.
    /// </summary>
    /// <param name="providerId">The provider ID to resolve. If null or empty, returns the default provider.</param>
    /// <returns>The resolved search provider.</returns>
    /// <exception cref="SearchProviderNotFoundException">Thrown when the provider is not found or disabled.</exception>
    ISearchProvider Resolve(string? providerId);

    /// <summary>
    /// Attempts to resolve a search provider by its ID.
    /// </summary>
    /// <param name="providerId">The provider ID to resolve. If null or empty, returns the default provider.</param>
    /// <param name="provider">The resolved provider, or null if not found.</param>
    /// <returns>True if the provider was resolved, false otherwise.</returns>
    bool TryResolve(string? providerId, out ISearchProvider? provider);

    /// <summary>
    /// Lists all enabled search providers.
    /// </summary>
    /// <returns>A list of enabled provider descriptors.</returns>
    IReadOnlyList<SearchProviderDescriptor> ListEnabled();

    /// <summary>
    /// Lists all registered search providers (including disabled ones).
    /// </summary>
    /// <returns>A list of all provider descriptors.</returns>
    IReadOnlyList<SearchProviderDescriptor> ListAll();

    /// <summary>
    /// Gets the descriptor for a specific provider.
    /// </summary>
    /// <param name="providerId">The provider ID.</param>
    /// <returns>The provider descriptor, or null if not found.</returns>
    SearchProviderDescriptor? GetDescriptor(string providerId);
}

/// <summary>
/// Exception thrown when a search provider cannot be resolved.
/// </summary>
public class SearchProviderNotFoundException : Exception
{
    /// <summary>
    /// The provider ID that was not found.
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// The error code for structured error handling.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SearchProviderNotFoundException"/>.
    /// </summary>
    public SearchProviderNotFoundException(string providerId, string message, string errorCode = "INVALID_SEARCH_PROVIDER")
        : base(message)
    {
        ProviderId = providerId;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an exception for an unknown provider.
    /// </summary>
    public static SearchProviderNotFoundException Unknown(string providerId)
        => new(providerId, $"Search provider '{providerId}' is not registered.", "UNKNOWN_SEARCH_PROVIDER");

    /// <summary>
    /// Creates an exception for a disabled provider.
    /// </summary>
    public static SearchProviderNotFoundException Disabled(string providerId)
        => new(providerId, $"Search provider '{providerId}' is disabled.", "DISABLED_SEARCH_PROVIDER");
}
