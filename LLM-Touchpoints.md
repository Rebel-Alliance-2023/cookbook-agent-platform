# LLM Touchpoints via MCP Tools

This document catalogs all code locations that invoke an LLM in the Cookbook Agent Platform. All LLM calls are routed through `ILlmRouter.ChatAsync`, which serves as the single entry point for LLM interactions.

## Overview

The solution uses a centralized LLM router (`ILlmRouter`) that abstracts provider-specific implementations (OpenAI, Anthropic/Claude). All agent services inject this router and invoke it as needed during their execution phases.

## LLM Call Inventory

| Phase            | Method                                       | File Path                                                                       | Provider | Purpose                                                             |
|------------------|----------------------------------------------|---------------------------------------------------------------------------------|----------|---------------------------------------------------------------------|
| Research          | `ResearchAgentServer.ScoreRecipeAsync`      | `src/Cookbook.Platform.A2A.Research/Services/ResearchAgentServer.cs`          | Claude   | Score recipe relevance on a 0.0-1.0 scale                          |
| Research          | `ResearchAgentServer.GenerateNotesAsync`    | `src/Cookbook.Platform.A2A.Research/Services/ResearchAgentServer.cs`          | Claude   | Generate research notes summarizing search results                   |
| Analyze           | `AnalysisAgentServer.GenerateShoppingListAsync`| `src/Cookbook.Platform.A2A.Analysis/Services/AnalysisAgentServer.cs`        | OpenAI   | Generate categorized shopping list JSON from recipe ingredients      |
| Ingest (Repair)  | `RepairParaphraseService.CallLlmForRephraseAsync`| `src/Cookbook.Platform.Orchestrator/Services/Ingest/RepairParaphraseService.cs`| OpenAI   | Paraphrase high-similarity content to reduce verbatim overlap       |

## Notes

- **No other LLM invocations exist** in the codebase outside these four touchpoints
- All calls use `ILlmRouter.ChatAsync` as the unified entry point
- Provider selection is explicit per use case (Claude for research, OpenAI for structured tasks)
- The LLM router handles retry logic, fallback, and provider-specific API differences

