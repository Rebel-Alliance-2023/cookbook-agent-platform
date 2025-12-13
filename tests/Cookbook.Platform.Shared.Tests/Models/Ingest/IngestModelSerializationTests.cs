using System.Text.Json;
using Newtonsoft.Json;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;

namespace Cookbook.Platform.Shared.Tests.Models.Ingest;

/// <summary>
/// Unit tests for verifying JSON serialization round-trips of ingest domain models.
/// Tests both System.Text.Json and Newtonsoft.Json for Cosmos DB compatibility.
/// </summary>
public class IngestModelSerializationTests
{
    private static readonly JsonSerializerOptions SystemTextJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    #region RecipeSource Tests

    [Fact]
    public void RecipeSource_SystemTextJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = CreateSampleRecipeSource();

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<RecipeSource>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Url, deserialized.Url);
        Assert.Equal(original.UrlHash, deserialized.UrlHash);
        Assert.Equal(original.SiteName, deserialized.SiteName);
        Assert.Equal(original.Author, deserialized.Author);
        Assert.Equal(original.RetrievedAt, deserialized.RetrievedAt);
        Assert.Equal(original.ExtractionMethod, deserialized.ExtractionMethod);
        Assert.Equal(original.LicenseHint, deserialized.LicenseHint);
    }

    [Fact]
    public void RecipeSource_NewtonsoftJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = CreateSampleRecipeSource();

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<RecipeSource>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Url, deserialized.Url);
        Assert.Equal(original.UrlHash, deserialized.UrlHash);
        Assert.Equal(original.SiteName, deserialized.SiteName);
        Assert.Equal(original.Author, deserialized.Author);
        Assert.Equal(original.RetrievedAt, deserialized.RetrievedAt);
        Assert.Equal(original.ExtractionMethod, deserialized.ExtractionMethod);
        Assert.Equal(original.LicenseHint, deserialized.LicenseHint);
    }

    [Fact]
    public void RecipeSource_SystemTextJson_UsesCorrectPropertyNames()
    {
        // Arrange
        var source = CreateSampleRecipeSource();

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(source, SystemTextJsonOptions);

        // Assert
        Assert.Contains("\"url\":", json);
        Assert.Contains("\"urlHash\":", json);
        Assert.Contains("\"siteName\":", json);
        Assert.Contains("\"author\":", json);
        Assert.Contains("\"retrievedAt\":", json);
        Assert.Contains("\"extractionMethod\":", json);
        Assert.Contains("\"licenseHint\":", json);
    }

    #endregion

    #region ValidationReport Tests

    [Fact]
    public void ValidationReport_SystemTextJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ValidationReport
        {
            Errors = ["Missing required field: Name", "Invalid prep time"],
            Warnings = ["Low quality image detected"]
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ValidationReport>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Errors, deserialized.Errors);
        Assert.Equal(original.Warnings, deserialized.Warnings);
        Assert.Equal(original.IsValid, deserialized.IsValid);
    }

    [Fact]
    public void ValidationReport_NewtonsoftJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ValidationReport
        {
            Errors = ["Missing required field: Name"],
            Warnings = ["Low quality image detected", "Author not found"]
        };

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<ValidationReport>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Errors, deserialized.Errors);
        Assert.Equal(original.Warnings, deserialized.Warnings);
    }

    [Fact]
    public void ValidationReport_IsValid_ReturnsTrue_WhenNoErrors()
    {
        // Arrange
        var report = new ValidationReport
        {
            Errors = [],
            Warnings = ["Some warning"]
        };

        // Assert
        Assert.True(report.IsValid);
    }

    [Fact]
    public void ValidationReport_IsValid_ReturnsFalse_WhenHasErrors()
    {
        // Arrange
        var report = new ValidationReport
        {
            Errors = ["Some error"],
            Warnings = []
        };

        // Assert
        Assert.False(report.IsValid);
    }

    #endregion

    #region SimilarityReport Tests

    [Fact]
    public void SimilarityReport_SystemTextJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new SimilarityReport
        {
            MaxContiguousTokenOverlap = 42,
            MaxNgramSimilarity = 0.75,
            ViolatesPolicy = true,
            Details = "High overlap detected in instructions section"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SimilarityReport>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MaxContiguousTokenOverlap, deserialized.MaxContiguousTokenOverlap);
        Assert.Equal(original.MaxNgramSimilarity, deserialized.MaxNgramSimilarity);
        Assert.Equal(original.ViolatesPolicy, deserialized.ViolatesPolicy);
        Assert.Equal(original.Details, deserialized.Details);
    }

    [Fact]
    public void SimilarityReport_NewtonsoftJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new SimilarityReport
        {
            MaxContiguousTokenOverlap = 15,
            MaxNgramSimilarity = 0.25,
            ViolatesPolicy = false,
            Details = null
        };

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<SimilarityReport>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MaxContiguousTokenOverlap, deserialized.MaxContiguousTokenOverlap);
        Assert.Equal(original.MaxNgramSimilarity, deserialized.MaxNgramSimilarity);
        Assert.Equal(original.ViolatesPolicy, deserialized.ViolatesPolicy);
        Assert.Equal(original.Details, deserialized.Details);
    }

    #endregion

    #region ArtifactRef Tests

    [Fact]
    public void ArtifactRef_SystemTextJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ArtifactRef
        {
            Type = "snapshot.txt",
            Uri = "https://storage.blob.core.windows.net/artifacts/task-123/snapshot.txt"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ArtifactRef>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Uri, deserialized.Uri);
    }

    [Fact]
    public void ArtifactRef_NewtonsoftJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ArtifactRef
        {
            Type = "draft.recipe.json",
            Uri = "https://storage.blob.core.windows.net/artifacts/task-456/draft.recipe.json"
        };

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<ArtifactRef>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Uri, deserialized.Uri);
    }

    #endregion

    #region RecipeDraft Tests

    [Fact]
    public void RecipeDraft_SystemTextJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = CreateSampleRecipeDraft();

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<RecipeDraft>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Recipe.Id, deserialized.Recipe.Id);
        Assert.Equal(original.Recipe.Name, deserialized.Recipe.Name);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
        Assert.Equal(original.ValidationReport.Errors, deserialized.ValidationReport.Errors);
        Assert.NotNull(deserialized.SimilarityReport);
        Assert.Equal(original.SimilarityReport!.MaxNgramSimilarity, deserialized.SimilarityReport.MaxNgramSimilarity);
        Assert.Equal(original.Artifacts.Count, deserialized.Artifacts.Count);
    }

    [Fact]
    public void RecipeDraft_NewtonsoftJson_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = CreateSampleRecipeDraft();

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<RecipeDraft>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Recipe.Id, deserialized.Recipe.Id);
        Assert.Equal(original.Recipe.Name, deserialized.Recipe.Name);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
        Assert.Equal(original.ValidationReport.Warnings.Count, deserialized.ValidationReport.Warnings.Count);
        Assert.NotNull(deserialized.SimilarityReport);
        Assert.Equal(original.SimilarityReport!.ViolatesPolicy, deserialized.SimilarityReport.ViolatesPolicy);
    }

    [Fact]
    public void RecipeDraft_SystemTextJson_RoundTrip_WithNullOptionalProperties()
    {
        // Arrange
        var original = new RecipeDraft
        {
            Recipe = CreateSampleRecipe(),
            Source = CreateSampleRecipeSource(),
            ValidationReport = new ValidationReport(),
            SimilarityReport = null,
            Artifacts = []
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<RecipeDraft>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.SimilarityReport);
        Assert.Empty(deserialized.Artifacts);
    }

    #endregion

    #region Recipe with Source Tests

    [Fact]
    public void Recipe_WithSource_SystemTextJson_RoundTrip_PreservesSource()
    {
        // Arrange
        var original = CreateSampleRecipe() with
        {
            Source = CreateSampleRecipeSource()
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Recipe>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Source);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
        Assert.Equal(original.Source.UrlHash, deserialized.Source.UrlHash);
    }

    [Fact]
    public void Recipe_WithSource_NewtonsoftJson_RoundTrip_PreservesSource()
    {
        // Arrange
        var original = CreateSampleRecipe() with
        {
            Source = CreateSampleRecipeSource()
        };

        // Act
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<Recipe>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Source);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
        Assert.Equal(original.Source.ExtractionMethod, deserialized.Source.ExtractionMethod);
    }

    [Fact]
    public void Recipe_WithNullSource_SystemTextJson_RoundTrip_PreservesNull()
    {
        // Arrange
        var original = CreateSampleRecipe() with { Source = null };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Recipe>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Source);
    }

    #endregion

    #region Cross-Serializer Compatibility Tests

    [Fact]
    public void RecipeDraft_SystemTextJson_CanBeDeserializedBy_NewtonsoftJson()
    {
        // Arrange
        var original = CreateSampleRecipeDraft();

        // Act - Serialize with System.Text.Json
        var json = System.Text.Json.JsonSerializer.Serialize(original, SystemTextJsonOptions);
        
        // Deserialize with Newtonsoft
        var deserialized = JsonConvert.DeserializeObject<RecipeDraft>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Recipe.Name, deserialized.Recipe.Name);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
    }

    [Fact]
    public void RecipeDraft_NewtonsoftJson_CanBeDeserializedBy_SystemTextJson()
    {
        // Arrange
        var original = CreateSampleRecipeDraft();

        // Act - Serialize with Newtonsoft
        var json = JsonConvert.SerializeObject(original);
        
        // Deserialize with System.Text.Json
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<RecipeDraft>(json, SystemTextJsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Recipe.Name, deserialized.Recipe.Name);
        Assert.Equal(original.Source.Url, deserialized.Source.Url);
    }

    #endregion

    #region Helper Methods

    private static RecipeSource CreateSampleRecipeSource() => new()
    {
        Url = "https://example.com/recipes/chocolate-cake",
        UrlHash = "abc123def456ghi789jk",
        SiteName = "Example Recipes",
        Author = "Chef John",
        RetrievedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        ExtractionMethod = "JsonLd",
        LicenseHint = "CC BY 4.0"
    };

    private static Recipe CreateSampleRecipe() => new()
    {
        Id = "recipe-001",
        Name = "Chocolate Cake",
        Description = "A delicious chocolate cake recipe",
        Ingredients =
        [
            new Ingredient { Name = "Flour", Quantity = 2, Unit = "cups" },
            new Ingredient { Name = "Sugar", Quantity = 1.5, Unit = "cups" }
        ],
        Instructions = ["Preheat oven to 350°F", "Mix dry ingredients", "Bake for 30 minutes"],
        Cuisine = "American",
        PrepTimeMinutes = 20,
        CookTimeMinutes = 30,
        Servings = 8,
        CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
    };

    private static RecipeDraft CreateSampleRecipeDraft() => new()
    {
        Recipe = CreateSampleRecipe(),
        Source = CreateSampleRecipeSource(),
        ValidationReport = new ValidationReport
        {
            Errors = [],
            Warnings = ["Image URL uses HTTP instead of HTTPS"]
        },
        SimilarityReport = new SimilarityReport
        {
            MaxContiguousTokenOverlap = 10,
            MaxNgramSimilarity = 0.35,
            ViolatesPolicy = false,
            Details = "Within acceptable limits"
        },
        Artifacts =
        [
            new ArtifactRef { Type = "snapshot.txt", Uri = "https://storage.example.com/artifacts/task-001/snapshot.txt" },
            new ArtifactRef { Type = "draft.recipe.json", Uri = "https://storage.example.com/artifacts/task-001/draft.recipe.json" }
        ]
    };

    #endregion
}
