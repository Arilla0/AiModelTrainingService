
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Services.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoTradingBot.Services.Implementations;

public class EvaluationService : IModelEvaluator
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EvaluationService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public EvaluationService(IUnitOfWork unitOfWork, ILogger<EvaluationService> logger, ILoggerFactory loggerFactory)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<EvaluationMetrics> EvaluateModelAsync(TrainingResult trainingResult, IEnumerable<TrainingData> testData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating model: {ModelId}", trainingResult.Id);

        var model = new DeepLOBModel(logger: _loggerFactory.CreateLogger<DeepLOBModel>());
        model.LoadModel(trainingResult.ModelPath);

        var (xTest, yTest) = PrepareTestData(testData);
        var predictions = model.Predict(xTest);

        var metrics = CalculateMetrics(predictions, yTest);
        var tradingMetrics = await CalculateTradingMetrics(predictions, yTest, testData, cancellationToken);

        var evaluationMetrics = new EvaluationMetrics
        {
            Id = Guid.NewGuid(),
            TrainingResultId = trainingResult.Id,
            MetricType = "Test",
            Accuracy = metrics.Accuracy,
            Precision = metrics.Precision,
            Recall = metrics.Recall,
            F1Score = metrics.F1Score,
            MeanSquaredError = metrics.MeanSquaredError,
            MeanAbsoluteError = metrics.MeanAbsoluteError,
            RootMeanSquaredError = metrics.RootMeanSquaredError,
            R2Score = metrics.R2Score,
            CustomMetrics = JsonConvert.SerializeObject(tradingMetrics),
            RecordedAt = DateTime.UtcNow
        };

        var repository = _unitOfWork.Repository<EvaluationMetrics>();
        await repository.AddAsync(evaluationMetrics);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Model evaluation completed. Accuracy: {Accuracy:F4}, Sharpe Ratio: {SharpeRatio:F4}", 
            metrics.Accuracy, tradingMetrics.SharpeRatio);

        return evaluationMetrics;
    }

    public async Task<IEnumerable<PredictionResult>> MakePredictionsAsync(string modelPath, IEnumerable<TrainingData> inputData, CancellationToken cancellationToken = default)
    {
        var model = new DeepLOBModel(logger: _loggerFactory.CreateLogger<DeepLOBModel>());
        model.LoadModel(modelPath);

        var (xData, _) = PrepareTestData(inputData);
        var predictions = model.Predict(xData);

        var results = new List<PredictionResult>();
        var inputList = inputData.ToList();

        for (int i = 0; i < predictions.GetLength(0); i++)
        {
            var predictionProbs = new float[predictions.GetLength(1)];
            for (int j = 0; j < predictions.GetLength(1); j++)
            {
                predictionProbs[j] = predictions[i, j];
            }

            var predictedClass = Array.IndexOf(predictionProbs, predictionProbs.Max());
            var confidence = predictionProbs.Max();

            // Convert class to price direction (-1, 0, 1)
            var priceDirection = predictedClass - 1;

            results.Add(new PredictionResult
            {
                Id = Guid.NewGuid(),
                PredictedValue = priceDirection,
                Confidence = confidence,
                PredictedAt = DateTime.UtcNow,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["class_probabilities"] = predictionProbs,
                    ["predicted_class"] = predictedClass,
                    ["input_features"] = inputList[i].Features
                }
            });
        }

        return results;
    }

    public async Task<PredictionResult> MakeSinglePredictionAsync(string modelPath, TrainingData inputData, CancellationToken cancellationToken = default)
    {
        var predictions = await MakePredictionsAsync(modelPath, new[] { inputData }, cancellationToken);
        return predictions.First();
    }

    public async Task<ModelPerformanceReport> GeneratePerformanceReportAsync(TrainingResult trainingResult, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating performance report for model: {ModelId}", trainingResult.Id);

        var metricsRepository = _unitOfWork.Repository<EvaluationMetrics>();
        var allMetrics = await metricsRepository.FindAsync(m => m.TrainingResultId == trainingResult.Id);

        var trainingMetrics = allMetrics.Where(m => m.MetricType == "Training").OrderBy(m => m.Epoch).ToList();
        var validationMetrics = allMetrics.Where(m => m.MetricType == "Validation").OrderBy(m => m.Epoch).ToList();
        var testMetrics = allMetrics.FirstOrDefault(m => m.MetricType == "Test");

        var report = new ModelPerformanceReport
        {
            TrainingResultId = trainingResult.Id,
            TrainingMetrics = trainingMetrics.LastOrDefault() ?? new EvaluationMetrics(),
            ValidationMetrics = validationMetrics.LastOrDefault() ?? new EvaluationMetrics(),
            TestMetrics = testMetrics,
            EpochMetrics = allMetrics.Where(m => m.MetricType != "Test"),
            Summary = GeneratePerformanceSummary(trainingResult, trainingMetrics, validationMetrics, testMetrics),
            GeneratedAt = DateTime.UtcNow
        };

        return report;
    }

    public async Task<bool> ValidateModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonPath = $"{modelPath}.json";
            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("Model file not found: {ModelPath}", jsonPath);
                return false;
            }

            var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var modelData = JsonConvert.DeserializeObject<dynamic>(json);

            if (modelData == null)
            {
                _logger.LogWarning("Invalid model data in file: {ModelPath}", jsonPath);
                return false;
            }

            // Validate required fields
            var requiredFields = new[] { "Architecture", "Weights", "IsTrained" };
            foreach (var field in requiredFields)
            {
                if (modelData[field] == null)
                {
                    _logger.LogWarning("Missing required field '{Field}' in model: {ModelPath}", field, jsonPath);
                    return false;
                }
            }

            _logger.LogInformation("Model validation successful: {ModelPath}", modelPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model validation failed: {ModelPath}", modelPath);
            return false;
        }
    }

    public async Task<ModelComparisonResult> CompareModelsAsync(IEnumerable<TrainingResult> trainingResults, IEnumerable<TrainingData> testData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing {ModelCount} models", trainingResults.Count());

        var comparisons = new List<ModelComparisonItem>();

        foreach (var trainingResult in trainingResults)
        {
            try
            {
                var metrics = await EvaluateModelAsync(trainingResult, testData, cancellationToken);
                
                comparisons.Add(new ModelComparisonItem
                {
                    TrainingResultId = trainingResult.Id,
                    ModelName = $"Model_{trainingResult.ModelVersion}",
                    Metrics = metrics,
                    Rank = 0 // Will be set after sorting
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate model {ModelId} for comparison", trainingResult.Id);
            }
        }

        // Rank models by accuracy (can be changed to other metrics)
        var rankedComparisons = comparisons
            .OrderByDescending(c => c.Metrics.Accuracy ?? 0)
            .Select((c, index) => { c.Rank = index + 1; return c; })
            .ToList();

        var result = new ModelComparisonResult
        {
            Comparisons = rankedComparisons,
            BestModel = rankedComparisons.FirstOrDefault() ?? new ModelComparisonItem(),
            ComparisonMetric = "Accuracy",
            ComparedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Model comparison completed. Best model: {BestModelId} with accuracy: {Accuracy:F4}", 
            result.BestModel.TrainingResultId, result.BestModel.Metrics.Accuracy);

        return result;
    }

    public async Task<CrossValidationResult> PerformCrossValidationAsync(ModelConfiguration configuration, IEnumerable<TrainingData> data, int folds = 5, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing {Folds}-fold cross-validation", folds);

        var dataList = data.ToList();
        var foldSize = dataList.Count / folds;
        var foldScores = new List<double>();

        for (int fold = 0; fold < folds; fold++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Processing fold {Fold}/{TotalFolds}", fold + 1, folds);

            // Split data into train and validation for this fold
            var validationStart = fold * foldSize;
            var validationEnd = (fold == folds - 1) ? dataList.Count : (fold + 1) * foldSize;
            
            var validationData = dataList.Skip(validationStart).Take(validationEnd - validationStart).ToList();
            var trainData = dataList.Take(validationStart).Concat(dataList.Skip(validationEnd)).ToList();

            // Train model on fold training data
            var (xTrain, yTrain) = PrepareTestData(trainData);
            var (xVal, yVal) = PrepareTestData(validationData);

            var model = new DeepLOBModel(
                sequenceLength: 100,
                numFeatures: 40,
                numClasses: 3,
                learningRate: 0.001f,
                logger: _loggerFactory.CreateLogger<DeepLOBModel>()
            );

            model.BuildModel();
            model.Train(xTrain, yTrain, xVal, yVal, epochs: 50, batchSize: 32, verbose: false);

            // Evaluate on validation set
            var predictions = model.Predict(xVal);
            var accuracy = CalculateAccuracy(predictions, yVal);
            foldScores.Add(accuracy);

            _logger.LogInformation("Fold {Fold} accuracy: {Accuracy:F4}", fold + 1, accuracy);
        }

        var meanScore = foldScores.Average();
        var stdDev = Math.Sqrt(foldScores.Select(x => Math.Pow(x - meanScore, 2)).Average());

        var result = new CrossValidationResult
        {
            MeanScore = meanScore,
            StandardDeviation = stdDev,
            FoldScores = foldScores,
            Metric = "Accuracy",
            Folds = folds
        };

        _logger.LogInformation("Cross-validation completed. Mean accuracy: {MeanAccuracy:F4} ± {StdDev:F4}", 
            meanScore, stdDev);

        return result;
    }

    public async Task<BacktestResult> PerformBacktestAsync(string modelPath, IEnumerable<TrainingData> historicalData, BacktestConfiguration config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing backtest with {DataPoints} data points", historicalData.Count());

        var model = new DeepLOBModel(logger: _loggerFactory.CreateLogger<DeepLOBModel>());
        model.LoadModel(modelPath);

        var dataList = historicalData.OrderBy(d => d.CreatedAt).ToList();
        var trades = new List<Trade>();
        var initialPrice = Convert.ToDouble(JsonConvert.DeserializeObject<Dictionary<string, object>>(dataList.First().Features)?["mid_price"] ?? 100.0);
        var portfolio = new Portfolio(config.InitialCapital, initialPrice);
        var currentPosition = 0.0; // -1: short, 0: neutral, 1: long

        for (int i = config.LookbackPeriod; i < dataList.Count(); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get prediction for current data point
            var currentData = dataList[i];
            var prediction = await MakeSinglePredictionAsync(modelPath, currentData, cancellationToken);
            
            // Extract price from features (assuming it's available)
            var features = JsonConvert.DeserializeObject<Dictionary<string, object>>(currentData.Features);
            var currentPrice = Convert.ToDouble(features?["mid_price"] ?? 100.0);

            // Trading logic based on prediction
            var predictedDirection = prediction.PredictedValue;
            var confidence = prediction.Confidence;

            if (confidence > config.ConfidenceThreshold)
            {
                var targetPosition = (double)predictedDirection;
                var positionChange = targetPosition - currentPosition;

                if (Math.Abs(positionChange) > 0.5) // Significant position change
                {
                    var trade = new Trade
                    {
                        Timestamp = currentData.CreatedAt,
                        Price = currentPrice,
                        Quantity = positionChange * config.PositionSize,
                        Direction = positionChange > 0 ? "BUY" : "SELL",
                        Confidence = confidence
                    };

                    trades.Add(trade);
                    portfolio.ExecuteTrade(trade);
                    currentPosition = targetPosition;
                    // Update portfolio value
                    portfolio.UpdateValue(currentPrice, currentPosition * config.PositionSize);
                }
            }
        }

        var backtestMetrics = CalculateBacktestMetrics(portfolio, trades, config);

        var result = new BacktestResult
        {
            StartDate = dataList.First().CreatedAt,
            EndDate = dataList.Last().CreatedAt,
            InitialCapital = config.InitialCapital,
            FinalValue = portfolio.CurrentValue,
            TotalReturn = (portfolio.CurrentValue - config.InitialCapital) / config.InitialCapital,
            TotalTrades = trades.Count,
            WinningTrades = trades.Count(t => t.PnL > 0),
            LosingTrades = trades.Count(t => t.PnL < 0),
            SharpeRatio = backtestMetrics.SharpeRatio,
            MaxDrawdown = backtestMetrics.MaxDrawdown,
            Trades = trades,
            PortfolioHistory = portfolio.ValueHistory,
            Metrics = backtestMetrics
        };

        _logger.LogInformation("Backtest completed. Total return: {TotalReturn:P2}, Sharpe ratio: {SharpeRatio:F2}, Max drawdown: {MaxDrawdown:P2}", 
            result.TotalReturn, result.SharpeRatio, result.MaxDrawdown);

        return result;
    }

    // Helper methods
    private (float[,,] x, float[,] y) PrepareTestData(IEnumerable<TrainingData> testData)
    {
        var dataList = testData.ToList();
        var featuresList = new List<float[]>();
        var labelsList = new List<int>();

        foreach (var data in dataList)
        {
            var features = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.Features);
            if (features != null)
            {
                var featureArray = features.Values.Select(v => Convert.ToSingle(v)).ToArray();
                featuresList.Add(featureArray);
            }

            var labels = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.Labels);
            if (labels != null && labels.ContainsKey("price_direction"))
            {
                var label = Convert.ToInt32(labels["price_direction"]);
                labelsList.Add(Math.Max(0, Math.Min(2, label + 1)));
            }
        }

        // Convert to arrays
        var numSamples = featuresList.Count;
        var numFeatures = featuresList.Any() ? featuresList[0].Length : 0;
        var x = new float[numSamples, 1, numFeatures];
        
        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numFeatures; j++)
            {
                x[i, 0, j] = featuresList[i][j];
            }
        }

        // Convert labels to one-hot
        var numClasses = 3;
        var y = new float[numSamples, numClasses];
        for (int i = 0; i < numSamples; i++)
        {
            if (i < labelsList.Count)
            {
                y[i, labelsList[i]] = 1.0f;
            }
        }

        return (x, y);
    }

    private ModelMetrics CalculateMetrics(float[,] predictions, float[,] trueLabels)
    {
        var accuracy = CalculateAccuracy(predictions, trueLabels);
        var precision = CalculatePrecision(predictions, trueLabels);
        var recall = CalculateRecall(predictions, trueLabels);
        var f1Score = precision + recall > 0 ? 2 * (precision * recall) / (precision + recall) : 0;

        // For regression-like metrics, we'll use simplified calculations
        var mse = CalculateMSE(predictions, trueLabels);
        var mae = CalculateMAE(predictions, trueLabels);
        var rmse = Math.Sqrt(mse);
        var r2 = CalculateR2(predictions, trueLabels);

        return new ModelMetrics
        {
            Accuracy = accuracy,
            Precision = precision,
            Recall = recall,
            F1Score = f1Score,
            MeanSquaredError = mse,
            MeanAbsoluteError = mae,
            RootMeanSquaredError = rmse,
            R2Score = r2
        };
    }

    private async Task<TradingMetrics> CalculateTradingMetrics(float[,] predictions, float[,] trueLabels, IEnumerable<TrainingData> testData, CancellationToken cancellationToken)
    {
        // Simulate trading based on predictions
        var returns = SimulateTradingReturns(predictions, trueLabels, testData);
        
        var sharpeRatio = CalculateSharpeRatio(returns);
        var maxDrawdown = CalculateMaxDrawdown(returns);
        var volatility = CalculateVolatility(returns);
        var winRate = CalculateWinRate(returns);

        return new TradingMetrics
        {
            SharpeRatio = sharpeRatio,
            MaxDrawdown = maxDrawdown,
            Volatility = volatility,
            WinRate = winRate,
            TotalReturn = returns.Sum(),
            AverageReturn = returns.Average(),
            NumberOfTrades = returns.Count
        };
    }

    private List<double> SimulateTradingReturns(float[,] predictions, float[,] trueLabels, IEnumerable<TrainingData> testData)
    {
        var returns = new List<double>();
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);

        for (int i = 0; i < predClasses.Length; i++)
        {
            // Convert class to direction: 0->-1, 1->0, 2->1
            var predictedDirection = predClasses[i] - 1;
            var actualDirection = trueClasses[i] - 1;

            // Simulate return based on prediction accuracy
            var baseReturn = 0.001; // 0.1% base return
            var returnMultiplier = predictedDirection * actualDirection; // 1 if correct direction, -1 if wrong, 0 if neutral

            var tradingReturn = baseReturn * returnMultiplier;
            returns.Add(tradingReturn);
        }

        return returns;
    }

    private double CalculateSharpeRatio(List<double> returns, double riskFreeRate = 0.02)
    {
        if (!returns.Any()) return 0;

        var avgReturn = returns.Average();
        var excessReturn = avgReturn - (riskFreeRate / 252); // Daily risk-free rate
        var volatility = CalculateVolatility(returns);

        return volatility > 0 ? excessReturn / volatility : 0;
    }

    private double CalculateMaxDrawdown(List<double> returns)
    {
        if (!returns.Any()) return 0;

        var cumulativeReturns = new List<double> { 1.0 };
        foreach (var ret in returns)
        {
            cumulativeReturns.Add(cumulativeReturns.Last() * (1 + ret));
        }

        var maxDrawdown = 0.0;
        var peak = cumulativeReturns[0];

        foreach (var value in cumulativeReturns)
        {
            if (value > peak)
                peak = value;

            var drawdown = (peak - value) / peak;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    private double CalculateVolatility(List<double> returns)
    {
        if (returns.Count < 2) return 0;

        var mean = returns.Average();
        var variance = returns.Select(r => Math.Pow(r - mean, 2)).Average();
        return Math.Sqrt(variance);
    }

    private double CalculateWinRate(List<double> returns)
    {
        if (!returns.Any()) return 0;
        return (double)returns.Count(r => r > 0) / returns.Count;
    }

    private double CalculateAccuracy(float[,] predictions, float[,] trueLabels)
    {
        var predClasses = ArgMax(predictions);
        var trueClasses = ArgMax(trueLabels);
        
        var correct = 0;
        for (int i = 0; i < predClasses.Length; i++)
        {
            if (predClasses[i] == trueClasses[i])
                correct++;
        }
        
        return (double)correct / predClasses.Length;
    }

    private double CalculatePrecision(float[,] predictions, float[,] trueLabels)
    {
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

    private double CalculateMSE(float[,] predictions, float[,] trueLabels)
    {
        var numSamples = predictions.GetLength(0);
        var numClasses = predictions.GetLength(1);
        var mse = 0.0;

        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                var error = predictions[i, j] - trueLabels[i, j];
                mse += error * error;
            }
        }

        return mse / (numSamples * numClasses);
    }

    private double CalculateMAE(float[,] predictions, float[,] trueLabels)
    {
        var numSamples = predictions.GetLength(0);
        var numClasses = predictions.GetLength(1);
        var mae = 0.0;

        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                mae += Math.Abs(predictions[i, j] - trueLabels[i, j]);
            }
        }

        return mae / (numSamples * numClasses);
    }

    private double CalculateR2(float[,] predictions, float[,] trueLabels)
    {
        // Simplified R² calculation for multiclass
        var totalSumSquares = 0.0;
        var residualSumSquares = 0.0;
        var numSamples = predictions.GetLength(0);
        var numClasses = predictions.GetLength(1);

        // Calculate mean of true labels
        var mean = 0.0;
        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                mean += trueLabels[i, j];
            }
        }
        mean /= (numSamples * numClasses);

        // Calculate sums
        for (int i = 0; i < numSamples; i++)
        {
            for (int j = 0; j < numClasses; j++)
            {
                var trueVal = trueLabels[i, j];
                var predVal = predictions[i, j];
                
                totalSumSquares += Math.Pow(trueVal - mean, 2);
                residualSumSquares += Math.Pow(trueVal - predVal, 2);
            }
        }

        return totalSumSquares > 0 ? 1 - (residualSumSquares / totalSumSquares) : 0;
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

    private string GeneratePerformanceSummary(TrainingResult trainingResult, List<EvaluationMetrics> trainingMetrics, List<EvaluationMetrics> validationMetrics, EvaluationMetrics? testMetrics)
    {
        var summary = new List<string>
        {
            $"Model: {trainingResult.ModelVersion}",
            $"Training Duration: {trainingResult.TrainingDuration?.TotalMinutes:F1} minutes",
            $"Epochs Completed: {trainingResult.EpochsCompleted}/{trainingResult.TotalEpochs}"
        };

        if (testMetrics != null)
        {
            summary.Add($"Test Accuracy: {testMetrics.Accuracy:F4}");
            summary.Add($"Test Precision: {testMetrics.Precision:F4}");
            summary.Add($"Test Recall: {testMetrics.Recall:F4}");
            summary.Add($"Test F1-Score: {testMetrics.F1Score:F4}");

            if (!string.IsNullOrEmpty(testMetrics.CustomMetrics))
            {
                try
                {
                    var tradingMetrics = JsonConvert.DeserializeObject<TradingMetrics>(testMetrics.CustomMetrics);
                    if (tradingMetrics != null)
                    {
                        summary.Add($"Sharpe Ratio: {tradingMetrics.SharpeRatio:F2}");
                        summary.Add($"Max Drawdown: {tradingMetrics.MaxDrawdown:P2}");
                        summary.Add($"Win Rate: {tradingMetrics.WinRate:P2}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse trading metrics for summary");
                }
            }
        }

        return string.Join("\n", summary);
    }

    private BacktestMetrics CalculateBacktestMetrics(Portfolio portfolio, List<Trade> trades, BacktestConfiguration config)
    {
        var returns = portfolio.ValueHistory.Zip(portfolio.ValueHistory.Skip(1), (prev, curr) => (curr - prev) / prev).ToList();
        
        return new BacktestMetrics
        {
            SharpeRatio = CalculateSharpeRatio(returns),
            MaxDrawdown = CalculateMaxDrawdown(returns),
            Volatility = CalculateVolatility(returns),
            WinRate = CalculateWinRate(trades.Select(t => t.PnL).ToList()),
            TotalReturn = (portfolio.CurrentValue - config.InitialCapital) / config.InitialCapital,
            AverageReturn = returns.Average(),
            NumberOfTrades = trades.Count
        };
    }
}

