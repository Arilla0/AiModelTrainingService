
using AiModelTrainingService.Core.Entities;

namespace AiModelTrainingService.Services.Interfaces;

public interface IDatasetService
{
    Task<Dataset> CreateDatasetAsync(string name, string description, string filePath, string format, long fileSize, int recordCount, string createdBy);
    Task<IEnumerable<Dataset>> GetAllDatasetsAsync();
    Task<Dataset?> GetDatasetByIdAsync(Guid id);
    Task<Dataset> UpdateDatasetAsync(Guid id, string name, string description);
    Task DeleteDatasetAsync(Guid id);
    Task AssignDatasetToModelAsync(Guid datasetId, Guid modelId);
    Task RemoveDatasetFromModelAsync(Guid datasetId, Guid modelId);
    Task<IEnumerable<Dataset>> GetModelDatasetsAsync(Guid modelId);
}
