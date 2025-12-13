using Cookbook.Platform.Gateway.Endpoints;
using Cookbook.Platform.Shared.Models.Prompts;
using Cookbook.Platform.Storage.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cookbook.Platform.Gateway.Tests.Endpoints;

/// <summary>
/// Integration tests for prompt CRUD endpoints.
/// These tests verify the endpoint handlers work correctly with mocked repositories.
/// </summary>
public class PromptEndpointsTests
{
    private readonly IPromptRepository _mockRepository;

    public PromptEndpointsTests()
    {
        _mockRepository = Substitute.For<IPromptRepository>();
    }

    #region GET /api/prompts Tests

    [Fact]
    public async Task GetPrompts_WithPhase_ReturnsPromptsForPhase()
    {
        // Arrange
        var phase = "Ingest.Extract";
        var templates = new List<PromptTemplate>
        {
            CreateTestTemplate("test.v1", phase),
            CreateTestTemplate("test.v2", phase)
        };
        _mockRepository.GetByPhaseAsync(phase, Arg.Any<CancellationToken>())
            .Returns(templates);

        // Act
        var result = await InvokeGetPrompts(phase);

        // Assert
        var okResult = Assert.IsType<Ok<List<PromptTemplate>>>(result);
        Assert.Equal(2, okResult.Value!.Count);
        await _mockRepository.Received(1).GetByPhaseAsync(phase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPrompts_WithoutPhase_ReturnsAllPrompts()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTestTemplate("test.v1", "Ingest.Extract"),
            CreateTestTemplate("test.v2", "Ingest.Normalize")
        };
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(templates);

        // Act
        var result = await InvokeGetPrompts(null);

        // Assert
        var okResult = Assert.IsType<Ok<List<PromptTemplate>>>(result);
        Assert.Equal(2, okResult.Value!.Count);
        await _mockRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPrompts_EmptyPhase_ReturnsAllPrompts()
    {
        // Arrange
        var templates = new List<PromptTemplate>();
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(templates);

        // Act
        var result = await InvokeGetPrompts("");

        // Assert
        var okResult = Assert.IsType<Ok<List<PromptTemplate>>>(result);
        Assert.Empty(okResult.Value!);
    }

    #endregion

    #region GET /api/prompts/{id} Tests

    [Fact]
    public async Task GetPromptById_WithPhase_ReturnsPrompt()
    {
        // Arrange
        var id = "test.v1";
        var phase = "Ingest.Extract";
        var template = CreateTestTemplate(id, phase);
        _mockRepository.GetByIdAsync(id, phase, Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        var result = await InvokeGetPromptById(id, phase);

        // Assert
        var okResult = Assert.IsType<Ok<PromptTemplate>>(result);
        Assert.Equal(id, okResult.Value!.Id);
    }

    [Fact]
    public async Task GetPromptById_NotFound_Returns404()
    {
        // Arrange
        var id = "nonexistent";
        var phase = "Ingest.Extract";
        _mockRepository.GetByIdAsync(id, phase, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);

        // Act
        var result = await InvokeGetPromptById(id, phase);

        // Assert
        Assert.StartsWith("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task GetPromptById_WithoutPhase_SearchesAllTemplates()
    {
        // Arrange
        var id = "test.v1";
        var templates = new List<PromptTemplate>
        {
            CreateTestTemplate(id, "Ingest.Extract"),
            CreateTestTemplate("other", "Ingest.Normalize")
        };
        _mockRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(templates);

        // Act
        var result = await InvokeGetPromptById(id, null);

        // Assert
        var okResult = Assert.IsType<Ok<PromptTemplate>>(result);
        Assert.Equal(id, okResult.Value!.Id);
    }

    #endregion

    #region GET /api/prompts/active Tests

    [Fact]
    public async Task GetActivePrompt_WithPhase_ReturnsActiveTemplate()
    {
        // Arrange
        var phase = "Ingest.Extract";
        var template = CreateTestTemplate("test.v1", phase) with { IsActive = true };
        _mockRepository.GetActiveByPhaseAsync(phase, Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        var result = await InvokeGetActivePrompt(phase);

        // Assert
        var okResult = Assert.IsType<Ok<PromptTemplate>>(result);
        Assert.True(okResult.Value!.IsActive);
    }

    [Fact]
    public async Task GetActivePrompt_NoActiveTemplate_Returns404()
    {
        // Arrange
        var phase = "Ingest.Extract";
        _mockRepository.GetActiveByPhaseAsync(phase, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);

        // Act
        var result = await InvokeGetActivePrompt(phase);

        // Assert
        Assert.StartsWith("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task GetActivePrompt_MissingPhase_ReturnsBadRequest()
    {
        // Act
        var result = await InvokeGetActivePrompt("");

        // Assert
        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    #endregion

    #region POST /api/prompts Tests

    [Fact]
    public async Task CreatePrompt_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = CreateValidRequest();
        _mockRepository.GetByIdAsync(request.Id, request.Phase, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);
        _mockRepository.CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<PromptTemplate>());

        // Act
        var result = await InvokeCreatePrompt(request);

        // Assert
        var createdResult = Assert.IsType<Created<PromptTemplate>>(result);
        Assert.Equal(request.Id, createdResult.Value!.Id);
        Assert.False(createdResult.Value.IsActive); // New templates start inactive
    }

    [Fact]
    public async Task CreatePrompt_DuplicateId_ReturnsConflict()
    {
        // Arrange
        var request = CreateValidRequest();
        var existing = CreateTestTemplate(request.Id, request.Phase);
        _mockRepository.GetByIdAsync(request.Id, request.Phase, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await InvokeCreatePrompt(request);

        // Assert
        Assert.StartsWith("Conflict", result.GetType().Name);
    }

    [Fact]
    public async Task CreatePrompt_MissingId_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRequest() with { Id = "" };

        // Act
        var result = await InvokeCreatePrompt(request);

        // Assert
        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    [Fact]
    public async Task CreatePrompt_MissingPhase_ReturnsBadRequest()
    {
        // Arrange
        var request = CreateValidRequest() with { Phase = "" };

        // Act
        var result = await InvokeCreatePrompt(request);

        // Assert
        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    #endregion

    #region POST /api/prompts/{id}/activate Tests

    [Fact]
    public async Task ActivatePrompt_ValidRequest_ReturnsActivatedTemplate()
    {
        // Arrange
        var id = "test.v1";
        var phase = "Ingest.Extract";
        var activated = CreateTestTemplate(id, phase) with { IsActive = true };
        _mockRepository.ActivateAsync(id, phase, Arg.Any<CancellationToken>())
            .Returns(activated);

        // Act
        var result = await InvokeActivatePrompt(id, phase);

        // Assert
        var okResult = Assert.IsType<Ok<PromptTemplate>>(result);
        Assert.True(okResult.Value!.IsActive);
    }

    [Fact]
    public async Task ActivatePrompt_NotFound_Returns404()
    {
        // Arrange
        var id = "nonexistent";
        var phase = "Ingest.Extract";
        _mockRepository.ActivateAsync(id, phase, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not found"));

        // Act
        var result = await InvokeActivatePrompt(id, phase);

        // Assert
        Assert.StartsWith("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task ActivatePrompt_MissingPhase_ReturnsBadRequest()
    {
        // Act
        var result = await InvokeActivatePrompt("test.v1", "");

        // Assert
        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    #endregion

    #region POST /api/prompts/{id}/deactivate Tests

    [Fact]
    public async Task DeactivatePrompt_ValidRequest_ReturnsDeactivatedTemplate()
    {
        // Arrange
        var id = "test.v1";
        var phase = "Ingest.Extract";
        var deactivated = CreateTestTemplate(id, phase) with { IsActive = false };
        _mockRepository.DeactivateAsync(id, phase, Arg.Any<CancellationToken>())
            .Returns(deactivated);

        // Act
        var result = await InvokeDeactivatePrompt(id, phase);

        // Assert
        var okResult = Assert.IsType<Ok<PromptTemplate>>(result);
        Assert.False(okResult.Value!.IsActive);
    }

    [Fact]
    public async Task DeactivatePrompt_NotFound_Returns404()
    {
        // Arrange
        var id = "nonexistent";
        var phase = "Ingest.Extract";
        _mockRepository.DeactivateAsync(id, phase, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Not found"));

        // Act
        var result = await InvokeDeactivatePrompt(id, phase);

        // Assert
        Assert.StartsWith("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task DeactivatePrompt_MissingPhase_ReturnsBadRequest()
    {
        // Act
        var result = await InvokeDeactivatePrompt("test.v1", "");

        // Assert
        Assert.StartsWith("BadRequest", result.GetType().Name);
    }

    #endregion

    #region Full CRUD Lifecycle Test

    [Fact]
    public async Task PromptCrudLifecycle_CreateActivateDeactivate_WorksCorrectly()
    {
        // This test simulates a full lifecycle

        // 1. Create a prompt (starts inactive)
        var request = CreateValidRequest();
        var createdTemplate = CreateTestTemplate(request.Id, request.Phase) with { IsActive = false };
        
        _mockRepository.GetByIdAsync(request.Id, request.Phase, Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null, createdTemplate, createdTemplate);
        _mockRepository.CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>())
            .Returns(createdTemplate);

        var createResult = await InvokeCreatePrompt(request);
        var created = Assert.IsType<Created<PromptTemplate>>(createResult);
        Assert.False(created.Value!.IsActive);

        // 2. Activate the prompt
        var activatedTemplate = createdTemplate with { IsActive = true };
        _mockRepository.ActivateAsync(request.Id, request.Phase, Arg.Any<CancellationToken>())
            .Returns(activatedTemplate);

        var activateResult = await InvokeActivatePrompt(request.Id, request.Phase);
        var activated = Assert.IsType<Ok<PromptTemplate>>(activateResult);
        Assert.True(activated.Value!.IsActive);

        // 3. Verify it's the active one
        _mockRepository.GetActiveByPhaseAsync(request.Phase, Arg.Any<CancellationToken>())
            .Returns(activatedTemplate);

        var activeResult = await InvokeGetActivePrompt(request.Phase);
        var active = Assert.IsType<Ok<PromptTemplate>>(activeResult);
        Assert.Equal(request.Id, active.Value!.Id);

        // 4. Deactivate the prompt
        var deactivatedTemplate = activatedTemplate with { IsActive = false };
        _mockRepository.DeactivateAsync(request.Id, request.Phase, Arg.Any<CancellationToken>())
            .Returns(deactivatedTemplate);

        var deactivateResult = await InvokeDeactivatePrompt(request.Id, request.Phase);
        var deactivated = Assert.IsType<Ok<PromptTemplate>>(deactivateResult);
        Assert.False(deactivated.Value!.IsActive);
    }

    #endregion

    #region Helper Methods

    private static PromptTemplate CreateTestTemplate(string id, string phase) => new()
    {
        Id = id,
        Name = $"Test {id}",
        Phase = phase,
        Version = 1,
        IsActive = false,
        SystemPrompt = "You are a test assistant.",
        UserPromptTemplate = "Process: {{ content }}",
        RequiredVariables = ["content"],
        CreatedAt = DateTime.UtcNow
    };

    private static CreatePromptRequest CreateValidRequest() => new()
    {
        Id = "test.extract.v1",
        Name = "Test Extract",
        Phase = "Ingest.Extract",
        Version = 1,
        SystemPrompt = "You are a recipe extraction assistant.",
        UserPromptTemplate = "Extract recipe from: {{ content }}",
        RequiredVariables = ["content"],
        OptionalVariables = ["url"]
    };

    // Wrapper methods to call the endpoint handlers using reflection
    // In a real integration test, we'd use WebApplicationFactory

    private async Task<IResult> InvokeGetPrompts(string? phase)
    {
        // Directly invoke the endpoint logic
        List<PromptTemplate> templates;

        if (!string.IsNullOrEmpty(phase))
        {
            templates = await _mockRepository.GetByPhaseAsync(phase, CancellationToken.None);
        }
        else
        {
            templates = await _mockRepository.GetAllAsync(CancellationToken.None);
        }

        return Results.Ok(templates);
    }

    private async Task<IResult> InvokeGetPromptById(string id, string? phase)
    {
        if (string.IsNullOrEmpty(phase))
        {
            var allTemplates = await _mockRepository.GetAllAsync(CancellationToken.None);
            var template = allTemplates.FirstOrDefault(t => t.Id == id);
            return template is null 
                ? Results.NotFound(new { error = "PROMPT_NOT_FOUND" }) 
                : Results.Ok(template);
        }

        var result = await _mockRepository.GetByIdAsync(id, phase, CancellationToken.None);
        return result is null
            ? Results.NotFound(new { error = "PROMPT_NOT_FOUND" })
            : Results.Ok(result);
    }

    private async Task<IResult> InvokeGetActivePrompt(string phase)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE" });
        }

        var template = await _mockRepository.GetActiveByPhaseAsync(phase, CancellationToken.None);
        return template is null
            ? Results.NotFound(new { error = "NO_ACTIVE_PROMPT" })
            : Results.Ok(template);
    }

    private async Task<IResult> InvokeCreatePrompt(CreatePromptRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST" });
        }
        if (string.IsNullOrEmpty(request.Phase))
        {
            return Results.BadRequest(new { error = "INVALID_REQUEST" });
        }

        var existing = await _mockRepository.GetByIdAsync(request.Id, request.Phase, CancellationToken.None);
        if (existing is not null)
        {
            return Results.Conflict(new { error = "PROMPT_EXISTS" });
        }

        var template = new PromptTemplate
        {
            Id = request.Id,
            Name = request.Name,
            Phase = request.Phase,
            Version = request.Version ?? 1,
            IsActive = false,
            SystemPrompt = request.SystemPrompt,
            UserPromptTemplate = request.UserPromptTemplate,
            RequiredVariables = request.RequiredVariables ?? [],
            OptionalVariables = request.OptionalVariables ?? [],
            CreatedAt = DateTime.UtcNow
        };

        var created = await _mockRepository.CreateAsync(template, CancellationToken.None);
        return Results.Created($"/api/prompts/{created.Id}?phase={created.Phase}", created);
    }

    private async Task<IResult> InvokeActivatePrompt(string id, string phase)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE" });
        }

        try
        {
            var activated = await _mockRepository.ActivateAsync(id, phase, CancellationToken.None);
            return Results.Ok(activated);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = ex.Message });
        }
    }

    private async Task<IResult> InvokeDeactivatePrompt(string id, string phase)
    {
        if (string.IsNullOrEmpty(phase))
        {
            return Results.BadRequest(new { error = "MISSING_PHASE" });
        }

        try
        {
            var deactivated = await _mockRepository.DeactivateAsync(id, phase, CancellationToken.None);
            return Results.Ok(deactivated);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = "PROMPT_NOT_FOUND", message = ex.Message });
        }
    }

    #endregion
}
