
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Services.Interfaces;

namespace CryptoTradingBot.Services.Implementations;

public class DatasetService : IDatasetService
{
    private readonly IUnitOfWork _unitOfWork;

    public DatasetService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Dataset> CreateDatasetAsync(string name, string description, string filePath, string format, long fileSize, int recordCount, string createdBy)
    {
        var dataset = new Dataset
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            FilePath = filePath,
            Format = format,
            FileSize = fileSize,
            RecordCount = recordCount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _unitOfWork.Datasets.AddAsync(dataset);
        await _unitOfWork.SaveChangesAsync();

        return dataset;
    }

    public async Task<IEnumerable<Dataset>> GetAllDatasetsAsync()
    {
        return await _unitOfWork.Datasets.GetAllAsync();
    }

    public async Task<Dataset?> GetDatasetByIdAsync(Guid id)
    {
        return await _unitOfWork.Datasets.GetByIdAsync(id);
    }

    public async Task<Dataset> UpdateDatasetAsync(Guid id, string name, string description)
    {
        var dataset = await _unitOfWork.Datasets.GetByIdAsync(id);
        if (dataset == null)
            throw new ArgumentException($"Dataset with ID {id} not found.");

        dataset.Name = name;
        dataset.Description = description;

        await _unitOfWork.Datasets.UpdateAsync(dataset);
        await _unitOfWork.SaveChangesAsync();

        return dataset;
    }

    public async Task DeleteDatasetAsync(Guid id)
    {
        var dataset = await _unitOfWork.Datasets.GetByIdAsync(id);
        if (dataset == null)
            throw new ArgumentException($"Dataset with ID {id} not found.");

        await _unitOfWork.Datasets.DeleteAsync(dataset);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task AssignDatasetToModelAsync(Guid datasetId, Guid modelId)
    {
        var dataset = await _unitOfWork.Datasets.GetByIdAsync(datasetId);
        var model = await _unitOfWork.ModelDefinitions.GetByIdAsync(modelId);

        if (dataset == null)
            throw new ArgumentException($"Dataset with ID {datasetId} not found.");
        if (model == null)
            throw new ArgumentException($"Model with ID {modelId} not found.");

        if (!dataset.ModelDefinitions.Any(m => m.Id == modelId))
        {
            dataset.ModelDefinitions.Add(model);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task RemoveDatasetFromModelAsync(Guid datasetId, Guid modelId)
    {
        var dataset = await _unitOfWork.Datasets.GetByIdAsync(datasetId);
        if (dataset == null)
            throw new ArgumentException($"Dataset with ID {datasetId} not found.");

        var model = dataset.ModelDefinitions.FirstOrDefault(m => m.Id == modelId);
        if (model != null)
        {
            dataset.ModelDefinitions.Remove(model);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Dataset>> GetModelDatasetsAsync(Guid modelId)
    {
        var datasets = await _unitOfWork.Datasets.GetAllAsync();
        return datasets.Where(d => d.ModelDefinitions.Any(m => m.Id == modelId));
    }
}
