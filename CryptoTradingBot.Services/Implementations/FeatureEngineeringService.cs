
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoTradingBot.Services.Implementations;

public class FeatureEngineeringService : IFeatureEngineering
{
    private readonly ILogger<FeatureEngineeringService> _logger;

    public FeatureEngineeringService(ILogger<FeatureEngineeringService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<TrainingData>> ExtractFeaturesAsync(IEnumerable<OrderBookData> orderBookData, ModelConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting feature extraction for {Count} records", orderBookData.Count());

        var dataList = orderBookData.OrderBy(x => x.Timestamp).ToList();
        var trainingData = new List<TrainingData>();

        // Extract features based on configuration
        var windowSize = GetWindowSizeFromConfiguration(configuration);
        var featureTypes = GetFeatureTypesFromConfiguration(configuration);

        for (int i = windowSize; i < dataList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windowData = dataList.Skip(i - windowSize).Take(windowSize).ToList();
            var currentData = dataList[i];

            var features = await ExtractFeaturesForWindowAsync(windowData, currentData, featureTypes, cancellationToken);
            var labels = ExtractLabels(currentData, dataList, i);

            trainingData.Add(new TrainingData
            {
                Id = Guid.NewGuid(),
                OrderBookDataId = currentData.Id,
                ModelConfigurationId = configuration.Id,
                Features = JsonConvert.SerializeObject(features),
                Labels = JsonConvert.SerializeObject(labels),
                DataType = DetermineDataType(i, dataList.Count),
                ProcessedAt = DateTime.UtcNow,
                ProcessingVersion = "2.0.0"
            });
        }

        _logger.LogInformation("Extracted features for {Count} training samples", trainingData.Count);
        return trainingData;
    }

    public async Task<TrainingData> ExtractFeaturesForSingleRecordAsync(OrderBookData orderBookData, ModelConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken); // Simulate async operation

        var featureTypes = GetFeatureTypesFromConfiguration(configuration);
        var features = ExtractBasicFeatures(orderBookData, featureTypes);
        var labels = new Dictionary<string, object> { ["price_direction"] = 0 }; // Placeholder

        return new TrainingData
        {
            Id = Guid.NewGuid(),
            OrderBookDataId = orderBookData.Id,
            ModelConfigurationId = configuration.Id,
            Features = JsonConvert.SerializeObject(features),
            Labels = JsonConvert.SerializeObject(labels),
            DataType = "Single",
            ProcessedAt = DateTime.UtcNow,
            ProcessingVersion = "2.0.0"
        };
    }

    public async Task<FeatureExtractionResult> ValidateFeatureExtractionAsync(ModelConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        var availableFeatures = await GetAvailableFeaturesAsync(cancellationToken);
        var configuredFeatures = GetFeatureTypesFromConfiguration(configuration);
        
        var validFeatures = configuredFeatures.Intersect(availableFeatures).ToList();
        var invalidFeatures = configuredFeatures.Except(availableFeatures).ToList();

        return new FeatureExtractionResult
        {
            IsValid = !invalidFeatures.Any(),
            ExtractedFeatures = validFeatures,
            MissingFeatures = invalidFeatures,
            ErrorMessage = invalidFeatures.Any() ? $"Invalid features: {string.Join(", ", invalidFeatures)}" : string.Empty
        };
    }

