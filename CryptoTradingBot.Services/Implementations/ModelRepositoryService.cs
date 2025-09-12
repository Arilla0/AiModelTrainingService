
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;

namespace CryptoTradingBot.Services.Implementations;

public class ModelRepositoryService : IModelRepository
{
    private readonly string _modelsBasePath;

    public ModelRepositoryService()
    {
        _modelsBasePath = Path.Combine(Environment.CurrentDirectory, "Models");
        Directory.CreateDirectory(_modelsBasePath);
    }

    public async Task<string> SaveModelAsync(TrainingResult trainingResult, byte[] modelData, CancellationToken cancellationToken = default)
    {
        var modelPath = Path.Combine(_modelsBasePath, $"{trainingResult.Id}.model");
        await File.WriteAllBytesAsync(modelPath, modelData, cancellationToken);
        return modelPath;
    }

    public async Task<byte[]?> LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(modelPath))
            return null;

        return await File.ReadAllBytesAsync(modelPath, cancellationToken);
    }

    public async Task<bool> DeleteModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            if (File.Exists(modelPath))
            {
                File.Delete(modelPath);
                return true;
            }
            return false;
        }, cancellationToken);

        return !File.Exists(modelPath);
    }

    public async Task<Core.Interfaces.ModelMetadata?> GetModelMetadataAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(modelPath))
            return null;

        var fileInfo = new FileInfo(modelPath);
        
        return await Task.FromResult(new CryptoTradingBot.Core.Interfaces.ModelMetadata
        {
            ModelPath = modelPath,
            Size = fileInfo.Length,
            CreatedAt = fileInfo.CreationTimeUtc,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            Version = "1.0.0" // TODO: Extract from model file
        });
    }

    public async Task<IEnumerable<string>> ListModelsAsync(Guid? modelConfigurationId = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var modelFiles = Directory.GetFiles(_modelsBasePath, "*.model");
            // TODO: Filter by modelConfigurationId if provided
            return modelFiles.AsEnumerable();
        }, cancellationToken);
    }

    public async Task<bool> ModelExistsAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(File.Exists(modelPath));
    }

    public async Task<string> CreateModelVersionAsync(Guid modelConfigurationId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement versioning logic
        var version = $"v{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        return await Task.FromResult(version);
    }

    public async Task<long> GetModelSizeAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(modelPath))
            return 0;

        var fileInfo = new FileInfo(modelPath);
        return await Task.FromResult(fileInfo.Length);
    }

    public async Task<string> ExportModelAsync(string modelPath, string exportFormat, CancellationToken cancellationToken = default)
    {
        // TODO: Implement export logic based on format
        var exportPath = Path.ChangeExtension(modelPath, exportFormat.ToLower());
        
        if (File.Exists(modelPath))
        {
            var sourceBytes = await File.ReadAllBytesAsync(modelPath, cancellationToken);
            await File.WriteAllBytesAsync(exportPath, sourceBytes, cancellationToken);
        }
        
        return exportPath;
    }
}
