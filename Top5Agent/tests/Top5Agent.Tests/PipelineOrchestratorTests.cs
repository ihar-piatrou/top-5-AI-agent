using FluentAssertions;
using Hangfire;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline;
using Xunit;

namespace Top5Agent.Tests;

public class PipelineOrchestratorTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ILlmClient> _gptMock;
    private readonly Mock<IEmbeddingClient> _embeddingMock;
    private readonly Mock<IBackgroundJobClient> _jobClientMock;

    public PipelineOrchestratorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _gptMock = new Mock<ILlmClient>();
        _embeddingMock = new Mock<IEmbeddingClient>();
        _jobClientMock = new Mock<IBackgroundJobClient>();
    }

    [Fact]
    public async Task RunAsync_CreatesAPipelineRun_AndReturnsRunId()
    {
        SetupGptToReturnIdeas();
        SetupEmbeddingsToReturnZeroVector();

        var orchestrator = CreateOrchestrator();
        var runId = await orchestrator.RunAsync("cars", autoApprove: false);

        runId.Should().NotBeEmpty();

        var run = await _db.PipelineRuns.FindAsync(runId);
        run.Should().NotBeNull();
        run!.Status.Should().Be(PipelineRunStatus.Completed);
    }

    [Fact]
    public async Task RunAsync_WithAutoApprove_EnqueuesProcessIdeaJobs()
    {
        SetupGptToReturnIdeas();
        SetupEmbeddingsToReturnZeroVector();

        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync("cars", autoApprove: true);

        // Should have enqueued jobs for approved ideas
        _jobClientMock.Verify(
            j => j.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<IState>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task RunAsync_WithoutAutoApprove_IdeasRemainPending()
    {
        SetupGptToReturnIdeas();
        SetupEmbeddingsToReturnZeroVector();

        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync("health", autoApprove: false);

        var ideas = _db.Ideas.ToList();
        ideas.Should().NotBeEmpty();
        ideas.All(i => i.Status == IdeaStatus.Pending).Should().BeTrue();
    }

    private void SetupGptToReturnIdeas()
    {
        var ideasJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new IdeaJson { Title = "5 Car Mistakes", Niche = "cars", Summary = "Costly mistakes drivers make." },
            new IdeaJson { Title = "5 Engine Tips", Niche = "cars", Summary = "Engine care secrets." }
        });

        _gptMock
            .Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ideasJson);
    }

    private void SetupEmbeddingsToReturnZeroVector()
    {
        _embeddingMock
            .Setup(c => c.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
    }

    private PipelineOrchestrator CreateOrchestrator()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pipeline:DuplicateThreshold"] = "0.85"
            })
            .Build();

        var ideaGenerator = new IdeaGeneratorService(
            _gptMock.Object, _db, NullLogger<IdeaGeneratorService>.Instance);

        var duplicateDetector = new DuplicateDetectorService(
            _embeddingMock.Object, _db, config, NullLogger<DuplicateDetectorService>.Instance);

        return new PipelineOrchestrator(
            ideaGenerator, duplicateDetector, _db, _jobClientMock.Object,
            NullLogger<PipelineOrchestrator>.Instance);
    }

    public void Dispose() => _db.Dispose();
}
