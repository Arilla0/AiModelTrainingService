
using Microsoft.EntityFrameworkCore.Storage;
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Infrastructure.Data;

namespace CryptoTradingBot.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repositories = new();
    
    // Existing repositories
    private IRepository<ModelDefinition>? _modelDefinitions;
    private IRepository<TrainingJob>? _trainingJobs;
    private IRepository<Dataset>? _datasets;
    private IRepository<TrainingMetric>? _trainingMetrics;
    
    // New trading-specific repositories
    private IRepository<OrderBookData>? _orderBookData;
    private IRepository<TrainingData>? _trainingData;
    private IRepository<ModelConfiguration>? _modelConfigurations;
    private IRepository<TrainingResult>? _trainingResults;
    private IRepository<EvaluationMetrics>? _evaluationMetrics;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<ModelDefinition> ModelDefinitions =>
        _modelDefinitions ??= new Repository<ModelDefinition>(_context);

    public IRepository<TrainingJob> TrainingJobs =>
        _trainingJobs ??= new Repository<TrainingJob>(_context);

    public IRepository<Dataset> Datasets =>
        _datasets ??= new Repository<Dataset>(_context);

    public IRepository<TrainingMetric> TrainingMetrics =>
        _trainingMetrics ??= new Repository<TrainingMetric>(_context);

    public IRepository<OrderBookData> OrderBookData =>
        _orderBookData ??= new Repository<OrderBookData>(_context);

    public IRepository<TrainingData> TrainingData =>
        _trainingData ??= new Repository<TrainingData>(_context);

    public IRepository<ModelConfiguration> ModelConfigurations =>
        _modelConfigurations ??= new Repository<ModelConfiguration>(_context);

    public IRepository<TrainingResult> TrainingResults =>
        _trainingResults ??= new Repository<TrainingResult>(_context);

    public IRepository<EvaluationMetrics> EvaluationMetrics =>
        _evaluationMetrics ??= new Repository<EvaluationMetrics>(_context);

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (_repositories.ContainsKey(type))
        {
            return (IRepository<T>)_repositories[type];
        }

        var repository = new Repository<T>(_context);
        _repositories[type] = repository;
        return repository;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
