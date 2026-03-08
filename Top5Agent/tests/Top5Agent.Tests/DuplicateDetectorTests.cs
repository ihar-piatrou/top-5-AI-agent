using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline;
using Xunit;

namespace Top5Agent.Tests;

public class DuplicateDetectorTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmbeddingClient> _embeddingClientMock;
    private readonly IConfiguration _config;

    public DuplicateDetectorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _embeddingClientMock = new Mock<IEmbeddingClient>();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pipeline:DuplicateThreshold"] = "0.85"
            })
            .Build();
    }

    [Fact]
    public async Task IsDuplicateAsync_WhenNoExistingEmbeddings_ReturnsFalse()
    {
        _embeddingClientMock
            .Setup(c => c.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);

        var sut = CreateSut();
        var result = await sut.IsDuplicateAsync("5 Things About Cars");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WhenHighlySimilar_ReturnsTrue()
    {
        // Arrange: seed an existing idea with an embedding
        var embedding = Enumerable.Range(0, 1536).Select(i => 1.0f).ToArray();
        var normalised = Normalize(embedding);

        var existingIdea = new Idea
        {
            Id = Guid.NewGuid(),
            Title = "5 Car Maintenance Mistakes",
            Status = "approved",
            Embedding = System.Text.Json.JsonSerializer.Serialize(normalised)
        };
        _db.Ideas.Add(existingIdea);
        await _db.SaveChangesAsync();

        // Candidate is nearly identical
        _embeddingClientMock
            .Setup(c => c.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalised); // cosine similarity = 1.0

        var sut = CreateSut();
        var result = await sut.IsDuplicateAsync("5 Car Maintenance Mistakes You Make");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsDuplicateAsync_WhenLowSimilarity_ReturnsFalse()
    {
        // Seed embedding pointing in one direction
        var existingEmbedding = new float[1536];
        existingEmbedding[0] = 1.0f;

        var existingIdea = new Idea
        {
            Id = Guid.NewGuid(),
            Title = "5 Car Tips",
            Status = "approved",
            Embedding = System.Text.Json.JsonSerializer.Serialize(existingEmbedding)
        };
        _db.Ideas.Add(existingIdea);
        await _db.SaveChangesAsync();

        // Candidate points in orthogonal direction — cosine similarity = 0
        var candidateEmbedding = new float[1536];
        candidateEmbedding[1] = 1.0f;

        _embeddingClientMock
            .Setup(c => c.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidateEmbedding);

        var sut = CreateSut();
        var result = await sut.IsDuplicateAsync("5 Cooking Tricks");

        result.Should().BeFalse();
    }

    private DuplicateDetectorService CreateSut() =>
        new(_embeddingClientMock.Object, _db, _config, NullLogger<DuplicateDetectorService>.Instance);

    private static float[] Normalize(float[] v)
    {
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        return v.Select(x => x / norm).ToArray();
    }

    public void Dispose() => _db.Dispose();
}
