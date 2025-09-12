
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoTradingBot.Services.Implementations;

public class ModelRegistry
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ModelRegistry> _logger;
    private readonly string _modelsBasePath;
    private readonly string _registryPath;

    public ModelRegistry(IUnitOfWork unitOfWork, ILogger<ModelRegistry> logger, string modelsBasePath = "/models")
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _modelsBasePath = modelsBasePath;
        _registryPath = Path.Combine(_modelsBasePath, "registry");
        
        Directory.CreateDirectory(_registryPath);
    }

    public async Task<ModelRegistryEntry> RegisterModelAsync(TrainingResult trainingResult, ModelMetadata metadata, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering model: {ModelId}", trainingResult.Id);

        var entry = new ModelRegistryEntry
        {
            Id = Guid.NewGuid(),
            ModelId = trainingResult.Id,
            ModelName = metadata.ModelPath,
            Version = GenerateVersion(metadata.ModelPath),
            ModelPath = trainingResult.ModelPath,
            Status = ModelRegistryStatus.Registered,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save registry entry to file system
        await SaveRegistryEntry(entry, cancellationToken);

        // Save to database
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        await repository.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Model registered successfully: {ModelName} v{Version}", entry.ModelName, entry.Version);
        return entry;
    }

    public async Task<ModelRegistryEntry?> GetModelAsync(string modelName, string? version = null, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        
        if (string.IsNullOrEmpty(version))
        {
            // Get latest version
            var models = await repository.FindAsync(m => m.ModelName == modelName);
            return models.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        }
        
        var model = await repository.FindAsync(m => m.ModelName == modelName && m.Version == version);
        return model.FirstOrDefault();
    }

    public async Task<IEnumerable<ModelRegistryEntry>> ListModelsAsync(ModelRegistryFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var query = await repository.GetAllAsync();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.ModelName))
                query = query.Where(m => m.ModelName.Contains(filter.ModelName, StringComparison.OrdinalIgnoreCase));

            if (filter.Status.HasValue)
                query = query.Where(m => m.Status == filter.Status.Value);

            if (filter.CreatedAfter.HasValue)
                query = query.Where(m => m.CreatedAt >= filter.CreatedAfter.Value);

            if (filter.CreatedBefore.HasValue)
                query = query.Where(m => m.CreatedAt <= filter.CreatedBefore.Value);

        }

        return query.OrderByDescending(m => m.CreatedAt);
    }

    public async Task<ModelRegistryEntry> UpdateModelStatusAsync(Guid modelId, ModelRegistryStatus status, string? notes = null, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var entry = await repository.GetByIdAsync(modelId);
        
        if (entry == null)
            throw new ArgumentException($"Model with ID {modelId} not found in registry.");

        _logger.LogInformation("Updating model status: {ModelName} v{Version} -> {Status}", 
            entry.ModelName, entry.Version, status);

        entry.Status = status;
        entry.UpdatedAt = DateTime.UtcNow;
        
        // Update deployment history
        entry.DeploymentHistory.Add(new DeploymentRecord
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Notes = notes ?? string.Empty
        });

        await repository.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        // Update registry file
        await SaveRegistryEntry(entry, cancellationToken);

        return entry;
    }

    public async Task<bool> DeleteModelAsync(Guid modelId, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var entry = await repository.GetByIdAsync(modelId);
        
        if (entry == null)
            return false;

        _logger.LogInformation("Deleting model: {ModelName} v{Version}", entry.ModelName, entry.Version);

        // Delete model files if requested
        if (deleteFiles && !string.IsNullOrEmpty(entry.ModelPath))
        {
            try
            {
                var modelFile = $"{entry.ModelPath}.json";
                if (File.Exists(modelFile))
                {
                    File.Delete(modelFile);
                }

                var modelDir = Path.GetDirectoryName(entry.ModelPath);
                if (Directory.Exists(modelDir) && !Directory.EnumerateFileSystemEntries(modelDir).Any())
                {
                    Directory.Delete(modelDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete model files: {ModelPath}", entry.ModelPath);
            }
        }

        // Delete registry entry file
        var registryFile = Path.Combine(_registryPath, $"{entry.Id}.json");
        if (File.Exists(registryFile))
        {
            File.Delete(registryFile);
        }

        // Delete from database
        await repository.DeleteAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<ModelRegistryEntry> DeployModelAsync(Guid modelId, DeploymentTarget target, DeploymentConfiguration config, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var entry = await repository.GetByIdAsync(modelId);
        
        if (entry == null)
            throw new ArgumentException($"Model with ID {modelId} not found in registry.");

        _logger.LogInformation("Deploying model: {ModelName} v{Version} to {Target}", 
            entry.ModelName, entry.Version, target);

        // Validate model before deployment
        if (!await ValidateModelForDeployment(entry, cancellationToken))
        {
            throw new InvalidOperationException($"Model {entry.ModelName} v{entry.Version} failed deployment validation.");
        }

        // Perform deployment based on target
        var deploymentResult = await PerformDeployment(entry, target, config, cancellationToken);

        // Update model status
        entry.Status = deploymentResult.Success ? ModelRegistryStatus.Deployed : ModelRegistryStatus.DeploymentFailed;
        entry.UpdatedAt = DateTime.UtcNow;

        // Add deployment record
        entry.DeploymentHistory.Add(new DeploymentRecord
        {
            Status = entry.Status,
            Target = target.ToString(),
            Timestamp = DateTime.UtcNow,
            Notes = deploymentResult.Message,
            Configuration = JsonConvert.SerializeObject(config)
        });

        await repository.UpdateAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        // Update registry file
        await SaveRegistryEntry(entry, cancellationToken);

        if (!deploymentResult.Success)
        {
            throw new InvalidOperationException($"Deployment failed: {deploymentResult.Message}");
        }

        _logger.LogInformation("Model deployed successfully: {ModelName} v{Version}", entry.ModelName, entry.Version);
        return entry;
    }

    public async Task<ModelRegistryEntry> CreateModelVersionAsync(Guid baseModelId, string newVersion, ModelMetadata metadata, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var baseModel = await repository.GetByIdAsync(baseModelId);
        
        if (baseModel == null)
            throw new ArgumentException($"Base model with ID {baseModelId} not found.");

        _logger.LogInformation("Creating new version: {ModelName} v{NewVersion}", baseModel.ModelName, newVersion);

        var newEntry = new ModelRegistryEntry
        {
            Id = Guid.NewGuid(),
            ModelId = Guid.NewGuid(), // New model ID for the version
            ModelName = baseModel.ModelName,
            Version = newVersion,
            ModelPath = GenerateVersionPath(baseModel.ModelName, newVersion),
            Status = ModelRegistryStatus.Registered,
            Metadata = metadata,
            ParentModelId = baseModelId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Copy model files to new version path
        await CopyModelFiles(baseModel.ModelPath, newEntry.ModelPath, cancellationToken);

        // Save new entry
        await repository.AddAsync(newEntry);
        await _unitOfWork.SaveChangesAsync();

        // Save registry entry
        await SaveRegistryEntry(newEntry, cancellationToken);

        return newEntry;
    }

    public async Task<ModelComparisonReport> CompareModelsAsync(IEnumerable<Guid> modelIds, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var models = new List<ModelRegistryEntry>();

        foreach (var modelId in modelIds)
        {
            var model = await repository.GetByIdAsync(modelId);
            if (model != null)
                models.Add(model);
        }

        _logger.LogInformation("Comparing {ModelCount} models", models.Count);

        var comparisons = new List<ModelComparison>();

        foreach (var model in models)
        {
            var metrics = await GetModelMetrics(model, cancellationToken);
            
            comparisons.Add(new ModelComparison
            {
                ModelId = model.Id,
                ModelName = model.ModelName,
                Version = model.Version,
                Metrics = metrics,
                CreatedAt = model.CreatedAt,
                Status = model.Status
            });
        }

        var report = new ModelComparisonReport
        {
            Comparisons = comparisons,
            ComparisonDate = DateTime.UtcNow,
            BestModel = DetermineBestModel(comparisons),
            Summary = GenerateComparisonSummary(comparisons)
        };

        return report;
    }

    public async Task<ModelLineage> GetModelLineageAsync(Guid modelId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<ModelRegistryEntry>();
        var model = await repository.GetByIdAsync(modelId);
        
        if (model == null)
            throw new ArgumentException($"Model with ID {modelId} not found.");

        var lineage = new ModelLineage
        {
            RootModel = model,
            Versions = new List<ModelRegistryEntry>(),
            Children = new List<ModelRegistryEntry>()
        };

        // Find all versions of this model
        var allVersions = await repository.FindAsync(m => m.ModelName == model.ModelName);
        lineage.Versions = allVersions.OrderBy(m => m.CreatedAt).ToList();

        // Find child models (if this model was used as a base)
        var children = await repository.FindAsync(m => m.ParentModelId == modelId);
        lineage.Children = children.ToList();

        return lineage;
    }

    // Helper methods
    private string GenerateVersion(string modelName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return $"v{timestamp}";
    }

    private string GenerateVersionPath(string modelName, string version)
    {
        return Path.Combine(_modelsBasePath, modelName, version, "model");
    }

    private async Task SaveRegistryEntry(ModelRegistryEntry entry, CancellationToken cancellationToken)
    {
        var registryFile = Path.Combine(_registryPath, $"{entry.Id}.json");
        var json = JsonConvert.SerializeObject(entry, Formatting.Indented);
        await File.WriteAllTextAsync(registryFile, json, cancellationToken);
    }

    private async Task<bool> ValidateModelForDeployment(ModelRegistryEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            // Check if model files exist
            var modelFile = $"{entry.ModelPath}.json";
            if (!File.Exists(modelFile))
            {
                _logger.LogWarning("Model file not found: {ModelFile}", modelFile);
                return false;
            }

            // Validate model structure
            var json = await File.ReadAllTextAsync(modelFile, cancellationToken);
            var modelData = JsonConvert.DeserializeObject<dynamic>(json);
            
            if (modelData == null || modelData.IsTrained != true)
            {
                _logger.LogWarning("Model is not properly trained: {ModelPath}", entry.ModelPath);
                return false;
            }

            // Additional validation checks can be added here
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model validation failed: {ModelPath}", entry.ModelPath);
            return false;
        }
    }

    private async Task<DeploymentResult> PerformDeployment(ModelRegistryEntry entry, DeploymentTarget target, DeploymentConfiguration config, CancellationToken cancellationToken)
    {
        try
        {
            switch (target)
            {
                case DeploymentTarget.Production:
                    return await DeployToProduction(entry, config, cancellationToken);
                
                case DeploymentTarget.Staging:
                    return await DeployToStaging(entry, config, cancellationToken);
                
                case DeploymentTarget.Development:
                    return await DeployToDevelopment(entry, config, cancellationToken);
                
                default:
                    return new DeploymentResult { Success = false, Message = $"Unknown deployment target: {target}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed for model: {ModelName} v{Version}", entry.ModelName, entry.Version);
            return new DeploymentResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<DeploymentResult> DeployToProduction(ModelRegistryEntry entry, DeploymentConfiguration config, CancellationToken cancellationToken)
    {
        // Simulate production deployment
        _logger.LogInformation("Deploying to production: {ModelName} v{Version}", entry.ModelName, entry.Version);
        
        var prodPath = Path.Combine(_modelsBasePath, "production", entry.ModelName);
        Directory.CreateDirectory(prodPath);
        
        await CopyModelFiles(entry.ModelPath, Path.Combine(prodPath, "model"), cancellationToken);
        
        // Create deployment manifest
        var manifest = new
        {
            ModelName = entry.ModelName,
            Version = entry.Version,
            DeployedAt = DateTime.UtcNow,
            Configuration = config
        };
        
        var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        await File.WriteAllTextAsync(Path.Combine(prodPath, "deployment.json"), manifestJson, cancellationToken);
        
        return new DeploymentResult { Success = true, Message = "Successfully deployed to production" };
    }

    private async Task<DeploymentResult> DeployToStaging(ModelRegistryEntry entry, DeploymentConfiguration config, CancellationToken cancellationToken)
    {
        // Simulate staging deployment
        _logger.LogInformation("Deploying to staging: {ModelName} v{Version}", entry.ModelName, entry.Version);
        
        var stagingPath = Path.Combine(_modelsBasePath, "staging", entry.ModelName);
        Directory.CreateDirectory(stagingPath);
        
        await CopyModelFiles(entry.ModelPath, Path.Combine(stagingPath, "model"), cancellationToken);
        
        return new DeploymentResult { Success = true, Message = "Successfully deployed to staging" };
    }

    private async Task<DeploymentResult> DeployToDevelopment(ModelRegistryEntry entry, DeploymentConfiguration config, CancellationToken cancellationToken)
    {
        // Simulate development deployment
        _logger.LogInformation("Deploying to development: {ModelName} v{Version}", entry.ModelName, entry.Version);
        
        var devPath = Path.Combine(_modelsBasePath, "development", entry.ModelName);
        Directory.CreateDirectory(devPath);
        
        await CopyModelFiles(entry.ModelPath, Path.Combine(devPath, "model"), cancellationToken);
        
        return new DeploymentResult { Success = true, Message = "Successfully deployed to development" };
    }

    private async Task CopyModelFiles(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var sourceFile = $"{sourcePath}.json";
        var destFile = $"{destinationPath}.json";
        
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        
        if (File.Exists(sourceFile))
        {
            using var sourceStream = File.Open(sourceFile, FileMode.Open);
            using var destinationStream = File.Create(destFile);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }
    }

    private async Task<Dictionary<string, object>> GetModelMetrics(ModelRegistryEntry model, CancellationToken cancellationToken)
    {
        // Get metrics from training result
        var trainingRepository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await trainingRepository.GetByIdAsync(model.ModelId);
        
        if (trainingResult != null && !string.IsNullOrEmpty(trainingResult.ModelArtifacts))
        {
            try
            {
                var artifacts = JsonConvert.DeserializeObject<Dictionary<string, object>>(trainingResult.ModelArtifacts);
                return artifacts ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse model artifacts for model: {ModelId}", model.Id);
            }
        }

        return new Dictionary<string, object>();
    }

    private ModelComparison DetermineBestModel(List<ModelComparison> comparisons)
    {
        // Simple logic: best model has highest accuracy
        return comparisons.OrderByDescending(c => 
        {
            if (c.Metrics.TryGetValue("TestMetrics", out var testMetrics) && testMetrics is Dictionary<string, object> metrics)
            {
                if (metrics.TryGetValue("Accuracy", out var accuracy))
                {
                    return Convert.ToDouble(accuracy);
                }
            }
            return 0.0;
        }).FirstOrDefault() ?? new ModelComparison();
    }

    private string GenerateComparisonSummary(List<ModelComparison> comparisons)
    {
        var summary = new List<string>
        {
            $"Compared {comparisons.Count} models",
            $"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };

        if (comparisons.Any())
        {
            var best = DetermineBestModel(comparisons);
            summary.Add($"Best model: {best.ModelName} v{best.Version}");
        }

        return string.Join("\n", summary);
    }
}

// Supporting classes for ModelRegistry
public class ModelRegistryEntry
{
    public Guid Id { get; set; }
    public Guid ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public ModelRegistryStatus Status { get; set; }
    public ModelMetadata Metadata { get; set; } = new();
    public Guid? ParentModelId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DeploymentRecord> DeploymentHistory { get; set; } = new();
}


public class DeploymentRecord
{
    public ModelRegistryStatus Status { get; set; }
    public string Target { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
}

public class ModelRegistryFilter
{
    public string? ModelName { get; set; }
    public ModelRegistryStatus? Status { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? Tag { get; set; }
}

public class DeploymentConfiguration
{
    public string Environment { get; set; } = string.Empty;
    public Dictionary<string, object> Settings { get; set; } = new();
    public bool AutoScale { get; set; } = false;
    public int Replicas { get; set; } = 1;
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ModelComparisonReport
{
    public List<ModelComparison> Comparisons { get; set; } = new();
    public DateTime ComparisonDate { get; set; }
    public ModelComparison BestModel { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class ModelComparison
{
    public Guid ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public ModelRegistryStatus Status { get; set; }
}

public class ModelLineage
{
    public ModelRegistryEntry RootModel { get; set; } = new();
    public List<ModelRegistryEntry> Versions { get; set; } = new();
    public List<ModelRegistryEntry> Children { get; set; } = new();
}

public enum ModelRegistryStatus
{
    Registered,
    Validated,
    Deployed,
    Deprecated,
    Archived,
    DeploymentFailed
}

public enum DeploymentTarget
{
    Development,
    Staging,
    Production
}
