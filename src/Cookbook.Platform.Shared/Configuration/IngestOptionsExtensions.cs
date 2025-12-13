using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cookbook.Platform.Shared.Configuration;

/// <summary>
/// Extension methods for registering ingest configuration options.
/// </summary>
public static class IngestOptionsExtensions
{
    /// <summary>
    /// Adds ingest configuration options to the service collection.
    /// Binds IngestOptions from "Ingest" section and IngestGuardrailOptions from "Ingest:Guardrail" section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIngestOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IngestOptions>(
            configuration.GetSection(IngestOptions.SectionName));

        services.Configure<IngestGuardrailOptions>(
            configuration.GetSection(IngestGuardrailOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Adds ingest configuration options with custom section names.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="ingestSectionName">The section name for IngestOptions.</param>
    /// <param name="guardrailSectionName">The section name for IngestGuardrailOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIngestOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        string ingestSectionName,
        string guardrailSectionName)
    {
        services.Configure<IngestOptions>(
            configuration.GetSection(ingestSectionName));

        services.Configure<IngestGuardrailOptions>(
            configuration.GetSection(guardrailSectionName));

        return services;
    }
}
