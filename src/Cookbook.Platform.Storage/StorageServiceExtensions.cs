using Azure.Storage.Blobs;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cookbook.Platform.Storage;

/// <summary>
/// Extension methods for registering storage services.
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>
    /// Adds Cosmos DB repositories to the service collection.
    /// </summary>
    public static IServiceCollection AddCosmosRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var cosmosOptions = new CosmosOptions();
        configuration.GetSection(CosmosOptions.SectionName).Bind(cosmosOptions);
        services.AddSingleton(cosmosOptions);

        services.AddSingleton<RecipeRepository>();
        services.AddSingleton<SessionRepository>();
        services.AddSingleton<MessageRepository>();
        services.AddSingleton<TaskRepository>();
        services.AddSingleton<ArtifactRepository>();
        services.AddSingleton<NotesRepository>();

        return services;
    }

    /// <summary>
    /// Adds Cosmos DB repositories with database initialization.
    /// Creates containers and seeds sample data on startup.
    /// </summary>
    public static IServiceCollection AddCosmosRepositoriesWithInitialization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCosmosRepositories(configuration);
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Adds blob storage services to the service collection.
    /// </summary>
    public static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton<IBlobStorage, AzureBlobStorage>();

        return services;
    }
}
