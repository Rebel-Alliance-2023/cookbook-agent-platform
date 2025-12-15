using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Cookbook.Platform.Orchestrator.Tests.GoldenSet;

/// <summary>
/// Golden set tests that validate the recipe extraction pipeline against curated fixtures.
/// These tests ensure consistent extraction quality across releases.
/// </summary>
[Trait("Category", "GoldenSet")]
public class GoldenSetTests
{
    private readonly ITestOutputHelper _output;
    private readonly JsonLdRecipeExtractor _jsonLdExtractor;
    private readonly JsonSerializerOptions _jsonOptions;

    public GoldenSetTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerMock = new Mock<ILogger<JsonLdRecipeExtractor>>();
        _jsonLdExtractor = new JsonLdRecipeExtractor(loggerMock.Object);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Creates a default extraction context for testing.
    /// </summary>
    private static ExtractionContext CreateContext(string? url = null) => new()
    {
        SourceUrl = url ?? "https://example.com/recipe"
    };

    #region Manifest Tests

    [Fact]
    public async Task Manifest_LoadsSuccessfully()
    {
        // Act
        var manifest = await FixtureLoader.LoadManifestAsync();

        // Assert
        Assert.NotNull(manifest);
        Assert.Equal("1.0", manifest.Version);
        Assert.NotEmpty(manifest.Fixtures.JsonLd);
        Assert.NotEmpty(manifest.Fixtures.PlainText);
        
        _output.WriteLine($"Loaded manifest with {manifest.Metadata.TotalFixtures} total fixtures");
        _output.WriteLine($"  JSON-LD: {manifest.Metadata.JsonLdFixtures}");
        _output.WriteLine($"  Plain Text: {manifest.Metadata.PlaintextFixtures}");
    }

    [Fact]
    public async Task Manifest_AllFixtureFilesExist()
    {
        // Arrange
        var manifest = await FixtureLoader.LoadManifestAsync();
        var missingFiles = new List<string>();

        // Act - Check JSON-LD fixtures
        foreach (var fixture in manifest.Fixtures.JsonLd)
        {
            try
            {
                await FixtureLoader.LoadJsonLdFixtureAsync(fixture.InputFile);
            }
            catch (FileNotFoundException)
            {
                missingFiles.Add(fixture.InputFile);
            }
        }

        // Check Plain Text fixtures
        foreach (var fixture in manifest.Fixtures.PlainText)
        {
            try
            {
                await FixtureLoader.LoadPlainTextFixtureAsync(fixture.InputFile);
            }
            catch (FileNotFoundException)
            {
                missingFiles.Add(fixture.InputFile);
            }
        }

        // Assert
        Assert.Empty(missingFiles);
    }

    #endregion

    #region JSON-LD Extraction Tests

