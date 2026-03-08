# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

All commands run from `Top5Agent/` unless noted.

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run the API
dotnet run --project src/Top5Agent.Api

# Run all tests
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~PipelineOrchestratorTests.RunAsync_CreatesAPipelineRun"

# Add EF migration (run from solution root Top5Agent/)
dotnet ef migrations add <MigrationName> --project src/Top5Agent.Infrastructure --startup-project src/Top5Agent.Api

# Apply migrations
dotnet ef database update --project src/Top5Agent.Infrastructure --startup-project src/Top5Agent.Api
```

**API keys — use user secrets (preferred over appsettings.json):**
```bash
cd Top5Agent
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/Top5Agent.Api
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project src/Top5Agent.Api
dotnet user-secrets set "Pexels:ApiKey" "..." --project src/Top5Agent.Api
```

---

## Runtime & Stack

- **.NET 8** (not 9)
- ASP.NET Core Minimal API
- EF Core 8 + SQL Server (`localhost\SQLEXPRESS2019` by default)
- Hangfire 1.8.14 with MSSQL storage
- OpenAI SDK `2.1.0` — `new OpenAIClient(new ApiKeyCredential(key))`
- **No Anthropic SDK** — `ClaudeClient` calls the REST API directly via `HttpClient`
- Swagger only in Development environment

---

## Project Structure

```
Top5Agent/
├── src/
│   ├── Top5Agent.Core/          # Interfaces, models, DTOs — no EF or external refs
│   ├── Top5Agent.Infrastructure/ # EF DbContext, GptClient, ClaudeClient, PexelsMediaProvider
│   ├── Top5Agent.Pipeline/      # All pipeline services, Hangfire jobs, Prompts.cs
│   └── Top5Agent.Api/           # Program.cs (DI root), minimal API endpoints
└── tests/
    └── Top5Agent.Tests/         # xUnit + Moq + FluentAssertions + EF InMemory
```

---

## Architecture

### Pipeline flow (end-to-end)

```
POST /api/pipeline/run
  └── PipelineOrchestrator.RunAsync(niche, count)
        ├── IdeaGeneratorService     → GPT-4o generates idea titles
        ├── DuplicateDetectorService → cosine similarity vs stored embeddings (threshold 0.85)
        └── [if autoApprove] enqueues ProcessIdeaJob per idea

PATCH /api/ideas/{id}/status → "approved"
  └── ProcessIdeaJob
        ├── ScriptWriterService   → Claude writes full script JSON
        ├── FactReviewerService   → GPT-4o fact-checks all verify_claims[]
        └── ContentPolisherService → Claude rewrites for spoken clarity

PATCH /api/scripts/{id}/status → "approved"
  └── DownloadMediaJob
        └── MediaAcquisitionService → downloads photos/videos from Pexels per section
```

### LLM responsibility split

| Service | LLM |
|---|---|
| `IdeaGeneratorService` | GPT-4o (`GptClient`) |
| `FactReviewerService` | GPT-4o (`GptClient`) |
| `ScriptWriterService` | Claude Sonnet 4.6 (`ClaudeClient`) |
| `ContentPolisherService` | Claude Sonnet 4.6 (`ClaudeClient`) |
| `DuplicateDetectorService` | OpenAI `text-embedding-3-small` (`OpenAiEmbeddingClient`) |

Services do **not** receive `ILlmClient` from DI by interface — they are wired explicitly via factory lambdas in `Program.cs` to ensure the correct LLM is used per service.

### ClaudeClient

No Anthropic SDK. Raw `HttpClient` registered as typed client in DI with headers:
- `x-api-key`
- `anthropic-version: 2023-06-01`

Model: `claude-sonnet-4-6`

### Deduplication

`ideas.embedding` stores a JSON-serialized `float[1536]` vector. Cosine similarity is computed in C# against all stored embeddings. Ideas scoring `>= 0.85` against any existing title are rejected.

### Idempotency (Hangfire retries)

- `ScriptWriterService.WriteAsync` — skips if a non-draft script already exists for the idea
- `FactReviewerService.ReviewAsync` — skips if a review already exists for the script
- Sources are deduplicated by URL before saving

---

## Key Files

| File | Purpose |
|---|---|
| `src/Top5Agent.Api/Program.cs` | DI wiring, Hangfire setup, middleware, recurring jobs |
| `src/Top5Agent.Pipeline/Prompts.cs` | All LLM prompts in one static class |
| `src/Top5Agent.Infrastructure/Data/AppDbContext.cs` | EF schema — all table/column config |
| `src/Top5Agent.Pipeline/PipelineOrchestrator.cs` | Top-level pipeline coordination |
| `src/Top5Agent.Api/Endpoints/PipelineEndpoints.cs` | `Niche` enum + `ToNicheString()` extension |

---

## Prompts

All prompts live in `Top5Agent.Pipeline/Prompts.cs` as a single static class.

- Non-parameterized prompts: `static readonly string` with `"""`
- Parameterized prompts: `static string` methods returning `$$"""` (double-dollar raw strings)
  - `{{expr}}` = interpolation hole
  - `{` / `}` = literal JSON braces (no escaping needed in `$$"""`)

---

## DB

See `Top5Agent/DB_SCHEMA.md` for full schema and relationship diagram.

Key facts:
- All PKs: `UNIQUEIDENTIFIER` with `NEWSEQUENTIALID()`
- All timestamps: `GETUTCDATE()` (UTC)
- Migrations output to `src/Top5Agent.Infrastructure/Data/Migrations/`
- Migrations auto-apply on startup via `db.Database.Migrate()`

---

## SQL Style

Format SQL queries like this (used in project docs and queries):

```sql
SELECT
    s.id AS script_id,
    s.status AS script_status
FROM scripts AS s
INNER JOIN ideas AS i
    ON i.id = s.idea_id
WHERE s.status != 'polished'
ORDER BY s.created_at DESC;
```

Rules: `AS` on all aliases, `ON` indented on its own line, subquery parens on their own lines, no tabs.
