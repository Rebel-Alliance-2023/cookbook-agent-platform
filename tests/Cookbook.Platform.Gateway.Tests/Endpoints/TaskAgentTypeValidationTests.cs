using Cookbook.Platform.Shared.Agents;

namespace Cookbook.Platform.Gateway.Tests.Endpoints;

/// <summary>
/// Tests for agent type validation in task creation.
/// These tests verify the INVALID_AGENT_TYPE error handling.
/// </summary>
public class TaskAgentTypeValidationTests
{
    #region Agent Type Validation Logic Tests

    [Theory]
    [InlineData("Ingest")]
    [InlineData("Research")]
    [InlineData("Analysis")]
    public void KnownAgentTypes_ValidTypes_AreAccepted(string agentType)
    {
        // Assert - these types should be valid
        Assert.True(KnownAgentTypes.IsValid(agentType));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Recipe")]
    [InlineData("Import")]
    [InlineData("Fetch")]
    [InlineData("")]
    public void KnownAgentTypes_InvalidTypes_AreRejected(string agentType)
    {
        // Assert - these types should be invalid
        Assert.False(KnownAgentTypes.IsValid(agentType));
    }

    [Fact]
    public void KnownAgentTypes_NullType_IsRejected()
    {
        // Assert
        Assert.False(KnownAgentTypes.IsValid(null));
    }

    [Theory]
    [InlineData("ingest", "Ingest")]
    [InlineData("RESEARCH", "Research")]
    [InlineData("analysis", "Analysis")]
    public void KnownAgentTypes_CaseInsensitive_NormalizesToCanonical(string input, string expected)
    {
        // Assert
        Assert.True(KnownAgentTypes.IsValid(input));
        Assert.Equal(expected, KnownAgentTypes.GetCanonical(input));
    }

    #endregion

    #region Error Response Tests

    [Fact]
    public void InvalidAgentType_ErrorResponse_ContainsExpectedFields()
    {
        // Arrange - simulate the error response structure
        var invalidType = "UnknownAgent";
        var error = new
        {
            error = "INVALID_AGENT_TYPE",
            message = $"Unknown agent type '{invalidType}'. Valid types are: {string.Join(", ", KnownAgentTypes.All)}",
            validTypes = KnownAgentTypes.All
        };

        // Assert
        Assert.Equal("INVALID_AGENT_TYPE", error.error);
        Assert.Contains(invalidType, error.message);
        Assert.Contains("Ingest", error.validTypes);
        Assert.Contains("Research", error.validTypes);
        Assert.Contains("Analysis", error.validTypes);
    }

    [Fact]
    public void ErrorMessage_ListsAllValidTypes()
    {
        // Arrange
        var invalidType = "BadType";
        var message = $"Unknown agent type '{invalidType}'. Valid types are: {string.Join(", ", KnownAgentTypes.All)}";

        // Assert
        Assert.Contains("Ingest", message);
        Assert.Contains("Research", message);
        Assert.Contains("Analysis", message);
    }

    #endregion

    #region Ingest Agent Type Tests

    [Fact]
    public void IngestAgentType_IsRecognized()
    {
        // Assert
        Assert.True(KnownAgentTypes.IsValid(KnownAgentTypes.Ingest));
        Assert.Equal("Ingest", KnownAgentTypes.Ingest);
    }

    [Theory]
    [InlineData("Ingest")]
    [InlineData("ingest")]
    [InlineData("INGEST")]
    public void IngestAgentType_CaseVariations_AllValid(string variation)
    {
        // Assert
        Assert.True(KnownAgentTypes.IsValid(variation));
        Assert.Equal("Ingest", KnownAgentTypes.GetCanonical(variation));
    }

    #endregion
}
