
using CryptoTradingBot.Core.Entities;

namespace CryptoTradingBot.Core.Interfaces;

public interface IModelTrainingService
{
    Task<TrainingResult> StartTrainingAsync(Guid modelConfigurationId, Guid datasetId, CancellationToken cancellationToken = default);
    Task<TrainingResult> ResumeTrainingAsync(Guid trainingResultId, CancellationToken cancellationToken = default);
    Task<bool> StopTrainingAsync(Guid trainingResultId, CancellationToken cancellationToken = default);
    Task<TrainingResult?> GetTrainingResultAsync(Guid trainingResultId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainingResult>> GetTrainingHistoryAsync(Guid modelConfigurationId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTrainingResultAsync(Guid trainingResultId, CancellationToken cancellationToken = default);
    Task<string> ExportModelAsync(Guid trainingResultId, string format, CancellationToken cancellationToken = default);
}
