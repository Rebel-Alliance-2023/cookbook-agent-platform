using System.Text.Json;
using Cookbook.Platform.Shared.Models.Ingest;
using Xunit;

namespace Cookbook.Platform.Shared.Tests.Models.Ingest;

/// <summary>
/// Unit tests for IngestPayload serialization and deserialization.
/// </summary>
public class IngestPayloadSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    #region IngestMode Tests

    [Theory]
    [InlineData(IngestMode.Url, "\"Url\"")]
    [InlineData(IngestMode.Query, "\"Query\"")]
    [InlineData(IngestMode.Normalize, "\"Normalize\"")]
    public void IngestMode_Serializes_AsString(IngestMode mode, string expected)
    {
        var json = JsonSerializer.Serialize(mode, JsonOptions);
        Assert.Equal(expected, json);
    }

    [Theory]
    [InlineData("\"Url\"", IngestMode.Url)]
    [InlineData("\"Query\"", IngestMode.Query)]
    [InlineData("\"Normalize\"", IngestMode.Normalize)]
    [InlineData("\"url\"", IngestMode.Url)]
    [InlineData("\"query\"", IngestMode.Query)]
    public void IngestMode_Deserializes_FromString(string json, IngestMode expected)
    {
        var result = JsonSerializer.Deserialize<IngestMode>(json, JsonOptions);
        Assert.Equal(expected, result);
    }

    #endregion

    #region PromptSelection Tests

    [Fact]
    public void PromptSelection_Serializes_AllNulls()
    {
        var selection = new PromptSelection();
        var json = JsonSerializer.Serialize(selection, JsonOptions);
        
        Assert.Contains("\"discoverPromptId\":null", json);
        Assert.Contains("\"extractPromptId\":null", json);
        Assert.Contains("\"normalizePromptId\":null", json);
    }

    [Fact]
    public void PromptSelection_Serializes_WithValues()
    {
        var selection = new PromptSelection
        {
            DiscoverPromptId = "discover.v1",
            ExtractPromptId = "ingest.extract.v1",
            NormalizePromptId = "ingest.normalize.v1"
        };
        
        var json = JsonSerializer.Serialize(selection, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PromptSelection>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal("discover.v1", deserialized.DiscoverPromptId);
        Assert.Equal("ingest.extract.v1", deserialized.ExtractPromptId);
        Assert.Equal("ingest.normalize.v1", deserialized.NormalizePromptId);
    }

    #endregion

    #region PromptOverrides Tests

    [Fact]
    public void PromptOverrides_RoundTrips()
    {
        var overrides = new PromptOverrides
        {
            ExtractOverride = "Custom extract prompt for testing"
        };
        
        var json = JsonSerializer.Serialize(overrides, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PromptOverrides>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal("Custom extract prompt for testing", deserialized.ExtractOverride);
        Assert.Null(deserialized.DiscoverOverride);
        Assert.Null(deserialized.NormalizeOverride);
    }

    #endregion

    #region IngestConstraints Tests

    [Fact]
    public void IngestConstraints_Serializes_WithAllFields()
    {
        var constraints = new IngestConstraints
        {
            DietType = "vegetarian",
            Cuisine = "Japanese",
            MaxPrepMinutes = 30
        };
        
        var json = JsonSerializer.Serialize(constraints, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestConstraints>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal("vegetarian", deserialized.DietType);
        Assert.Equal("Japanese", deserialized.Cuisine);
        Assert.Equal(30, deserialized.MaxPrepMinutes);
    }

    [Fact]
    public void IngestConstraints_Serializes_WithNullFields()
    {
        var constraints = new IngestConstraints
        {
            Cuisine = "Italian"
        };
        
        var json = JsonSerializer.Serialize(constraints, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestConstraints>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.DietType);
        Assert.Equal("Italian", deserialized.Cuisine);
        Assert.Null(deserialized.MaxPrepMinutes);
    }

    #endregion

    #region IngestPayload Tests - URL Mode

    [Fact]
    public void IngestPayload_UrlMode_Serializes_Correctly()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipes/test-recipe",
            PromptSelection = new PromptSelection
            {
                ExtractPromptId = "ingest.extract.v1"
            }
        };
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        
        Assert.Contains("\"mode\":\"Url\"", json);
        Assert.Contains("\"url\":\"https://example.com/recipes/test-recipe\"", json);
    }

    [Fact]
    public void IngestPayload_UrlMode_RoundTrips()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipes/tonkotsu-ramen",
            PromptSelection = new PromptSelection
            {
                ExtractPromptId = "ingest.extract.v1",
                NormalizePromptId = "ingest.normalize.v1"
            }
        };
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal(IngestMode.Url, deserialized.Mode);
        Assert.Equal("https://example.com/recipes/tonkotsu-ramen", deserialized.Url);
        Assert.Null(deserialized.Query);
        Assert.Null(deserialized.RecipeId);
        Assert.NotNull(deserialized.PromptSelection);
        Assert.Equal("ingest.extract.v1", deserialized.PromptSelection.ExtractPromptId);
    }

    #endregion

    #region IngestPayload Tests - Query Mode

    [Fact]
    public void IngestPayload_QueryMode_Serializes_Correctly()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "best tonkotsu ramen recipe",
            Constraints = new IngestConstraints
            {
                Cuisine = "Japanese",
                MaxPrepMinutes = 60
            }
        };
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        
        Assert.Contains("\"mode\":\"Query\"", json);
        Assert.Contains("\"query\":\"best tonkotsu ramen recipe\"", json);
        Assert.Contains("\"cuisine\":\"Japanese\"", json);
    }

    [Fact]
    public void IngestPayload_QueryMode_RoundTrips()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "healthy vegetarian pasta",
            Constraints = new IngestConstraints
            {
                DietType = "vegetarian",
                Cuisine = "Italian"
            },
            PromptSelection = new PromptSelection
            {
                DiscoverPromptId = "ingest.discover.v1",
                ExtractPromptId = "ingest.extract.v1"
            }
        };
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal(IngestMode.Query, deserialized.Mode);
        Assert.Equal("healthy vegetarian pasta", deserialized.Query);
        Assert.NotNull(deserialized.Constraints);
        Assert.Equal("vegetarian", deserialized.Constraints.DietType);
        Assert.Equal("Italian", deserialized.Constraints.Cuisine);
    }

    #endregion

    #region IngestPayload Tests - Normalize Mode

    [Fact]
    public void IngestPayload_NormalizeMode_RoundTrips()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Normalize,
            RecipeId = "recipe-123-abc",
            PromptSelection = new PromptSelection
            {
                NormalizePromptId = "ingest.normalize.v2"
            }
        };
        
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IngestPayload>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Equal(IngestMode.Normalize, deserialized.Mode);
        Assert.Equal("recipe-123-abc", deserialized.RecipeId);
        Assert.Null(deserialized.Url);
        Assert.Null(deserialized.Query);
    }

    #endregion

    #region CreateIngestTaskRequest/Response Tests

    [Fact]
    public void CreateIngestTaskRequest_Serializes_Correctly()
    {
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = "thread-123",
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };
        
        var json = JsonSerializer.Serialize(request, JsonOptions);
        
        Assert.Contains("\"agentType\":\"Ingest\"", json);
        Assert.Contains("\"threadId\":\"thread-123\"", json);
        Assert.Contains("\"payload\":{", json);
    }

    [Fact]
    public void CreateIngestTaskRequest_WithNullThreadId_Serializes()
    {
        var request = new CreateIngestTaskRequest
        {
            AgentType = "Ingest",
            ThreadId = null,
            Payload = new IngestPayload
            {
                Mode = IngestMode.Url,
                Url = "https://example.com/recipe"
            }
        };
        
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CreateIngestTaskRequest>(json, JsonOptions);
        
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.ThreadId);
    }

    [Fact]
    public void CreateIngestTaskResponse_Serializes_Correctly()
    {
        var response = new CreateIngestTaskResponse
        {
            TaskId = "task-abc-123",
            ThreadId = "thread-456",
            AgentType = "Ingest",
            Status = "Pending"
        };
        
        var json = JsonSerializer.Serialize(response, JsonOptions);
        
        Assert.Contains("\"taskId\":\"task-abc-123\"", json);
        Assert.Contains("\"threadId\":\"thread-456\"", json);
        Assert.Contains("\"agentType\":\"Ingest\"", json);
        Assert.Contains("\"status\":\"Pending\"", json);
    }

    #endregion

    #region Newtonsoft.Json Compatibility Tests

    [Fact]
    public void IngestPayload_NewtonsoftCompatibility_RoundTrips()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Url,
            Url = "https://example.com/recipe",
            PromptSelection = new PromptSelection
            {
                ExtractPromptId = "test.prompt"
            }
        };
        
        // Serialize with System.Text.Json
        var systemTextJson = JsonSerializer.Serialize(payload, JsonOptions);
        
        // Deserialize with Newtonsoft
        var newtonsoftResult = Newtonsoft.Json.JsonConvert.DeserializeObject<IngestPayload>(systemTextJson);
        
        Assert.NotNull(newtonsoftResult);
        Assert.Equal(IngestMode.Url, newtonsoftResult.Mode);
        Assert.Equal("https://example.com/recipe", newtonsoftResult.Url);
    }

    [Fact]
    public void IngestPayload_NewtonsoftSerialization_RoundTrips()
    {
        var payload = new IngestPayload
        {
            Mode = IngestMode.Query,
            Query = "test query",
            Constraints = new IngestConstraints { Cuisine = "Mexican" }
        };
        
        // Serialize with Newtonsoft
        var newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        
        // Deserialize with System.Text.Json
        var systemTextResult = JsonSerializer.Deserialize<IngestPayload>(newtonsoftJson, JsonOptions);
        
        Assert.NotNull(systemTextResult);
        Assert.Equal(IngestMode.Query, systemTextResult.Mode);
        Assert.Equal("test query", systemTextResult.Query);
    }

    #endregion
}
