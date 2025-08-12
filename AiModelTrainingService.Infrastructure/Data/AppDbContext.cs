
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiModelTrainingService.Core.Entities;

namespace AiModelTrainingService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Existing DbSets
    public DbSet<ModelDefinition> ModelDefinitions { get; set; }
    public DbSet<TrainingJob> TrainingJobs { get; set; }
    public DbSet<Dataset> Datasets { get; set; }
    public DbSet<TrainingMetric> TrainingMetrics { get; set; }
    
    // New Trading-specific DbSets
    public DbSet<OrderBookData> OrderBookData { get; set; }
    public DbSet<TrainingData> TrainingData { get; set; }
    public DbSet<ModelConfiguration> ModelConfigurations { get; set; }
    public DbSet<TrainingResult> TrainingResults { get; set; }
    public DbSet<EvaluationMetrics> EvaluationMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ModelDefinition configuration
        modelBuilder.Entity<ModelDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Configuration);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            
            entity.HasMany(e => e.TrainingJobs)
                  .WithOne(e => e.ModelDefinition)
                  .HasForeignKey(e => e.ModelDefinitionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TrainingJob configuration
        modelBuilder.Entity<TrainingJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Parameters);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ModelPath).HasMaxLength(500);
            
            entity.HasMany(e => e.Metrics)
                  .WithOne(e => e.TrainingJob)
                  .HasForeignKey(e => e.TrainingJobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Dataset configuration
        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
        });

        // TrainingMetric configuration
        modelBuilder.Entity<TrainingMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
        });

        // Many-to-many relationship between ModelDefinition and Dataset
        modelBuilder.Entity<ModelDefinition>()
            .HasMany(e => e.Datasets)
            .WithMany(e => e.ModelDefinitions);

        // OrderBookData configuration
        modelBuilder.Entity<OrderBookData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.BestBidPrice).HasPrecision(18, 8);
            entity.Property(e => e.BestAskPrice).HasPrecision(18, 8);
            entity.Property(e => e.BestBidQuantity).HasPrecision(18, 8);
            entity.Property(e => e.BestAskQuantity).HasPrecision(18, 8);
            entity.Property(e => e.Spread).HasPrecision(18, 8);
            entity.Property(e => e.MidPrice).HasPrecision(18, 8);
            entity.Property(e => e.TotalBidVolume).HasPrecision(18, 8);
            entity.Property(e => e.TotalAskVolume).HasPrecision(18, 8);
            entity.Property(e => e.RawData);
            
            entity.HasIndex(e => new { e.Symbol, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);
        });

        // TrainingData configuration
        modelBuilder.Entity<TrainingData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Features).IsRequired();
            entity.Property(e => e.Labels).IsRequired();
            entity.Property(e => e.PredictedValue).HasPrecision(18, 8);
            entity.Property(e => e.ActualValue).HasPrecision(18, 8);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProcessingVersion).HasMaxLength(50);
            
            entity.HasOne(e => e.OrderBookData)
                  .WithMany(e => e.TrainingDataRecords)
                  .HasForeignKey(e => e.OrderBookDataId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.ModelConfiguration)
                  .WithMany(e => e.TrainingDataRecords)
                  .HasForeignKey(e => e.ModelConfigurationId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => e.DataType);
            entity.HasIndex(e => e.ProcessedAt);
        });

        // ModelConfiguration configuration
        modelBuilder.Entity<ModelConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Algorithm).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Hyperparameters);
            entity.Property(e => e.FeatureColumns);
            entity.Property(e => e.TargetColumn).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PreprocessingSteps);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ModelType);
        });

        // TrainingResult configuration
        modelBuilder.Entity<TrainingResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModelVersion).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ModelPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ModelArtifacts);
            entity.Property(e => e.TrainingLogs);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            
            entity.HasOne(e => e.ModelConfiguration)
                  .WithMany(e => e.TrainingResults)
                  .HasForeignKey(e => e.ModelConfigurationId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.TrainingJob)
                  .WithOne()
                  .HasForeignKey<TrainingResult>(e => e.TrainingJobId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TrainingStartedAt);
        });

        // EvaluationMetrics configuration
        modelBuilder.Entity<EvaluationMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomMetrics);
            
            entity.HasOne(e => e.TrainingResult)
                  .WithMany(e => e.EvaluationMetrics)
                  .HasForeignKey(e => e.TrainingResultId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => new { e.TrainingResultId, e.MetricType, e.Epoch });
        });
    }
}
