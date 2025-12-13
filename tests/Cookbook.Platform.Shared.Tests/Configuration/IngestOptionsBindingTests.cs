using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Cookbook.Platform.Shared.Configuration;

namespace Cookbook.Platform.Shared.Tests.Configuration;

/// <summary>
/// Unit tests for ingest configuration options binding.
/// </summary>
public class IngestOptionsBindingTests
{
    #region IngestOptions Binding

    [Fact]
    public void IngestOptions_BindsFromConfiguration()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ingest:MaxDiscoveryCandidates"] = "15",
            ["Ingest:DraftExpirationDays"] = "14",
            ["Ingest:MaxFetchSizeBytes"] = "10485760",
            ["Ingest:ContentCharacterBudget"] = "80000",
            ["Ingest:RespectRobotsTxt"] = "false",
            ["Ingest:MaxArtifactSizeBytes"] = "2097152",
            ["Ingest:UserAgent"] = "TestAgent/1.0",
            ["Ingest:FetchTimeoutSeconds"] = "60",
            ["Ingest:MaxFetchRetries"] = "3"
        });

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;

        // Assert
        Assert.Equal(15, options.MaxDiscoveryCandidates);
        Assert.Equal(14, options.DraftExpirationDays);
        Assert.Equal(10485760, options.MaxFetchSizeBytes);
        Assert.Equal(80000, options.ContentCharacterBudget);
        Assert.False(options.RespectRobotsTxt);
        Assert.Equal(2097152, options.MaxArtifactSizeBytes);
        Assert.Equal("TestAgent/1.0", options.UserAgent);
        Assert.Equal(60, options.FetchTimeoutSeconds);
        Assert.Equal(3, options.MaxFetchRetries);
    }

    [Fact]
    public void IngestOptions_UsesDefaultValues_WhenNotConfigured()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;

        // Assert
        Assert.Equal(10, options.MaxDiscoveryCandidates);
        Assert.Equal(7, options.DraftExpirationDays);
        Assert.Equal(5 * 1024 * 1024, options.MaxFetchSizeBytes);
        Assert.Equal(60_000, options.ContentCharacterBudget);
        Assert.True(options.RespectRobotsTxt);
        Assert.Equal(1 * 1024 * 1024, options.MaxArtifactSizeBytes);
        Assert.Equal(30, options.FetchTimeoutSeconds);
        Assert.Equal(2, options.MaxFetchRetries);
    }

    [Fact]
    public void IngestOptions_ComputedProperties_ReturnCorrectTimeSpans()
    {
        // Arrange
        var options = new IngestOptions
        {
            DraftExpirationDays = 14,
            FetchTimeoutSeconds = 45
        };

        // Assert
        Assert.Equal(TimeSpan.FromDays(14), options.DraftExpiration);
        Assert.Equal(TimeSpan.FromSeconds(45), options.FetchTimeout);
    }

    #endregion

    #region CircuitBreakerOptions Binding

    [Fact]
    public void CircuitBreakerOptions_BindsFromConfiguration()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ingest:CircuitBreaker:FailureThreshold"] = "10",
            ["Ingest:CircuitBreaker:FailureWindowMinutes"] = "15",
            ["Ingest:CircuitBreaker:BlockDurationMinutes"] = "60"
        });

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;

        // Assert
        Assert.Equal(10, options.CircuitBreaker.FailureThreshold);
        Assert.Equal(15, options.CircuitBreaker.FailureWindowMinutes);
        Assert.Equal(60, options.CircuitBreaker.BlockDurationMinutes);
    }

    [Fact]
    public void CircuitBreakerOptions_UsesDefaultValues_WhenNotConfigured()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;

        // Assert
        Assert.Equal(5, options.CircuitBreaker.FailureThreshold);
        Assert.Equal(10, options.CircuitBreaker.FailureWindowMinutes);
        Assert.Equal(30, options.CircuitBreaker.BlockDurationMinutes);
    }

    [Fact]
    public void CircuitBreakerOptions_ComputedProperties_ReturnCorrectTimeSpans()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureWindowMinutes = 20,
            BlockDurationMinutes = 45
        };

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(20), options.FailureWindow);
        Assert.Equal(TimeSpan.FromMinutes(45), options.BlockDuration);
    }

    #endregion

    #region IngestGuardrailOptions Binding

    [Fact]
    public void IngestGuardrailOptions_BindsFromConfiguration()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ingest:Guardrail:TokenOverlapWarningThreshold"] = "50",
            ["Ingest:Guardrail:TokenOverlapErrorThreshold"] = "100",
            ["Ingest:Guardrail:NgramSimilarityWarningThreshold"] = "0.25",
            ["Ingest:Guardrail:NgramSimilarityErrorThreshold"] = "0.40",
            ["Ingest:Guardrail:NgramSize"] = "6",
            ["Ingest:Guardrail:AutoRepairOnError"] = "false"
        });

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestGuardrailOptions>>().Value;

        // Assert
        Assert.Equal(50, options.TokenOverlapWarningThreshold);
        Assert.Equal(100, options.TokenOverlapErrorThreshold);
        Assert.Equal(0.25, options.NgramSimilarityWarningThreshold);
        Assert.Equal(0.40, options.NgramSimilarityErrorThreshold);
        Assert.Equal(6, options.NgramSize);
        Assert.False(options.AutoRepairOnError);
    }

    [Fact]
    public void IngestGuardrailOptions_UsesDefaultValues_WhenNotConfigured()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestGuardrailOptions>>().Value;

        // Assert
        Assert.Equal(40, options.TokenOverlapWarningThreshold);
        Assert.Equal(80, options.TokenOverlapErrorThreshold);
        Assert.Equal(0.20, options.NgramSimilarityWarningThreshold);
        Assert.Equal(0.35, options.NgramSimilarityErrorThreshold);
        Assert.Equal(5, options.NgramSize);
        Assert.True(options.AutoRepairOnError);
    }

    #endregion

    #region Full Configuration Tests

    [Fact]
    public void AddIngestOptions_RegistersBothOptionTypes()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ingest:MaxDiscoveryCandidates"] = "12",
            ["Ingest:Guardrail:NgramSize"] = "7"
        });

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var ingestOptions = provider.GetService<IOptions<IngestOptions>>();
        var guardrailOptions = provider.GetService<IOptions<IngestGuardrailOptions>>();

        // Assert
        Assert.NotNull(ingestOptions);
        Assert.NotNull(guardrailOptions);
        Assert.Equal(12, ingestOptions.Value.MaxDiscoveryCandidates);
        Assert.Equal(7, guardrailOptions.Value.NgramSize);
    }

    [Fact]
    public void AddIngestOptions_WithCustomSectionNames_BindsCorrectly()
    {
        // Arrange
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["CustomIngest:MaxDiscoveryCandidates"] = "20",
            ["CustomGuardrail:NgramSize"] = "8"
        });

        var services = new ServiceCollection();
        services.AddIngestOptions(config, "CustomIngest", "CustomGuardrail");
        var provider = services.BuildServiceProvider();

        // Act
        var ingestOptions = provider.GetRequiredService<IOptions<IngestOptions>>().Value;
        var guardrailOptions = provider.GetRequiredService<IOptions<IngestGuardrailOptions>>().Value;

        // Assert
        Assert.Equal(20, ingestOptions.MaxDiscoveryCandidates);
        Assert.Equal(8, guardrailOptions.NgramSize);
    }

    #endregion

    #region JSON Configuration Tests

    [Fact]
    public void IngestOptions_BindsFromJsonConfiguration()
    {
        // Arrange
        var json = """
        {
            "Ingest": {
                "MaxDiscoveryCandidates": 25,
                "DraftExpirationDays": 10,
                "CircuitBreaker": {
                    "FailureThreshold": 8,
                    "FailureWindowMinutes": 20
                }
            }
        }
        """;

        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;

        // Assert
        Assert.Equal(25, options.MaxDiscoveryCandidates);
        Assert.Equal(10, options.DraftExpirationDays);
        Assert.Equal(8, options.CircuitBreaker.FailureThreshold);
        Assert.Equal(20, options.CircuitBreaker.FailureWindowMinutes);
    }

    [Fact]
    public void IngestGuardrailOptions_BindsFromJsonConfiguration()
    {
        // Arrange
        var json = """
        {
            "Ingest": {
                "Guardrail": {
                    "TokenOverlapWarningThreshold": 45,
                    "NgramSimilarityErrorThreshold": 0.50,
                    "AutoRepairOnError": false
                }
            }
        }
        """;

        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        var services = new ServiceCollection();
        services.AddIngestOptions(config);
        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<IngestGuardrailOptions>>().Value;

        // Assert
        Assert.Equal(45, options.TokenOverlapWarningThreshold);
        Assert.Equal(0.50, options.NgramSimilarityErrorThreshold);
        Assert.False(options.AutoRepairOnError);
    }

    #endregion

    #region Helper Methods

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    #endregion
}
