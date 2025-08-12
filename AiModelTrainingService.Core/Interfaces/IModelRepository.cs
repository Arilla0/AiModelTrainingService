
using AiModelTrainingService.Core.Entities;

namespace AiModelTrainingService.Core.Interfaces;

public interface IModelRepository
{
    Task<string> SaveModelAsync(TrainingResult trainingResult, byte[] modelData, CancellationToken cancellationToken = default);
    Task<byte[]?> LoadModelAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<bool> DeleteModelAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<ModelMetadata?> GetModelMetadataAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> ListModelsAsync(Guid? modelConfigurationId = null, CancellationToken cancellationToken = default);
    Task<bool> ModelExistsAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<string> CreateModelVersionAsync(Guid modelConfigurationId, CancellationToken cancellationToken = default);
    Task<long> GetModelSizeAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<string> ExportModelAsync(string modelPath, string exportFormat, CancellationToken cancellationToken = default);
}

public class ModelMetadata
{
    public string ModelPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