    [Fact]
    public async Task JsonLd_AllFixtures_ExtractSuccessfully()
    {
        // Arrange
        var fixtures = new List<FixtureDescriptor>();
        await foreach (var fixture in FixtureLoader.GetJsonLdFixturesAsync())
        {
            fixtures.Add(fixture);
        }

        var failures = new List<(string FixtureId, string Error)>();

        // Act
        foreach (var fixture in fixtures)
        {
            try
            {
                var jsonLd = await FixtureLoader.LoadJsonLdFixtureAsync(fixture.InputFile);
                var result = await _jsonLdExtractor.ExtractAsync(jsonLd, CreateContext());

                if (!result.Success)
                {
                    failures.Add((fixture.Id, result.Error ?? "Unknown error"));
                }
                else
                {
                    _output.WriteLine($"? {fixture.Id}: {result.Recipe?.Name}");
                }
            }
            catch (Exception ex)
            {
                failures.Add((fixture.Id, ex.Message));
            }
        }

        // Assert
        if (failures.Any())
        {
            foreach (var (id, error) in failures)
            {
                _output.WriteLine($"? {id}: {error}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task JsonLd_ChocolateChipCookies_ExtractsCorrectly()
    {
        await AssertJsonLdExtractionAsync(
            "JsonLd/001-chocolate-chip-cookies.jsonld",
            recipe =>
            {
                Assert.Equal("Classic Chocolate Chip Cookies", recipe.Name);
                Assert.NotEmpty(recipe.Ingredients);
                Assert.NotEmpty(recipe.Instructions);
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("flour", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("chocolate chips", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task JsonLd_TuscanChicken_ExtractsCorrectly()
    {
        await AssertJsonLdExtractionAsync(
            "JsonLd/002-tuscan-chicken.jsonld",
            recipe =>
            {
                Assert.Equal("Creamy Garlic Tuscan Chicken", recipe.Name);
                Assert.Equal(4, recipe.Servings);
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("chicken", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("spinach", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task JsonLd_BananaBread_ExtractsCorrectly()
    {
        await AssertJsonLdExtractionAsync(
            "JsonLd/003-banana-bread.jsonld",
            recipe =>
            {
                Assert.Equal("Classic Banana Bread", recipe.Name);
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("banana", StringComparison.OrdinalIgnoreCase));
                Assert.True(recipe.CookTimeMinutes > 0);
            });
    }

    [Fact]
    public async Task JsonLd_BeefTacos_ExtractsCorrectly()
    {
        await AssertJsonLdExtractionAsync(
            "JsonLd/004-beef-tacos.jsonld",
            recipe =>
            {
                Assert.Equal("Homemade Beef Tacos", recipe.Name);
                Assert.Equal("Mexican", recipe.Cuisine);
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("beef", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("tortilla", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task JsonLd_ChickenRice_ExtractsCorrectly()
    {
        await AssertJsonLdExtractionAsync(
            "JsonLd/005-chicken-rice.jsonld",
            recipe =>
            {
                Assert.Equal("One-Pot Chicken and Rice", recipe.Name);
                Assert.Equal(6, recipe.Servings);
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("chicken", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(recipe.Ingredients, i => i.Name.Contains("rice", StringComparison.OrdinalIgnoreCase));
            });
    }

    #endregion

    #region Plain Text Loading Tests

    [Fact]
    public async Task PlainText_AllFixtures_LoadSuccessfully()
    {
        // Arrange
        var fixtures = new List<FixtureDescriptor>();
        await foreach (var fixture in FixtureLoader.GetPlainTextFixturesAsync())
        {
            fixtures.Add(fixture);
        }

        var failures = new List<(string FixtureId, string Error)>();

        // Act
        foreach (var fixture in fixtures)
        {
            try
            {
                var content = await FixtureLoader.LoadPlainTextFixtureAsync(fixture.InputFile);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    failures.Add((fixture.Id, "Empty content"));
                }
                else
                {
                    _output.WriteLine($"? {fixture.Id}: {content.Length} chars");
                }
            }
            catch (Exception ex)
            {
                failures.Add((fixture.Id, ex.Message));
            }
        }

        // Assert
        Assert.Empty(failures);
    }

    [Fact]
    public async Task PlainText_ApplePie_ContainsExpectedContent()
    {
        // Arrange
        var content = await FixtureLoader.LoadPlainTextFixtureAsync("PlainText/001-apple-pie.txt");

        // Assert
        Assert.Contains("Grandma's Apple Pie", content);
        Assert.Contains("Prep Time", content);
        Assert.Contains("Cook Time", content);
        Assert.Contains("Granny Smith apples", content);
        Assert.Contains("Instructions", content);
    }

    [Fact]
    public async Task PlainText_ButterChicken_ContainsExpectedContent()
    {
        // Arrange
        var content = await FixtureLoader.LoadPlainTextFixtureAsync("PlainText/004-butter-chicken.txt");

        // Assert (case-insensitive checks)
        Assert.Contains("BUTTER CHICKEN", content.ToUpperInvariant());
        Assert.Contains("MARINADE", content.ToUpperInvariant());
        Assert.Contains("SAUCE", content.ToUpperInvariant());
        Assert.Contains("garam masala", content.ToLowerInvariant());
    }

    #endregion

    #region Quality Metrics Tests

    [Fact]
    public async Task JsonLd_AllFixtures_HaveRequiredFields()
    {
        // Arrange
        var fixtures = new List<FixtureDescriptor>();
        await foreach (var fixture in FixtureLoader.GetJsonLdFixturesAsync())
        {
            fixtures.Add(fixture);
        }

        var incompleteRecipes = new List<(string FixtureId, List<string> MissingFields)>();

        // Act
        foreach (var fixture in fixtures)
        {
            var jsonLd = await FixtureLoader.LoadJsonLdFixtureAsync(fixture.InputFile);
            var result = await _jsonLdExtractor.ExtractAsync(jsonLd, CreateContext());

            if (result.Success && result.Recipe != null)
            {
                var missing = new List<string>();
                
                if (string.IsNullOrWhiteSpace(result.Recipe.Name))
                    missing.Add("Name");
                if (!result.Recipe.Ingredients.Any())
                    missing.Add("Ingredients");
                if (!result.Recipe.Instructions.Any())
                    missing.Add("Instructions");

                if (missing.Any())
                {
                    incompleteRecipes.Add((fixture.Id, missing));
                }
            }
        }

        // Assert
        if (incompleteRecipes.Any())
        {
            foreach (var (id, fields) in incompleteRecipes)
            {
                _output.WriteLine($"? {id} missing: {string.Join(", ", fields)}");
            }
        }

        Assert.Empty(incompleteRecipes);
    }

    [Fact]
    public async Task JsonLd_IngredientCount_MeetsExpectations()
    {
        // Arrange - Define minimum expected ingredient counts
        var expectedCounts = new Dictionary<string, int>
        {
            ["001-chocolate-chip-cookies"] = 8,
            ["002-tuscan-chicken"] = 10,
            ["003-banana-bread"] = 8,
            ["004-beef-tacos"] = 10,
            ["005-chicken-rice"] = 10
        };

        var fixtures = new List<FixtureDescriptor>();
        await foreach (var fixture in FixtureLoader.GetJsonLdFixturesAsync())
        {
            fixtures.Add(fixture);
        }

        // Act & Assert
        foreach (var fixture in fixtures)
        {
            var jsonLd = await FixtureLoader.LoadJsonLdFixtureAsync(fixture.InputFile);
            var result = await _jsonLdExtractor.ExtractAsync(jsonLd, CreateContext());

            if (result.Success && expectedCounts.TryGetValue(fixture.Id, out var expected))
            {
                var actual = result.Recipe?.Ingredients.Count ?? 0;
                _output.WriteLine($"{fixture.Id}: {actual} ingredients (expected >= {expected})");
                Assert.True(actual >= expected, 
                    $"Fixture {fixture.Id} has {actual} ingredients, expected at least {expected}");
            }
        }
    }

    #endregion

    #region Helper Methods

    private async Task AssertJsonLdExtractionAsync(string fixturePath, Action<Recipe> assertions)
    {
        // Arrange
        var jsonLd = await FixtureLoader.LoadJsonLdFixtureAsync(fixturePath);

        // Act
        var result = await _jsonLdExtractor.ExtractAsync(jsonLd, CreateContext());

        // Assert
        Assert.True(result.Success, $"Extraction failed: {result.Error}");
        Assert.NotNull(result.Recipe);
        
        assertions(result.Recipe);
        
        _output.WriteLine($"Extracted: {result.Recipe.Name}");
        _output.WriteLine($"  Ingredients: {result.Recipe.Ingredients.Count}");
        _output.WriteLine($"  Instructions: {result.Recipe.Instructions.Count}");
    }

    #endregion
}
