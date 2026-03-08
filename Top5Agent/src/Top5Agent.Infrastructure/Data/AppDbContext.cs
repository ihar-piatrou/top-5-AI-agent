using Microsoft.EntityFrameworkCore;
using Top5Agent.Core.Models;

namespace Top5Agent.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ScriptSection> ScriptSections => Set<ScriptSection>();
    public DbSet<ScriptReview> ScriptReviews => Set<ScriptReview>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Idea>(e =>
        {
            e.ToTable("ideas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Niche).HasMaxLength(100);
            e.Property(x => x.Summary).HasMaxLength(1000);
            e.Property(x => x.Embedding).HasColumnType("nvarchar(max)");
            e.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("pending");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<Script>(e =>
        {
            e.ToTable("scripts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.JsonContent).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.RawText).HasColumnType("nvarchar(max)");
            e.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("draft");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Idea).WithMany(x => x.Scripts).HasForeignKey(x => x.IdeaId);
        });

        modelBuilder.Entity<ScriptSection>(e =>
        {
            e.ToTable("script_sections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Narration).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.MediaQuery).HasMaxLength(300);
            e.Property(x => x.MediaType).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Script).WithMany(x => x.Sections).HasForeignKey(x => x.ScriptId);
        });

        modelBuilder.Entity<ScriptReview>(e =>
        {
            e.ToTable("script_reviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.Reviewer).HasMaxLength(50).IsRequired();
            e.Property(x => x.ReviewText).HasColumnType("nvarchar(max)");
            e.Property(x => x.IssuesFound).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Script).WithMany(x => x.Reviews).HasForeignKey(x => x.ScriptId);
        });

        modelBuilder.Entity<Source>(e =>
        {
            e.ToTable("sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Script).WithMany(x => x.Sources).HasForeignKey(x => x.ScriptId);
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.ToTable("media_assets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.PexelsId).HasMaxLength(50);
            e.Property(x => x.AssetType).HasMaxLength(20).IsRequired();
            e.Property(x => x.RemoteUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.LocalPath).HasMaxLength(1000);
            e.Property(x => x.Attribution).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.ScriptSection).WithMany(x => x.MediaAssets).HasForeignKey(x => x.ScriptSectionId);
        });

        modelBuilder.Entity<PipelineRun>(e =>
        {
            e.ToTable("pipeline_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(x => x.TriggerReason).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("running");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
