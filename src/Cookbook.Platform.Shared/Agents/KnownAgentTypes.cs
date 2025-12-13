namespace Cookbook.Platform.Shared.Agents;

/// <summary>
/// Defines the known agent types supported by the platform.
/// </summary>
public static class KnownAgentTypes
{
    /// <summary>
    /// Ingest agent for importing recipes from external sources.
    /// </summary>
    public const string Ingest = "Ingest";

    /// <summary>
    /// Research agent for exploring recipe-related topics.
    /// </summary>
    public const string Research = "Research";

    /// <summary>
    /// Analysis agent for analyzing recipes and nutritional data.
    /// </summary>
    public const string Analysis = "Analysis";

    /// <summary>
    /// All known agent types.
    /// </summary>
    public static readonly string[] All = [Ingest, Research, Analysis];

    /// <summary>
    /// Checks if the specified agent type is known/valid.
    /// </summary>
    /// <param name="agentType">The agent type to validate.</param>
    /// <returns>True if the agent type is known, false otherwise.</returns>
    public static bool IsValid(string? agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            return false;
        }

        return All.Contains(agentType, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the canonical (properly cased) version of an agent type.
    /// </summary>
    /// <param name="agentType">The agent type to normalize.</param>
    /// <returns>The canonical agent type, or null if not found.</returns>
    public static string? GetCanonical(string? agentType)
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            return null;
        }

        return All.FirstOrDefault(t => t.Equals(agentType, StringComparison.OrdinalIgnoreCase));
    }
}
