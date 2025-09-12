
using CryptoTradingBot.Core.Entities;

namespace CryptoTradingBot.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // Existing repositories
    IRepository<ModelDefinition> ModelDefinitions { get; }
    IRepository<TrainingJob> TrainingJobs { get; }
    IRepository<Dataset> Datasets { get; }
    IRepository<TrainingMetric> TrainingMetrics { get; }
    
    // New trading-specific repositories
    IRepository<OrderBookData> OrderBookData { get; }
    IRepository<TrainingData> TrainingData { get; }
    IRepository<ModelConfiguration> ModelConfigurations { get; }
    IRepository<TrainingResult> TrainingResults { get; }
    IRepository<EvaluationMetrics> EvaluationMetrics { get; }
    
    // Generic repository access
    IRepository<T> Repository<T>() where T : class;
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
