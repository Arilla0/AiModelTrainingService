
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Enums;

namespace CryptoTradingBot.Services.Interfaces;

public interface IModelTrainerService
{
    Task<ModelDefinition> CreateModelAsync(string name, string description, ModelType type, string configuration, string createdBy);
    Task<IEnumerable<ModelDefinition>> GetAllModelsAsync();
    Task<ModelDefinition?> GetModelByIdAsync(Guid id);
    Task<ModelDefinition> UpdateModelAsync(Guid id, string name, string description, string configuration);
    Task DeleteModelAsync(Guid id);
    Task<TrainingJob> StartTrainingAsync(Guid modelId, string jobName, string parameters, int epochs);
    Task<TrainingJob?> GetTrainingJobAsync(Guid jobId);
    Task<IEnumerable<TrainingJob>> GetModelTrainingJobsAsync(Guid modelId);
    Task CancelTrainingAsync(Guid jobId);
    Task<IEnumerable<TrainingMetric>> GetTrainingMetricsAsync(Guid jobId);
}
