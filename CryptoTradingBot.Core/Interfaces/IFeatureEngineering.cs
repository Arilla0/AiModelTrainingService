
using CryptoTradingBot.Core.Entities;

namespace CryptoTradingBot.Core.Interfaces;

public interface IFeatureEngineering
{
    Task<IEnumerable<TrainingData>> ExtractFeaturesAsync(IEnumerable<OrderBookData> orderBookData, ModelConfiguration configuration, CancellationToken cancellationToken = default);
    Task<TrainingData> ExtractFeaturesForSingleRecordAsync(OrderBookData orderBookData, ModelConfiguration configuration, CancellationToken cancellationToken = default);
    Task<FeatureExtractionResult> ValidateFeatureExtractionAsync(ModelConfiguration configuration, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetAvailableFeaturesAsync(CancellationToken cancellationToken = default);
    Task<FeatureImportanceResult> CalculateFeatureImportanceAsync(IEnumerable<TrainingData> trainingData, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainingData>> ApplyFeatureSelectionAsync(IEnumerable<TrainingData> trainingData, IEnumerable<string> selectedFeatures, CancellationToken cancellationToken = default);
}

public class FeatureExtractionResult
{
    public bool IsValid { get; set; }
    public IEnumerable<string> ExtractedFeatures { get; set; } = new List<string>();
    public IEnumerable<string> MissingFeatures { get; set; } = new List<string>();
    public string ErrorMessage { get; set; } = string.Empty;
}

public class FeatureImportanceResult
{
    public Dictionary<string, double> FeatureImportances { get; set; } = new Dictionary<string, double>();
    public IEnumerable<string> TopFeatures { get; set; } = new List<string>();
    public double TotalImportance { get; set; }
}