    public async Task<IEnumerable<string>> GetAvailableFeaturesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        return new List<string>
        {
            // Basic price features
            "best_bid_price", "best_ask_price", "mid_price", "spread", "spread_percentage",
            
            // Volume features
            "best_bid_quantity", "best_ask_quantity", "total_bid_volume", "total_ask_volume",
            "volume_imbalance", "volume_ratio",
            
            // Price movement features
            "price_change", "price_change_percentage", "volatility",
            
            // Technical indicators
            "moving_average_5", "moving_average_10", "moving_average_20",
            "rsi", "bollinger_upper", "bollinger_lower", "macd", "macd_signal",
            
            // Order book depth features
            "bid_levels", "ask_levels", "depth_imbalance",
            
            // Time-based features
            "hour_of_day", "day_of_week", "minute_of_hour",
            
            // Advanced features
            "order_flow_imbalance", "microprice", "effective_spread",
            "realized_spread", "price_impact", "market_impact"
        };
    }

    public async Task<FeatureImportanceResult> CalculateFeatureImportanceAsync(IEnumerable<TrainingData> trainingData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating feature importance for {Count} training samples", trainingData.Count());

        await Task.Delay(100, cancellationToken);

        // Extract all features from training data
        var allFeatures = new Dictionary<string, List<double>>();
        var allLabels = new List<double>();

        foreach (var data in trainingData)
        {
            var features = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.Features);
            var labels = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.Labels);

            if (features != null)
            {
                foreach (var feature in features)
                {
                    if (!allFeatures.ContainsKey(feature.Key))
                        allFeatures[feature.Key] = new List<double>();

                    if (double.TryParse(feature.Value?.ToString(), out var value))
                        allFeatures[feature.Key].Add(value);
                }
            }

            if (labels != null && labels.ContainsKey("price_direction"))
            {
                if (double.TryParse(labels["price_direction"]?.ToString(), out var label))
                    allLabels.Add(label);
            }
        }

        // Calculate correlation-based importance (simplified)
        var importances = new Dictionary<string, double>();
        foreach (var feature in allFeatures)
        {
            if (feature.Value.Count == allLabels.Count)
            {
                var correlation = CalculateCorrelation(feature.Value, allLabels);
                importances[feature.Key] = Math.Abs(correlation);
            }
        }

        var topFeatures = importances
            .OrderByDescending(x => x.Value)
            .Take(10)
            .Select(x => x.Key)
            .ToList();

        return new FeatureImportanceResult
        {
            FeatureImportances = importances,
            TopFeatures = topFeatures,
            TotalImportance = importances.Values.Sum()
        };
    }

    public async Task<IEnumerable<TrainingData>> ApplyFeatureSelectionAsync(IEnumerable<TrainingData> trainingData, IEnumerable<string> selectedFeatures, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying feature selection with {Count} selected features", selectedFeatures.Count());

        var selectedFeatureSet = selectedFeatures.ToHashSet();
        var filteredData = new List<TrainingData>();

        foreach (var data in trainingData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var features = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.Features);
            if (features == null) continue;

            var filteredFeatures = features
                .Where(f => selectedFeatureSet.Contains(f.Key))
                .ToDictionary(f => f.Key, f => f.Value);

            filteredData.Add(new TrainingData
            {
                Id = data.Id,
                OrderBookDataId = data.OrderBookDataId,
                ModelConfigurationId = data.ModelConfigurationId,
                Features = JsonConvert.SerializeObject(filteredFeatures),
                Labels = data.Labels,
                DataType = data.DataType,
                ProcessedAt = DateTime.UtcNow,
                ProcessingVersion = data.ProcessingVersion
            });
        }

        await Task.Delay(10, cancellationToken);
        return filteredData;
    }

    private async Task<Dictionary<string, object>> ExtractFeaturesForWindowAsync(
        List<OrderBookData> windowData, 
        OrderBookData currentData, 
        List<string> featureTypes, 
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);

        var features = new Dictionary<string, object>();

        // Basic features
        if (featureTypes.Contains("basic"))
        {
            var basicFeatures = ExtractBasicFeatures(currentData, featureTypes);
            foreach (var feature in basicFeatures)
                features[feature.Key] = feature.Value;
        }

        // Technical indicators
        if (featureTypes.Contains("technical"))
        {
            var technicalFeatures = ExtractTechnicalIndicators(windowData, currentData);
            foreach (var feature in technicalFeatures)
                features[feature.Key] = feature.Value;
        }

        // Advanced features
        if (featureTypes.Contains("advanced"))
        {
            var advancedFeatures = ExtractAdvancedFeatures(windowData, currentData);
            foreach (var feature in advancedFeatures)
                features[feature.Key] = feature.Value;
        }

        return features;
    }

    private Dictionary<string, object> ExtractBasicFeatures(OrderBookData data, List<string> featureTypes)
    {
        var features = new Dictionary<string, object>
        {
            ["best_bid_price"] = data.BestBidPrice,
            ["best_ask_price"] = data.BestAskPrice,
            ["mid_price"] = data.MidPrice,
            ["spread"] = data.Spread,
            ["spread_percentage"] = data.Spread / data.MidPrice * 100,
            ["best_bid_quantity"] = data.BestBidQuantity,
            ["best_ask_quantity"] = data.BestAskQuantity,
            ["total_bid_volume"] = data.TotalBidVolume,
            ["total_ask_volume"] = data.TotalAskVolume,
            ["volume_imbalance"] = (data.TotalBidVolume - data.TotalAskVolume) / (data.TotalBidVolume + data.TotalAskVolume),
            ["volume_ratio"] = data.TotalBidVolume / Math.Max(data.TotalAskVolume, 1),
            ["bid_levels"] = data.BidLevels,
            ["ask_levels"] = data.AskLevels,
            ["depth_imbalance"] = (data.BidLevels - data.AskLevels) / (double)(data.BidLevels + data.AskLevels),
            ["hour_of_day"] = data.Timestamp.Hour,
            ["day_of_week"] = (int)data.Timestamp.DayOfWeek,
            ["minute_of_hour"] = data.Timestamp.Minute
        };

        return features;
    }

    private Dictionary<string, object> ExtractTechnicalIndicators(List<OrderBookData> windowData, OrderBookData currentData)
    {
        var prices = windowData.Select(x => (double)x.MidPrice).ToArray();
        var volumes = windowData.Select(x => (double)x.TotalBidVolume + (double)x.TotalAskVolume).ToArray();

        var features = new Dictionary<string, object>();

        // Moving averages
        if (prices.Length >= 5)
            features["moving_average_5"] = prices.TakeLast(5).Average();
        if (prices.Length >= 10)
            features["moving_average_10"] = prices.TakeLast(10).Average();
        if (prices.Length >= 20)
            features["moving_average_20"] = prices.TakeLast(20).Average();

        // RSI
        if (prices.Length >= 14)
            features["rsi"] = CalculateRSI(prices, 14);

        // Volatility
        if (prices.Length >= 2)
        {
            var returns = new double[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
                returns[i - 1] = Math.Log(prices[i] / prices[i - 1]);
            
            features["volatility"] = CalculateStandardDeviation(returns);
        }

        // Price change
        if (prices.Length >= 2)
        {
            features["price_change"] = prices.Last() - prices[^2];
            features["price_change_percentage"] = (prices.Last() - prices[^2]) / prices[^2] * 100;
        }

        return features;
    }

    private Dictionary<string, object> ExtractAdvancedFeatures(List<OrderBookData> windowData, OrderBookData currentData)
    {
        var features = new Dictionary<string, object>();

        // Microprice
        var totalVolume = currentData.BestBidQuantity + currentData.BestAskQuantity;
        if (totalVolume > 0)
        {
            features["microprice"] = ((double)currentData.BestBidPrice * (double)currentData.BestAskQuantity + 
                                    (double)currentData.BestAskPrice * (double)currentData.BestBidQuantity) / (double)totalVolume;
        }

        // Order flow imbalance
        features["order_flow_imbalance"] = ((double)currentData.BestBidQuantity - (double)currentData.BestAskQuantity) / 
                                         ((double)currentData.BestBidQuantity + (double)currentData.BestAskQuantity);

        // Effective spread
        features["effective_spread"] = (double)currentData.Spread / (double)currentData.MidPrice;

        // Market impact (simplified)
        if (windowData.Count >= 2)
        {
            var prevData = windowData[^2];
            var priceImpact = Math.Abs((double)currentData.MidPrice - (double)prevData.MidPrice) / (double)prevData.MidPrice;
            features["market_impact"] = priceImpact;
        }

        return features;
    }

    private Dictionary<string, object> ExtractLabels(OrderBookData currentData, List<OrderBookData> allData, int currentIndex)
    {
        var labels = new Dictionary<string, object>();

        // Future price direction (classification)
        if (currentIndex < allData.Count - 5)
        {
            var futurePrice = allData[currentIndex + 5].MidPrice;
            var currentPrice = currentData.MidPrice;
            
            if (futurePrice > currentPrice * 1.001m) // 0.1% threshold
                labels["price_direction"] = 1; // Up
            else if (futurePrice < currentPrice * 0.999m)
                labels["price_direction"] = -1; // Down
            else
                labels["price_direction"] = 0; // Stable
        }

        // Future price (regression)
        if (currentIndex < allData.Count - 1)
        {
            labels["future_price"] = allData[currentIndex + 1].MidPrice;
        }

        return labels;
    }

    private int GetWindowSizeFromConfiguration(ModelConfiguration configuration)
    {
        // Parse window size from configuration parameters
        var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(configuration.Hyperparameters ?? "{}");
        if (parameters?.ContainsKey("window_size") == true && 
            int.TryParse(parameters["window_size"]?.ToString(), out var windowSize))
        {
            return windowSize;
        }
        return 50; // Default window size
    }

    private List<string> GetFeatureTypesFromConfiguration(ModelConfiguration configuration)
    {
        var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(configuration.Hyperparameters ?? "{}");
        if (parameters?.ContainsKey("feature_types") == true)
        {
            var featureTypesJson = parameters["feature_types"]?.ToString();
            if (!string.IsNullOrEmpty(featureTypesJson))
            {
                return JsonConvert.DeserializeObject<List<string>>(featureTypesJson) ?? new List<string> { "basic" };
            }
        }
        return new List<string> { "basic", "technical", "advanced" };
    }

    private string DetermineDataType(int index, int totalCount)
    {
        var ratio = (double)index / totalCount;
        return ratio switch
        {
            < 0.7 => "Train",
            < 0.85 => "Validation",
            _ => "Test"
        };
    }

    private double CalculateRSI(double[] prices, int period)
    {
        if (prices.Length < period + 1) return 50.0;

        var gains = new List<double>();
        var losses = new List<double>();

        for (int i = 1; i < prices.Length; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0) return 100.0;

        var rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    private double CalculateStandardDeviation(double[] values)
    {
        if (values.Length < 2) return 0.0;

        var mean = values.Average();
        var sumSquaredDiffs = values.Sum(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / (values.Length - 1));
    }

    private double CalculateCorrelation(List<double> x, List<double> y)
    {
        if (x.Count != y.Count || x.Count < 2) return 0.0;

        var meanX = x.Average();
        var meanY = y.Average();

        var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
        var denominator = Math.Sqrt(x.Sum(xi => Math.Pow(xi - meanX, 2)) * y.Sum(yi => Math.Pow(yi - meanY, 2)));

        return denominator == 0 ? 0.0 : numerator / denominator;
    }
}
