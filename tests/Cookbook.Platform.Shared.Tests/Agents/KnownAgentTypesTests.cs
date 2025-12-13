using Cookbook.Platform.Shared.Agents;

namespace Cookbook.Platform.Shared.Tests.Agents;

/// <summary>
/// Unit tests for the KnownAgentTypes class.
/// </summary>
public class KnownAgentTypesTests
{
    #region IsValid Tests

    [Theory]
    [InlineData("Ingest", true)]
    [InlineData("Research", true)]
    [InlineData("Analysis", true)]
    [InlineData("ingest", true)]      // Case insensitive
    [InlineData("RESEARCH", true)]    // Case insensitive
    [InlineData("analysis", true)]    // Case insensitive
    [InlineData("InGeSt", true)]      // Mixed case
    public void IsValid_KnownAgentTypes_ReturnsTrue(string agentType, bool expected)
    {
        // Act
        var result = KnownAgentTypes.IsValid(agentType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Recipe")]
    [InlineData("Import")]
    [InlineData("Fetch")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_UnknownAgentTypes_ReturnsFalse(string agentType)
    {
        // Act
        var result = KnownAgentTypes.IsValid(agentType);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_NullAgentType_ReturnsFalse()
    {
        // Act
        var result = KnownAgentTypes.IsValid(null);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetCanonical Tests

    [Theory]
    [InlineData("Ingest", "Ingest")]
    [InlineData("ingest", "Ingest")]
    [InlineData("INGEST", "Ingest")]
    [InlineData("Research", "Research")]
    [InlineData("research", "Research")]
    [InlineData("Analysis", "Analysis")]
    [InlineData("ANALYSIS", "Analysis")]
    public void GetCanonical_KnownAgentTypes_ReturnsCanonicalCasing(string input, string expected)
    {
        // Act
        var result = KnownAgentTypes.GetCanonical(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Recipe")]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCanonical_UnknownAgentTypes_ReturnsNull(string input)
    {
        // Act
        var result = KnownAgentTypes.GetCanonical(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCanonical_NullAgentType_ReturnsNull()
    {
        // Act
        var result = KnownAgentTypes.GetCanonical(null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region All Property Tests

    [Fact]
    public void All_ContainsExpectedAgentTypes()
    {
        // Assert
        Assert.Contains("Ingest", KnownAgentTypes.All);
        Assert.Contains("Research", KnownAgentTypes.All);
        Assert.Contains("Analysis", KnownAgentTypes.All);
    }

    [Fact]
    public void All_HasCorrectCount()
    {
        // Assert
        Assert.Equal(3, KnownAgentTypes.All.Length);
    }

    #endregion
}
