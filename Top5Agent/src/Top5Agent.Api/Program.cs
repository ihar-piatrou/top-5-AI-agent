using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using Top5Agent.Api.Endpoints;
using Top5Agent.Core.Interfaces;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Infrastructure.EmbeddingClients;
using Top5Agent.Infrastructure.LlmClients;
using Top5Agent.Infrastructure.MediaClients;
using Top5Agent.Pipeline;
using Top5Agent.Pipeline.Jobs;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = config.GetConnectionString("DefaultConnection")!;

services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));

// ── OpenAI ────────────────────────────────────────────────────────────────────
services.AddSingleton(new OpenAIClient(new System.ClientModel.ApiKeyCredential(config["OpenAI:ApiKey"]!)));

// ── Anthropic (typed HttpClient — no SDK dependency) ──────────────────────────
services.AddHttpClient<ClaudeClient>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("x-api-key", config["Anthropic:ApiKey"]);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

// ── Pexels (typed HttpClient) ─────────────────────────────────────────────────
services.AddHttpClient<IMediaProvider, PexelsMediaProvider>(client =>
{
    client.BaseAddress = new Uri(config["Pexels:BaseUrl"] ?? "https://api.pexels.com/");
    client.DefaultRequestHeaders.Add("Authorization", config["Pexels:ApiKey"]);
});

// ── HeyGen (typed HttpClient) ─────────────────────────────────────────────────
services.AddHttpClient<HeyGenClient>(client =>
{
    client.BaseAddress = new Uri("https://api.heygen.com/");
    client.DefaultRequestHeaders.Add("X-Api-Key", config["HeyGen:ApiKey"]);
});

// ── LLM / embedding clients ───────────────────────────────────────────────────
services.AddScoped<GptClient>();
services.AddScoped<IEmbeddingClient, OpenAiEmbeddingClient>();

// ── Pipeline services (wired to correct LLM client) ──────────────────────────
services.AddScoped<IdeaGeneratorService>(sp => new IdeaGeneratorService(
    sp.GetRequiredService<GptClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ILogger<IdeaGeneratorService>>()
));

services.AddScoped<DuplicateDetectorService>();

services.AddScoped<ScriptWriterService>(sp => new ScriptWriterService(
    sp.GetRequiredService<ClaudeClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ILogger<ScriptWriterService>>()
));

services.AddScoped<FactReviewerService>(sp => new FactReviewerService(
    sp.GetRequiredService<GptClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ILogger<FactReviewerService>>()
));

services.AddScoped<ContentPolisherService>(sp => new ContentPolisherService(
    sp.GetRequiredService<ClaudeClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<ILogger<ContentPolisherService>>()
));

services.AddScoped<MediaAcquisitionService>();
services.AddScoped<HeyGenAvatarVideoService>();
services.AddScoped<HeyGenAudioService>();
services.AddScoped<HeyGenPollingService>();
services.AddScoped<PipelineOrchestrator>();

// ── Hangfire jobs ─────────────────────────────────────────────────────────────
services.AddScoped<GenerateIdeasJob>();
services.AddScoped<ProcessIdeaJob>();
services.AddScoped<DownloadMediaJob>();
services.AddScoped<GenerateHeyGenMediaJob>();
services.AddScoped<PollHeyGenJobsJob>();

// ── Hangfire ──────────────────────────────────────────────────────────────────
services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

services.AddHangfireServer();

// ── Swagger ───────────────────────────────────────────────────────────────────
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

// ── Migrations ────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard(config["Hangfire:DashboardPath"] ?? "/hangfire");

 //── Hangfire recurring jobs ───────────────────────────────────────────────────
RecurringJob.AddOrUpdate<GenerateIdeasJob>(
    "daily-idea-generation",
    j => j.ExecuteAsync(CancellationToken.None),
    Cron.Never());

RecurringJob.AddOrUpdate<PollHeyGenJobsJob>(
    "poll-heygen-jobs",
    j => j.ExecuteAsync(CancellationToken.None),
    "*/2 * * * *"); // every 2 minutes

// ── API endpoints ─────────────────────────────────────────────────────────────
app.MapPipelineEndpoints();
app.MapIdeasEndpoints();
app.MapScriptsEndpoints();
app.MapMediaEndpoints();
app.MapHeyGenEndpoints();

app.Run();
