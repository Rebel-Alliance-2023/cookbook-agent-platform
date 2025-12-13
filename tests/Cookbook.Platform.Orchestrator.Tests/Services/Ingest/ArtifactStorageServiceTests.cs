using System.Text;
using System.Text.Json;
using Cookbook.Platform.Orchestrator.Services.Ingest;
using Cookbook.Platform.Shared.Models;
using Cookbook.Platform.Shared.Models.Ingest;
using Cookbook.Platform.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cookbook.Platform.Orchestrator.Tests.Services.Ingest;

/// <summary>
/// Unit tests for BlobArtifactStorageService.
/// </summary>
public class BlobArtifactStorageServiceTests
{
    private readonly Mock<IBlobStorage> _blobStorageMock;
    private readonly BlobArtifactStorageService _service;

    public BlobArtifactStorageServiceTests()
    {
        _blobStorageMock = new Mock<IBlobStorage>();
        var loggerMock = new Mock<ILogger<BlobArtifactStorageService>>();
        _service = new BlobArtifactStorageService(_blobStorageMock.Object, loggerMock.Object);
    }

    #region StoreRawHtmlAsync Tests

    [Fact]
    public async Task StoreRawHtmlAsync_StoresContentAtCorrectPath()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var content = "<html><body>Test</body></html>";
        var expectedUri = "https://storage.blob/artifacts/thread-123/task-456/fetch/raw.html";

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.Is<string>(p => p.Contains("thread-123") && p.Contains("task-456") && p.Contains("raw.html")),
                It.IsAny<byte[]>(),
                "text/html",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUri);

        var result = await _service.StoreRawHtmlAsync(threadId, taskId, content);

        Assert.Equal(ArtifactTypes.RawHtml, result.Type);
        Assert.Equal(expectedUri, result.Uri);
    }

    [Fact]
    public async Task StoreRawHtmlAsync_EncodesContentAsUtf8()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var content = "<html>Test with émojis ??</html>";
        byte[]? capturedBytes = null;

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], string?, CancellationToken>((_, bytes, _, _) => capturedBytes = bytes)
            .ReturnsAsync("https://storage.blob/test");

        await _service.StoreRawHtmlAsync(threadId, taskId, content);

        Assert.NotNull(capturedBytes);
        Assert.Equal(content, Encoding.UTF8.GetString(capturedBytes));
    }

    #endregion

    #region StoreSanitizedContentAsync Tests

    [Fact]
    public async Task StoreSanitizedContentAsync_StoresTextContent()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var content = new SanitizedContent
        {
            TextContent = "Sanitized recipe content",
            Metadata = new PageMetadata { Title = "Test Recipe" }
        };

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.Is<string>(p => p.Contains("sanitized.txt")),
                It.IsAny<byte[]>(),
                "text/plain",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/sanitized.txt");

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.Is<string>(p => p.Contains("page.meta.json")),
                It.IsAny<byte[]>(),
                "application/json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/page.meta.json");

        var result = await _service.StoreSanitizedContentAsync(threadId, taskId, content);

        Assert.Equal(ArtifactTypes.SanitizedText, result.Type);
        
        // Verify both text and metadata were stored
        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("sanitized.txt")),
            It.IsAny<byte[]>(),
            "text/plain",
            It.IsAny<CancellationToken>()), Times.Once);

        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("page.meta.json")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region StoreJsonLdAsync Tests

    [Fact]
    public async Task StoreJsonLdAsync_StoresWithCorrectContentType()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var jsonLd = """{"@type": "Recipe", "name": "Test"}""";

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                "application/ld+json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/recipe.jsonld");

        var result = await _service.StoreJsonLdAsync(threadId, taskId, jsonLd);

        Assert.Equal(ArtifactTypes.JsonLd, result.Type);
        
        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("recipe.jsonld")),
            It.IsAny<byte[]>(),
            "application/ld+json",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region StoreExtractionResultAsync Tests

    [Fact]
    public async Task StoreExtractionResultAsync_StoresResultAndRecipe()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var result = new RecipeExtractionResult
        {
            Success = true,
            Recipe = new Recipe { Id = "recipe-1", Name = "Test Recipe" },
            Method = ExtractionMethod.JsonLd
        };

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                "application/json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/test");

        var artifactRef = await _service.StoreExtractionResultAsync(threadId, taskId, result);

        Assert.Equal(ArtifactTypes.ExtractionResult, artifactRef.Type);

        // Verify both extraction result and recipe were stored
        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("extraction.json")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Once);

        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("recipe.json")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreExtractionResultAsync_FailedResult_DoesNotStoreRecipe()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var result = new RecipeExtractionResult
        {
            Success = false,
            Error = "Extraction failed",
            Method = ExtractionMethod.Llm
        };

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                "application/json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/test");

        await _service.StoreExtractionResultAsync(threadId, taskId, result);

        // Only extraction result should be stored, not recipe
        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("extraction.json")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Once);

        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("recipe.json") && !p.Contains("extraction")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region StoreValidationReportAsync Tests

    [Fact]
    public async Task StoreValidationReportAsync_StoresReport()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var report = new ValidationReport
        {
            Errors = [],
            Warnings = ["Missing description"]
        };

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                "application/json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/validation.json");

        var result = await _service.StoreValidationReportAsync(threadId, taskId, report);

        Assert.Equal(ArtifactTypes.ValidationReport, result.Type);
        
        _blobStorageMock.Verify(x => x.UploadAsync(
            It.Is<string>(p => p.Contains("validation.json")),
            It.IsAny<byte[]>(),
            "application/json",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Generic Store Tests

    [Fact]
    public async Task StoreAsync_String_StoresWithCorrectContentType()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var content = "Test content";
        var contentType = "text/plain";

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                contentType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/test");

        var result = await _service.StoreAsync(threadId, taskId, "custom.txt", content, contentType);

        Assert.Equal("custom.txt", result.Type);
    }

    [Fact]
    public async Task StoreAsync_Bytes_StoresDirectly()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var contentType = "application/octet-stream";

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                content,
                contentType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.blob/test");

        var result = await _service.StoreAsync(threadId, taskId, "binary.bin", content, contentType);

        Assert.Equal("binary.bin", result.Type);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ReturnsContentAsString()
    {
        var path = "thread-123/task-456/test.txt";
        var content = "Test content";
        var bytes = Encoding.UTF8.GetBytes(content);

        _blobStorageMock
            .Setup(x => x.DownloadAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var result = await _service.GetAsync(path);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var path = "thread-123/task-456/nonexistent.txt";

        _blobStorageMock
            .Setup(x => x.DownloadAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await _service.GetAsync(path);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBytesAsync_ReturnsBytes()
    {
        var path = "thread-123/task-456/test.bin";
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        _blobStorageMock
            .Setup(x => x.DownloadAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var result = await _service.GetBytesAsync(path);

        Assert.Equal(bytes, result);
    }

    #endregion

    #region ListAsync Tests

    [Fact]
    public async Task ListAsync_ReturnsArtifactRefs()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        var blobs = new List<string>
        {
            "thread-123/task-456/fetch/raw.html",
            "thread-123/task-456/sanitize/sanitized.txt",
            "thread-123/task-456/extract/recipe.json"
        };

        _blobStorageMock
            .Setup(x => x.ListAsync($"{threadId}/{taskId}/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(blobs);

        var result = await _service.ListAsync(threadId, taskId);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Type == "raw.html");
        Assert.Contains(result, r => r.Type == "sanitized.txt");
        Assert.Contains(result, r => r.Type == "recipe.json");
    }

    [Fact]
    public async Task ListAsync_EmptyResult_ReturnsEmptyList()
    {
        var threadId = "thread-123";
        var taskId = "task-456";

        _blobStorageMock
            .Setup(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var result = await _service.ListAsync(threadId, taskId);

        Assert.Empty(result);
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_Exists_ReturnsTrue()
    {
        var path = "thread-123/task-456/test.txt";

        _blobStorageMock
            .Setup(x => x.ExistsAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ExistsAsync(path);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NotExists_ReturnsFalse()
    {
        var path = "thread-123/task-456/nonexistent.txt";

        _blobStorageMock
            .Setup(x => x.ExistsAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ExistsAsync(path);

        Assert.False(result);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_CallsBlobStorage()
    {
        var path = "thread-123/task-456/test.txt";

        await _service.DeleteAsync(path);

        _blobStorageMock.Verify(x => x.DeleteAsync(path, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Path Building Tests

    [Fact]
    public async Task StoreAsync_SanitizesPathSegments()
    {
        var threadId = "thread:123";  // Contains colon
        var taskId = "task*456";       // Contains asterisk
        var content = "Test";
        string? capturedPath = null;

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], string?, CancellationToken>((path, _, _, _) => capturedPath = path)
            .ReturnsAsync("https://storage.blob/test");

        await _service.StoreAsync(threadId, taskId, "test.txt", content, "text/plain");

        Assert.NotNull(capturedPath);
        Assert.DoesNotContain(":", capturedPath);
        Assert.DoesNotContain("*", capturedPath);
        Assert.Contains("thread-123", capturedPath);
        Assert.Contains("task-456", capturedPath);
    }

    [Fact]
    public async Task StoreRawHtmlAsync_IncludesPhaseInPath()
    {
        var threadId = "thread-123";
        var taskId = "task-456";
        string? capturedPath = null;

        _blobStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], string?, CancellationToken>((path, _, _, _) => capturedPath = path)
            .ReturnsAsync("https://storage.blob/test");

        await _service.StoreRawHtmlAsync(threadId, taskId, "<html></html>");

        Assert.NotNull(capturedPath);
        Assert.Contains("fetch", capturedPath);
        Assert.Equal($"{threadId}/{taskId}/fetch/{ArtifactTypes.RawHtml}", capturedPath);
    }

    #endregion
}

/// <summary>
/// Tests for ArtifactTypes constants.
/// </summary>
public class ArtifactTypesTests
{
    [Fact]
    public void ArtifactTypes_HasExpectedValues()
    {
        Assert.Equal("raw.html", ArtifactTypes.RawHtml);
        Assert.Equal("sanitized.txt", ArtifactTypes.SanitizedText);
        Assert.Equal("page.meta.json", ArtifactTypes.PageMetadata);
        Assert.Equal("recipe.jsonld", ArtifactTypes.JsonLd);
        Assert.Equal("extraction.json", ArtifactTypes.ExtractionResult);
        Assert.Equal("recipe.json", ArtifactTypes.Recipe);
        Assert.Equal("validation.json", ArtifactTypes.ValidationReport);
        Assert.Equal("draft.json", ArtifactTypes.Draft);
    }
}

/// <summary>
/// Tests for ArtifactRef record.
/// </summary>
public class ArtifactRefTests
{
    [Fact]
    public void ArtifactRef_RequiresTypeAndUri()
    {
        var artifactRef = new ArtifactRef
        {
            Type = "test.txt",
            Uri = "https://storage.blob/test.txt"
        };

        Assert.Equal("test.txt", artifactRef.Type);
        Assert.Equal("https://storage.blob/test.txt", artifactRef.Uri);
    }
}
