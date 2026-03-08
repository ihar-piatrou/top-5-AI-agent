namespace Top5Agent.Core.Interfaces;

public interface IEmbeddingClient
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
}
