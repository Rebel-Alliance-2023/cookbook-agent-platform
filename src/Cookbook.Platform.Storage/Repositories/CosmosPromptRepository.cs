using Cookbook.Platform.Shared.Models.Prompts;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Cookbook.Platform.Storage.Repositories;

/// <summary>
/// Cosmos DB implementation of the prompt repository.
/// Uses /phase as the partition key for efficient queries by phase.
/// </summary>
public class CosmosPromptRepository : IPromptRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosPromptRepository> _logger;

    public CosmosPromptRepository(
        CosmosClient cosmosClient,
        CosmosOptions options,
        ILogger<CosmosPromptRepository> logger)
    {
        _container = cosmosClient.GetContainer(options.DatabaseName, "prompts");
        _logger = logger;
    }

    public async Task<PromptTemplate?> GetByIdAsync(string id, string phase, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PromptTemplate>(
                id,
                new PartitionKey(phase),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Prompt template {Id} not found in phase {Phase}", id, phase);
            return null;
        }
    }

    public async Task<PromptTemplate?> GetActiveByPhaseAsync(string phase, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.phase = @phase AND c.isActive = true")
            .WithParameter("@phase", phase);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(phase)
        };

        using var iterator = _container.GetItemQueryIterator<PromptTemplate>(query, requestOptions: options);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var template = response.FirstOrDefault();

            if (template != null)
            {
                _logger.LogDebug("Found active prompt template {Id} for phase {Phase}", template.Id, phase);
            }

            return template;
        }

        _logger.LogDebug("No active prompt template found for phase {Phase}", phase);
        return null;
    }

    public async Task<List<PromptTemplate>> GetByPhaseAsync(string phase, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.phase = @phase ORDER BY c.version DESC")
            .WithParameter("@phase", phase);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(phase)
        };

        var results = new List<PromptTemplate>();
        using var iterator = _container.GetItemQueryIterator<PromptTemplate>(query, requestOptions: options);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogDebug("Found {Count} prompt templates for phase {Phase}", results.Count, phase);
        return results;
    }

    public async Task<List<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.phase, c.version DESC");

        var results = new List<PromptTemplate>();
        using var iterator = _container.GetItemQueryIterator<PromptTemplate>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogDebug("Found {Count} prompt templates across all phases", results.Count);
        return results;
    }

    public async Task<PromptTemplate> CreateAsync(PromptTemplate template, CancellationToken cancellationToken = default)
    {
        var templateToCreate = template with
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        var response = await _container.CreateItemAsync(
            templateToCreate,
            new PartitionKey(template.Phase),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created prompt template {Id} for phase {Phase}", template.Id, template.Phase);
        return response.Resource;
    }

    public async Task<PromptTemplate> UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default)
    {
        var templateToUpdate = template with
        {
            UpdatedAt = DateTime.UtcNow
        };

        var response = await _container.ReplaceItemAsync(
            templateToUpdate,
            template.Id,
            new PartitionKey(template.Phase),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Updated prompt template {Id} for phase {Phase}", template.Id, template.Phase);
        return response.Resource;
    }

    public async Task<PromptTemplate> ActivateAsync(string id, string phase, CancellationToken cancellationToken = default)
    {
        // First, deactivate any currently active template for this phase
        var currentActive = await GetActiveByPhaseAsync(phase, cancellationToken);
        if (currentActive != null && currentActive.Id != id)
        {
            await DeactivateAsync(currentActive.Id, phase, cancellationToken);
        }

        // Get and activate the requested template
        var template = await GetByIdAsync(id, phase, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt template {id} not found in phase {phase}");

        if (template.IsActive)
        {
            _logger.LogDebug("Prompt template {Id} is already active", id);
            return template;
        }

        var activatedTemplate = template with
        {
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };

        var response = await _container.ReplaceItemAsync(
            activatedTemplate,
            id,
            new PartitionKey(phase),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Activated prompt template {Id} for phase {Phase}", id, phase);
        return response.Resource;
    }

    public async Task<PromptTemplate> DeactivateAsync(string id, string phase, CancellationToken cancellationToken = default)
    {
        var template = await GetByIdAsync(id, phase, cancellationToken)
            ?? throw new InvalidOperationException($"Prompt template {id} not found in phase {phase}");

        if (!template.IsActive)
        {
            _logger.LogDebug("Prompt template {Id} is already inactive", id);
            return template;
        }

        var deactivatedTemplate = template with
        {
            IsActive = false,
            UpdatedAt = DateTime.UtcNow
        };

        var response = await _container.ReplaceItemAsync(
            deactivatedTemplate,
            id,
            new PartitionKey(phase),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Deactivated prompt template {Id} for phase {Phase}", id, phase);
        return response.Resource;
    }

    public async Task DeleteAsync(string id, string phase, CancellationToken cancellationToken = default)
    {
        await _container.DeleteItemAsync<PromptTemplate>(
            id,
            new PartitionKey(phase),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted prompt template {Id} from phase {Phase}", id, phase);
    }
}
