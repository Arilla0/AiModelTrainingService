
using AiModelTrainingService.Core.Entities;
using AiModelTrainingService.Core.Interfaces;
using AiModelTrainingService.Services.Models;
using AiModelTrainingService.Services.Optimizers;
using AiModelTrainingService.Services.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AiModelTrainingService.Services.Implementations;

public class TrainingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataLoader _dataLoader;
    private readonly IFeatureEngineering _featureEngineering;
    private readonly IModelRepository _modelRepository;
    private readonly ILogger<TrainingService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Guid, CancellationTokenSource> _trainingCancellationTokens = new();

    public TrainingService(
        IUnitOfWork unitOfWork,
        IDataLoader dataLoader,
        IFeatureEngineering featureEngineering,
        IModelRepository modelRepository,
        ILogger<TrainingService> logger,
        ILoggerFactory loggerFactory)
    {
        _unitOfWork = unitOfWork;
        _dataLoader = dataLoader;
        _featureEngineering = featureEngineering;
        _modelRepository = modelRepository;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<TrainingResult> TrainModelAsync(
        Guid modelConfigurationId,
        Guid datasetId,
        TrainingConfiguration trainingConfig,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting advanced training for model configuration: {ModelConfigurationId}", modelConfigurationId);

        var trainingResult = new TrainingResult
        {
            Id = Guid.NewGuid(),
            ModelConfigurationId = modelConfigurationId,
            TrainingJobId = Guid.NewGuid(),
            ModelVersion = GenerateModelVersion(),
            ModelPath = $"/models/{modelConfigurationId}/{Guid.NewGuid()}",
            Status = Core.Enums.TrainingStatus.InProgress,
            TrainingStartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TotalEpochs = trainingConfig.Epochs
        };

        var repository = _unitOfWork.Repository<TrainingResult>();
        await repository.AddAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        // Register cancellation token for this training
        var trainingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _trainingCancellationTokens[trainingResult.Id] = trainingCts;

        try
        {
            // Load configuration and data
            var modelConfig = await LoadModelConfiguration(modelConfigurationId);
            var dataset = await LoadDataset(datasetId);
            var orderBookData = await LoadOrderBookDataFromDataset(dataset, trainingCts.Token);
            var trainingData = await _featureEngineering.ExtractFeaturesAsync(orderBookData, modelConfig, trainingCts.Token);

            // Prepare training data
            var (xTrain, yTrain, xVal, yVal, xTest, yTest) = PrepareTrainingData(trainingData);

            // Create model with advanced configuration
            var modelParams = ParseModelParameters(modelConfig.Hyperparameters);
            var deepLobModel = new DeepLOBModel(
                sequenceLength: modelParams.SequenceLength,
                numFeatures: modelParams.NumFeatures,
                numClasses: modelParams.NumClasses,
                learningRate: modelParams.LearningRate,
                logger: _loggerFactory.CreateLogger<DeepLOBModel>()
            );

            deepLobModel.BuildModel();

            // Initialize training components
            var optimizer = CreateOptimizer(trainingConfig.OptimizerType, trainingConfig.LearningRate);
            var earlyStopping = new EarlyStopping(
                patience: trainingConfig.EarlyStoppingPatience,
                minDelta: trainingConfig.EarlyStoppingMinDelta,
                monitor: trainingConfig.EarlyStoppingMonitor
            );

            var checkpointManager = new CheckpointManager(trainingResult.ModelPath, trainingConfig.SaveBestOnly);

            // Training loop with advanced features
            var bestValLoss = float.MaxValue;
            var epochsWithoutImprovement = 0;

            for (int epoch = 0; epoch < trainingConfig.Epochs; epoch++)
            {
                trainingCts.Token.ThrowIfCancellationRequested();

                // Training step
                var (trainLoss, trainMetrics) = await TrainEpoch(
                    deepLobModel, xTrain, yTrain, optimizer, trainingConfig.BatchSize, trainingCts.Token);

                // Validation step
                var (valLoss, valMetrics) = await ValidateEpoch(
                    deepLobModel, xVal, yVal, trainingConfig.BatchSize, trainingCts.Token);

                // Log progress
                _logger.LogInformation(
                    "Epoch {Epoch}/{TotalEpochs} - train_loss: {TrainLoss:F4} - val_loss: {ValLoss:F4} - train_acc: {TrainAcc:F4} - val_acc: {ValAcc:F4}",
                    epoch + 1, trainingConfig.Epochs, trainLoss, valLoss, trainMetrics.Accuracy, valMetrics.Accuracy);

                // Save metrics
                await SaveEpochMetrics(trainingResult.Id, epoch, trainMetrics, valMetrics, trainingCts.Token);

                // Checkpoint management
                var isImprovement = valLoss < bestValLoss - trainingConfig.EarlyStoppingMinDelta;
                if (isImprovement)
                {
                    bestValLoss = valLoss;
                    epochsWithoutImprovement = 0;
                    await checkpointManager.SaveCheckpoint(deepLobModel, epoch, valLoss, trainMetrics, valMetrics);
                }
                else
                {
                    epochsWithoutImprovement++;
                }

                // Early stopping check
                if (earlyStopping.ShouldStop(valLoss))
                {
                    _logger.LogInformation("Early stopping triggered at epoch {Epoch}", epoch + 1);
                    trainingResult.EpochsCompleted = epoch + 1;
                    break;
                }

                // Learning rate scheduling
                if (trainingConfig.UseLearningRateScheduling)
                {
                    optimizer.UpdateLearningRate(epoch, valLoss);
                }

                trainingResult.EpochsCompleted = epoch + 1;
                await repository.UpdateAsync(trainingResult);
            }

            // Load best model from checkpoint
            await checkpointManager.LoadBestCheckpoint(deepLobModel);

            // Final evaluation
            var testMetrics = await EvaluateModel(deepLobModel, xTest, yTest, trainingCts.Token);

            // Save final model
            Directory.CreateDirectory(Path.GetDirectoryName(trainingResult.ModelPath)!);
            deepLobModel.SaveModel(trainingResult.ModelPath);

            // Update training result
            trainingResult.Status = Core.Enums.TrainingStatus.Completed;
            trainingResult.TrainingCompletedAt = DateTime.UtcNow;
            trainingResult.TrainingDuration = trainingResult.TrainingCompletedAt - trainingResult.TrainingStartedAt;
            trainingResult.ModelArtifacts = JsonConvert.SerializeObject(new
            {
                BestEpoch = checkpointManager.BestEpoch,
                BestValLoss = bestValLoss,
                TestMetrics = testMetrics,
                ModelSummary = deepLobModel.GetModelSummary(),
                TrainingConfig = trainingConfig
            });

            await repository.UpdateAsync(trainingResult);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Training completed successfully for model: {ModelId}", trainingResult.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Training was cancelled for model: {ModelId}", trainingResult.Id);
            trainingResult.Status = Core.Enums.TrainingStatus.Cancelled;
            await UpdateTrainingResultOnError(trainingResult, "Training was cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training failed for model: {ModelId}", trainingResult.Id);
            trainingResult.Status = Core.Enums.TrainingStatus.Failed;
            await UpdateTrainingResultOnError(trainingResult, ex.Message);
        }
        finally
        {
            _trainingCancellationTokens.Remove(trainingResult.Id);
        }

        return trainingResult;
    }

    public async Task<bool> StopTrainingAsync(Guid trainingResultId)
    {
        if (_trainingCancellationTokens.TryGetValue(trainingResultId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Training cancellation requested for model: {ModelId}", trainingResultId);
            return true;
        }

        return false;
    }

    public async Task<TrainingResult> ResumeTrainingAsync(Guid trainingResultId, TrainingConfiguration trainingConfig, CancellationToken cancellationToken = default)
    {
        var repository = _unitOfWork.Repository<TrainingResult>();
        var trainingResult = await repository.GetByIdAsync(trainingResultId);

        if (trainingResult == null)
            throw new ArgumentException($"Training result with ID {trainingResultId} not found.");

        _logger.LogInformation("Resuming training for model: {ModelId}", trainingResultId);

        // Load existing model
        var deepLobModel = new DeepLOBModel(logger: _loggerFactory.CreateLogger<DeepLOBModel>());
        deepLobModel.LoadModel(trainingResult.ModelPath);

        // Continue training from last checkpoint
        var checkpointManager = new CheckpointManager(trainingResult.ModelPath, trainingConfig.SaveBestOnly);
        var lastCheckpoint = await checkpointManager.GetLastCheckpoint();

        trainingResult.Status = Core.Enums.TrainingStatus.InProgress;
        await repository.UpdateAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();

        // Resume training logic would go here...
        // For brevity, returning the updated result
        return trainingResult;
    }

    private IOptimizer CreateOptimizer(OptimizerType optimizerType, float learningRate)
    {
        return optimizerType switch
        {
            OptimizerType.Adam => new AdamOptimizer(learningRate),
            OptimizerType.SGD => new SGDOptimizer(learningRate),
            OptimizerType.RMSprop => new RMSpropOptimizer(learningRate),
            OptimizerType.AdaGrad => new AdaGradOptimizer(learningRate),
            _ => new AdamOptimizer(learningRate)
        };
    }

    private async Task<(float loss, EpochMetrics metrics)> TrainEpoch(
        DeepLOBModel model, float[,,] xTrain, float[,] yTrain,
        IOptimizer optimizer, int batchSize, CancellationToken cancellationToken)
    {
        var numSamples = xTrain.GetLength(0);
        var numBatches = (int)Math.Ceiling((double)numSamples / batchSize);
        var totalLoss = 0f;
        var correct = 0;

        for (int batch = 0; batch < numBatches; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startIdx = batch * batchSize;
            var endIdx = Math.Min(startIdx + batchSize, numSamples);
            var currentBatchSize = endIdx - startIdx;

            // Extract batch data
            var xBatch = ExtractBatch(xTrain, startIdx, currentBatchSize);
            var yBatch = ExtractBatch(yTrain, startIdx, currentBatchSize);

            // Forward pass and loss calculation
            var predictions = model.Predict(xBatch);
            var loss = CalculateLoss(predictions, yBatch);
            totalLoss += loss;

            // Calculate accuracy for this batch
            correct += CalculateCorrectPredictions(predictions, yBatch);

            // Simulate optimizer step
            optimizer.Step();
        }

        var avgLoss = totalLoss / numBatches;
        var accuracy = (double)correct / numSamples;

        return (avgLoss, new EpochMetrics { Loss = avgLoss, Accuracy = accuracy });
    }

    private async Task<(float loss, EpochMetrics metrics)> ValidateEpoch(
        DeepLOBModel model, float[,,] xVal, float[,] yVal,
        int batchSize, CancellationToken cancellationToken)
    {
        var predictions = model.Predict(xVal);
        var loss = CalculateLoss(predictions, yVal);
        var accuracy = CalculateAccuracy(predictions, yVal);

        return (loss, new EpochMetrics { Loss = loss, Accuracy = accuracy });
    }

    private async Task<EpochMetrics> EvaluateModel(DeepLOBModel model, float[,,] xTest, float[,] yTest, CancellationToken cancellationToken)
    {
        var predictions = model.Predict(xTest);
        var accuracy = CalculateAccuracy(predictions, yTest);
        var precision = CalculatePrecision(predictions, yTest);
        var recall = CalculateRecall(predictions, yTest);
        var f1Score = precision + recall > 0 ? 2 * (precision * recall) / (precision + recall) : 0;

        return new EpochMetrics
        {
            Accuracy = accuracy,
            Precision = precision,
            Recall = recall,
            F1Score = f1Score
        };
    }

    private async Task SaveEpochMetrics(Guid trainingResultId, int epoch, EpochMetrics trainMetrics, EpochMetrics valMetrics, CancellationToken cancellationToken)
    {
        var metricsRepository = _unitOfWork.Repository<EvaluationMetrics>();

        var trainEvalMetrics = new EvaluationMetrics
        {
            Id = Guid.NewGuid(),
            TrainingResultId = trainingResultId,
            MetricType = "Training",
            Accuracy = trainMetrics.Accuracy,
            Precision = trainMetrics.Precision,
            Recall = trainMetrics.Recall,
            F1Score = trainMetrics.F1Score,
            Loss = trainMetrics.Loss,
            Epoch = epoch,
            RecordedAt = DateTime.UtcNow
        };

        var valEvalMetrics = new EvaluationMetrics
        {
            Id = Guid.NewGuid(),
            TrainingResultId = trainingResultId,
            MetricType = "Validation",
            Accuracy = valMetrics.Accuracy,
            Precision = valMetrics.Precision,
            Recall = valMetrics.Recall,
            F1Score = valMetrics.F1Score,
            Loss = valMetrics.Loss,
            ValidationLoss = valMetrics.Loss,
            Epoch = epoch,
            RecordedAt = DateTime.UtcNow
        };

        await metricsRepository.AddAsync(trainEvalMetrics);
        await metricsRepository.AddAsync(valEvalMetrics);
        await _unitOfWork.SaveChangesAsync();
    }

    // Helper methods
    private string GenerateModelVersion() => $"1.{DateTime.UtcNow:yyyyMMdd}.{DateTime.UtcNow:HHmmss}";

    private async Task<ModelConfiguration> LoadModelConfiguration(Guid modelConfigurationId)
    {
        var repository = _unitOfWork.Repository<ModelConfiguration>();
        var config = await repository.GetByIdAsync(modelConfigurationId);
        return config ?? throw new ArgumentException($"Model configuration with ID {modelConfigurationId} not found.");
    }

    private async Task<Dataset> LoadDataset(Guid datasetId)
    {
        var repository = _unitOfWork.Repository<Dataset>();
        var dataset = await repository.GetByIdAsync(datasetId);
        return dataset ?? throw new ArgumentException($"Dataset with ID {datasetId} not found.");
    }

    private async Task<IEnumerable<OrderBookData>> LoadOrderBookDataFromDataset(Dataset dataset, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(dataset.FilePath) && File.Exists(dataset.FilePath))
        {
            return await _dataLoader.LoadOrderBookDataAsync(dataset.FilePath, cancellationToken);
        }

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
                var featureArray = features.Values.Select(v => Convert.ToSingle(v)).ToArray();
                featuresList.Add(featureArray);
            }
        }

        if (!featuresList.Any()) return new float[0, 0, 0];

        var numSamples = featuresList.Count;
        var numFeatures = featuresList[0].Length;
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
                labelsList.Add(Math.Max(0, Math.Min(2, label + 1)));
            }
        }

        if (!labelsList.Any()) return new float[0, 0];

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

    private async Task UpdateTrainingResultOnError(TrainingResult trainingResult, string errorMessage)
    {
        trainingResult.TrainingCompletedAt = DateTime.UtcNow;
        trainingResult.TrainingDuration = trainingResult.TrainingCompletedAt - trainingResult.TrainingStartedAt;
        trainingResult.ErrorMessage = errorMessage;

        var repository = _unitOfWork.Repository<TrainingResult>();
        await repository.UpdateAsync(trainingResult);
        await _unitOfWork.SaveChangesAsync();
    }

    // Utility methods for calculations
    private float[,,] ExtractBatch(float[,,] data, int startIdx, int batchSize)
    {
        var seqLen = data.GetLength(1);
        var numFeatures = data.GetLength(2);
        var batch = new float[batchSize, seqLen, numFeatures];

        for (int i = 0; i < batchSize; i++)
        {
            for (int j = 0; j < seqLen; j++)
            {
                for (int k = 0; k < numFeatures; k++)
                {
                    batch[i, j, k] = data[startIdx + i, j, k];
                }
            }
        }

        return batch;
    }

    private float[,] ExtractBatch(float[,] data, int startIdx, int batchSize)
    {
        var numClasses = data.GetLength(1);
        var batch = new float[batchSize, numClasses];

        for (int i = 0; i < batchSize; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                batch[i, j] = data[startIdx + i, j];
            }
        }

        return batch;
    }

    private float CalculateLoss(float[,] predictions, float[,] trueLabels)
    {
        var numSamples = predictions.GetLength(0);
        var numClasses = predictions.GetLength(1);
        var loss = 0f;

        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                if (trueLabels[i, j] > 0)
                {
                    loss -= trueLabels[i, j] * (float)Math.Log(Math.Max(predictions[i, j], 1e-7));
                }
            }
        }

        return loss / numSamples;
    }

    private int CalculateCorrectPredictions(float[,] predictions, float[,] trueLabels)
    {
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);

        var correct = 0;
        for (int i = 0; i < predClasses.Length; i++)
        {
            if (predClasses[i] == trueClasses[i])
                correct++;
        }

        return correct;
    }

    private double CalculateAccuracy(float[,] predictions, float[,] trueLabels)
    {
        var correct = CalculateCorrectPredictions(predictions, trueLabels);
        return (double)correct / predictions.GetLength(0);
    }

    private double CalculatePrecision(float[,] predictions, float[,] trueLabels)
    {
        // Simplified multiclass precision calculation
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);
        var numClasses = predictions.GetLength(1);

        var classPrecisions = new List<double>();

        for (int c = 0; c < numClasses; c++)
        {
            var truePositives = 0;
            var falsePositives = 0;

            for (int i = 0; i < predClasses.Length; i++)
            {
                if (predClasses[i] == c)
                {
                    if (trueClasses[i] == c)
                        truePositives++;
                    else
                        falsePositives++;
                }
            }

            if (truePositives + falsePositives > 0)
                classPrecisions.Add((double)truePositives / (truePositives + falsePositives));
        }

        return classPrecisions.Any() ? classPrecisions.Average() : 0.0;
    }

    private double CalculateRecall(float[,] predictions, float[,] trueLabels)
    {
        // Simplified multiclass recall calculation
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);
        var numClasses = predictions.GetLength(1);

        var classRecalls = new List<double>();

        for (int c = 0; c < numClasses; c++)
        {
            var truePositives = 0;
            var falseNegatives = 0;

            for (int i = 0; i < trueClasses.Length; i++)
            {
                if (trueClasses[i] == c)
                {
                    if (predClasses[i] == c)
                        truePositives++;
                    else
                        falseNegatives++;
                }
            }

            if (truePositives + falseNegatives > 0)
                classRecalls.Add((double)truePositives / (truePositives + falseNegatives));
        }

        return classRecalls.Any() ? classRecalls.Average() : 0.0;
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

    private class EpochMetrics
    {
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double Loss { get; set; }
    }
}

// Supporting classes for training configuration
public class TrainingConfiguration
{
    public int Epochs { get; set; } = 100;
    public int BatchSize { get; set; } = 32;
    public float LearningRate { get; set; } = 0.001f;
    public OptimizerType OptimizerType { get; set; } = OptimizerType.Adam;
    public bool UseLearningRateScheduling { get; set; } = true;
    public int EarlyStoppingPatience { get; set; } = 10;
    public float EarlyStoppingMinDelta { get; set; } = 0.001f;
    public string EarlyStoppingMonitor { get; set; } = "val_loss";
    public bool SaveBestOnly { get; set; } = true;
    public int CheckpointFrequency { get; set; } = 5;
}

public enum OptimizerType
{
    Adam,
    SGD,
    RMSprop,
    AdaGrad
}