// Supporting classes
public class ModelMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double MeanSquaredError { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double RootMeanSquaredError { get; set; }
    public double R2Score { get; set; }
}

public class TradingMetrics
{
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public double Volatility { get; set; }
    public double WinRate { get; set; }
    public double TotalReturn { get; set; }
    public double AverageReturn { get; set; }
    public int NumberOfTrades { get; set; }
}

public class BacktestResult
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double InitialCapital { get; set; }
    public double FinalValue { get; set; }
    public double TotalReturn { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public List<Trade> Trades { get; set; } = new();
    public List<double> PortfolioHistory { get; set; } = new();
    public BacktestMetrics Metrics { get; set; } = new();
}

public class BacktestConfiguration
{
    public double InitialCapital { get; set; } = 100000;
    public double PositionSize { get; set; } = 1000;
    public double ConfidenceThreshold { get; set; } = 0.6;
    public int LookbackPeriod { get; set; } = 100;
    public double TransactionCost { get; set; } = 0.001;
}

public class BacktestMetrics
{
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public double Volatility { get; set; }
    public double WinRate { get; set; }
    public double TotalReturn { get; set; }
    public double AverageReturn { get; set; }
    public int NumberOfTrades { get; set; }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public double Price { get; set; }
    public double Quantity { get; set; }
    public string Direction { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double PnL { get; set; }
}

public class Portfolio
{
    public double InitialValue { get; }
    public double CurrentValue { get; private set; }
    public List<double> ValueHistory { get; } = new();
    private double _lastPrice;

    public Portfolio(double initialValue, double initialPrice)
    {
        InitialValue = initialValue;
        CurrentValue = initialValue;
        ValueHistory.Add(initialValue);
        _lastPrice = initialPrice;
    }

    public void ExecuteTrade(Trade trade)
    {
        // Simplified trade execution
        var cost = Math.Abs(trade.Quantity * trade.Price * 0.001); // 0.1% transaction cost
        CurrentValue -= cost;
        
        trade.PnL = trade.Quantity * (trade.Price - _lastPrice);
        _lastPrice = trade.Price;
    }

    public void UpdateValue(double currentPrice, double position)
    {
        // Update portfolio value based on current position and price
        CurrentValue = InitialValue + (position * currentPrice);
        ValueHistory.Add(CurrentValue);
    }
}
