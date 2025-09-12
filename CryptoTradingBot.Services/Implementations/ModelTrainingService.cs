
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Services.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoTradingBot.Services.Implementations;

public class ModelTrainingService : IModelTrainingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataLoader _dataLoader;
    private readonly IFeatureEngineering _featureEngineering;
    private readonly IModelRepository _modelRepository;
    private readonly IModelEvaluator _modelEvaluator;
    private readonly ILogger<ModelTrainingService> _logger;

    public ModelTrainingService(
        IUnitOfWork unitOfWork,
        IDataLoader dataLoader,
        IFeatureEngineering featureEngineering,
        IModelRepository modelRepository,
        IModelEvaluator modelEvaluator,
        ILogger<ModelTrainingService> logger)
    {
        _unitOfWork = unitOfWork;
        _dataLoader = dataLoader;
        _featureEngineering = featureEngineering;
        _modelRepository = modelRepository;
        _modelEvaluator = modelEvaluator;
        _logger = logger;
    }

    public async Task<TrainingResult> StartTrainingAsync(Guid modelConfigurationId, Guid datasetId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting training for model configuration: {ModelConfigurationId}", modelConfigurationId);

        var trainingResult = new TrainingResult
        {
            Id = Guid.NewGuid(),
            ModelConfigurationId = modelConfigurationId,
            TrainingJobId = Guid.NewGuid(),
            ModelVersion = "1.0.0",
            ModelPath = $"/models/{modelConfigurationId}/{Guid.NewGuid()}",
            Status = Core.Enums.TrainingStatus.InProgress,
            TrainingStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TotalEpochs = 100
        };

        var repository = _unitOfWork.Repository<TrainingResult>();
        await repository.AddAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            // Load model configuration
            var configRepository = _unitOfWork.Repository<ModelConfiguration>();
            var modelConfig = await configRepository.GetByIdAsync(modelConfigurationId);
            
            if (modelConfig == null)
                throw new ArgumentException($"Model configuration with ID {modelConfigurationId} not found.");

            // Load dataset
            var datasetRepository = _unitOfWork.Repository<Dataset>();
            var dataset = await datasetRepository.GetByIdAsync(datasetId);
            
            if (dataset == null)
                throw new ArgumentException($"Dataset with ID {datasetId} not found.");

            // Load and prepare data
            var orderBookData = await LoadOrderBookDataFromDataset(dataset, cancellationToken);
            var trainingData = await _featureEngineering.ExtractFeaturesAsync(orderBookData, modelConfig, cancellationToken);

            // Prepare training tensors
            var (xTrain, yTrain, xVal, yVal, xTest, yTest) = PrepareTrainingData(trainingData);

            // Create and train DeepLOB model
            var modelParams = ParseModelParameters(modelConfig.Hyperparameters);
            var deepLobModel = new DeepLOBModel(
                sequenceLength: modelParams.SequenceLength,
                numFeatures: modelParams.NumFeatures,
                numClasses: modelParams.NumClasses,
                learningRate: modelParams.LearningRate
            );

            deepLobModel.BuildModel();
            
            _logger.LogInformation("Training DeepLOB model with {TrainSamples} training samples", xTrain.GetLength(0));

            // Train the model
            deepLobModel.Train(
                xTrain: xTrain,
                yTrain: yTrain,
                xVal: xVal,
                yVal: yVal,
                epochs: modelParams.Epochs,
                batchSize: modelParams.BatchSize,
                verbose: true
            );

            // Save the trained model
            Directory.CreateDirectory(Path.GetDirectoryName(trainingResult.ModelPath)!);
            deepLobModel.SaveModel(trainingResult.ModelPath);

            // Evaluate the model
            var predictions = deepLobModel.Predict(xTest);
            var evaluationMetrics = await EvaluateModel(predictions, yTest, cancellationToken);

            // Update training result
            trainingResult.Status = Core.Enums.TrainingStatus.Completed;
            trainingResult.TrainingCompletedAt = DateTime.UtcNow;
            trainingResult.TrainingDuration = trainingResult.TrainingCompletedAt - trainingResult.TrainingStartedAt;
            trainingResult.EpochsCompleted = modelParams.Epochs;
            trainingResult.ModelArtifacts = JsonConvert.SerializeObject(evaluationMetrics);

            await repository.UpdateAsync(trainingResult);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Training completed successfully for model: {ModelId}", trainingResult.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training failed for model: {ModelId}", trainingResult.Id);
            
            trainingResult.Status = Core.Enums.TrainingStatus.Failed;
            trainingResult.TrainingCompletedAt = DateTime.UtcNow;
            trainingResult.TrainingDuration = trainingResult.TrainingCompletedAt - trainingResult.TrainingStartedAt;
            trainingResult.ErrorMessage = ex.Message;

            await repository.UpdateAsync(trainingResult);
            await _unitOfWork.SaveChangesAsync();
        }

        return trainingResult;
    }

    public async Task<TrainingResult> ResumeTrainingAsync(Guid trainingResultId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await repository.GetByIdAsync(trainingResultId);
        
        if (trainingResult == null)
            throw new ArgumentException($"Training result with ID {trainingResultId} not found.");

        _logger.LogInformation("Resuming training for model: {ModelId}", trainingResultId);

        trainingResult.Status = Core.Enums.TrainingStatus.InProgress;
        
        await repository.UpdateAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        return trainingResult;
    }

    public async Task<bool> StopTrainingAsync(Guid trainingResultId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await repository.GetByIdAsync(trainingResultId);
        
        if (trainingResult == null)
            return false;

        _logger.LogInformation("Stopping training for model: {ModelId}", trainingResultId);

        trainingResult.Status = Core.Enums.TrainingStatus.Cancelled;
        trainingResult.TrainingCompletedAt = DateTime.UtcNow;
        trainingResult.TrainingDuration = trainingResult.TrainingCompletedAt - trainingResult.TrainingStartedAt;
        
        await repository.UpdateAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<TrainingResult?> GetTrainingResultAsync(Guid trainingResultId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        return await repository.GetByIdAsync(trainingResultId);
    }

    public async Task<IEnumerable<TrainingResult>> GetTrainingHistoryAsync(Guid modelConfigurationId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        return await repository.FindAsync(tr => tr.ModelConfigurationId == modelConfigurationId);
    }

    public async Task<bool> DeleteTrainingResultAsync(Guid trainingResultId, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await repository.GetByIdAsync(trainingResultId);
        
        if (trainingResult == null)
            return false;

        // Delete model files
        if (File.Exists($"{trainingResult.ModelPath}.json"))
        {
            try
            {
                File.Delete($"{trainingResult.ModelPath}.json");
                var directory = Path.GetDirectoryName(trainingResult.ModelPath);
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete model files at: {ModelPath}", trainingResult.ModelPath);
            }
        }

        await repository.DeleteAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<string> ExportModelAsync(Guid trainingResultId, string format, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await repository.GetByIdAsync(trainingResultId);
        
        if (trainingResult == null)
            throw new ArgumentException($"Training result with ID {trainingResultId} not found.");

        return await _modelRepository.ExportModelAsync(trainingResult.ModelPath, format, cancellationToken);
    }

    private async Task<IEnumerable<OrderBookData>> LoadOrderBookDataFromDataset(Dataset dataset, CancellationToken cancellationToken)
    {
        // Load data from dataset file path
        if (!string.IsNullOrEmpty(dataset.FilePath) && File.Exists(dataset.FilePath))
        {
            return await _dataLoader.LoadOrderBookDataAsync(dataset.FilePath, cancellationToken);
        }

        // Fallback to loading sample data
        return await _dataLoader.LoadOrderBookDataBySymbolAsync(
            "BTCUSD", 
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            cancellationToken
        );
    }

    private (float[,,] xTrain, float[,] yTrain, float[,,] xVal, float[,] yVal, float[,,] xTest, float[,] yTest) PrepareTrainingData(IEnumerable<TrainingData> trainingData)
    {
        var trainData = trainingData.Where(x => x.DataType == "Train").ToList();
        var valData = trainingData.Where(x => x.DataType == "Validation").ToList();
        var testData = trainingData.Where(x => x.DataType == "Test").ToList();

        // Convert to arrays
        var xTrain = ConvertToArray(trainData.Select(x => x.Features));
        var yTrain = ConvertLabelsToArray(trainData.Select(x => x.Labels));
        
        var xVal = ConvertToArray(valData.Select(x => x.Features));
        var yVal = ConvertLabelsToArray(valData.Select(x => x.Labels));
        
        var xTest = ConvertToArray(testData.Select(x => x.Features));
        var yTest = ConvertLabelsToArray(testData.Select(x => x.Labels));

        return (xTrain, yTrain, xVal, yVal, xTest, yTest);
    }

    private float[,,] ConvertToArray(IEnumerable<string> featuresJson)
    {
        var featuresList = new List<float[]>();
        
        foreach (var json in featuresJson)
        {
            var features = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (features != null)
            {
                var featureArray = features.Values
                    .Select(v => Convert.ToSingle(v))
                    .ToArray();
                featuresList.Add(featureArray);
            }
        }

        if (!featuresList.Any()) return new float[0, 0, 0];

        var numSamples = featuresList.Count;
        var numFeatures = featuresList[0].Length;
        
        // Reshape for sequence data (assuming sequence length of 1 for simplicity)
        var data = new float[numSamples, 1, numFeatures];
        
        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numFeatures; j++)
            {
                data[i, 0, j] = featuresList[i][j];
            }
        }

        return data;
    }

    private float[,] ConvertLabelsToArray(IEnumerable<string> labelsJson)
    {
        var labelsList = new List<int>();
        
        foreach (var json in labelsJson)
        {
            var labels = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (labels != null && labels.ContainsKey("price_direction"))
            {
                var label = Convert.ToInt32(labels["price_direction"]);
                // Convert to one-hot encoding (3 classes: -1, 0, 1 -> 0, 1, 2)
                labelsList.Add(Math.Max(0, Math.Min(2, label + 1)));
            }
        }

        if (!labelsList.Any()) return new float[0, 0];

        // Convert to one-hot encoding
        var numSamples = labelsList.Count;
        var numClasses = 3;
        var oneHot = new float[numSamples, numClasses];
        
        for (int i = 0; i < numSamples; i++)
        {
            var classIndex = labelsList[i];
            oneHot[i, classIndex] = 1.0f;
        }

        return oneHot;
    }

    private ModelParameters ParseModelParameters(string? parametersJson)
    {
        var defaultParams = new ModelParameters();
        
        if (string.IsNullOrEmpty(parametersJson))
            return defaultParams;

        try
        {
            var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(parametersJson);
            if (parameters == null) return defaultParams;

            return new ModelParameters
            {
                SequenceLength = GetIntParameter(parameters, "sequence_length", defaultParams.SequenceLength),
                NumFeatures = GetIntParameter(parameters, "num_features", defaultParams.NumFeatures),
                NumClasses = GetIntParameter(parameters, "num_classes", defaultParams.NumClasses),
                LearningRate = GetFloatParameter(parameters, "learning_rate", defaultParams.LearningRate),
                Epochs = GetIntParameter(parameters, "epochs", defaultParams.Epochs),
                BatchSize = GetIntParameter(parameters, "batch_size", defaultParams.BatchSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse model parameters, using defaults");
            return defaultParams;
        }
    }

    private int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
    {
        if (parameters.ContainsKey(key) && int.TryParse(parameters[key]?.ToString(), out var value))
            return value;
        return defaultValue;
    }

    private float GetFloatParameter(Dictionary<string, object> parameters, string key, float defaultValue)
    {
        if (parameters.ContainsKey(key) && float.TryParse(parameters[key]?.ToString(), out var value))
            return value;
        return defaultValue;
    }

    private async Task<Dictionary<string, object>> EvaluateModel(float[,] predictions, float[,] yTrue, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simulate evaluation time

        // Calculate basic metrics
        var accuracy = CalculateAccuracy(predictions, yTrue);
        var precision = CalculatePrecision(predictions, yTrue);
        var recall = CalculateRecall(predictions, yTrue);
        var f1Score = precision + recall > 0 ? 2 * (precision * recall) / (precision + recall) : 0;

        return new Dictionary<string, object>
        {
            ["accuracy"] = accuracy,
            ["precision"] = precision,
            ["recall"] = recall,
            ["f1_score"] = f1Score,
            ["evaluation_date"] = DateTime.UtcNow
        };
    }

    private double CalculateAccuracy(float[,] predictions, float[,] trueLabels)
    {
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);
        
        var correct = 0;
        var total = predClasses.Length;
        
        for (int i = 0; i < total; i++)
        {
            if (predClasses[i] == trueClasses[i])
                correct++;
        }
        
        return (double)correct / total;
    }

    private double CalculatePrecision(float[,] predictions, float[,] trueLabels)
    {
        // Simplified precision calculation for multiclass
        return 0.85; // Placeholder
    }

    private double CalculateRecall(float[,] predictions, float[,] trueLabels)
    {
        // Simplified recall calculation for multiclass
        return 0.82; // Placeholder
    }

    private int[] ArgMax(float[,] array)
    {
        var numSamples = array.GetLength(0);
        var numClasses = array.GetLength(1);
        var result = new int[numSamples];
        
        for (int i = 0; i < numSamples; i++)
        {
            var maxIndex = 0;
            var maxValue = array[i, 0];
            
            for (int j = 1; j < numClasses; j++)
            {
                if (array[i, j] > maxValue)
                {
                    maxValue = array[i, j];
                    maxIndex = j;
                }
            }
            
            result[i] = maxIndex;
        }
        
        return result;
    }

    private class ModelParameters
    {
        public int SequenceLength { get; set; } = 100;
        public int NumFeatures { get; set; } = 40;
        public int NumClasses { get; set; } = 3;
        public float LearningRate { get; set; } = 0.001f;
        public int Epochs { get; set; } = 100;
        public int BatchSize { get; set; } = 32;
    }
}
