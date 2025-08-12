
using AiModelTrainingService.Core.Entities;

namespace AiModelTrainingService.Core.Interfaces;

public interface IModelEvaluator
{
    Task<EvaluationMetrics> EvaluateModelAsync(TrainingResult trainingResult, IEnumerable<TrainingData> testData, CancellationToken cancellationToken = default);
    Task<IEnumerable<PredictionResult>> MakePredictionsAsync(string modelPath, IEnumerable<TrainingData> inputData, CancellationToken cancellationToken = default);
    Task<PredictionResult> MakeSinglePredictionAsync(string modelPath, TrainingData inputData, CancellationToken cancellationToken = default);
    Task<ModelPerformanceReport> GeneratePerformanceReportAsync(TrainingResult trainingResult, CancellationToken cancellationToken = default);
    Task<bool> ValidateModelAsync(string modelPath, CancellationToken cancellationToken = default);
    Task<ModelComparisonResult> CompareModelsAsync(IEnumerable<TrainingResult> trainingResults, IEnumerable<TrainingData> testData, CancellationToken cancellationToken = default);
    Task<CrossValidationResult> PerformCrossValidationAsync(ModelConfiguration configuration, IEnumerable<TrainingData> data, int folds = 5, CancellationToken cancellationToken = default);
}

public class PredictionResult
{
    public Guid Id { get; set; }
    public decimal PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public double Confidence { get; set; }
    public DateTime PredictedAt { get; set; }
    public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();
}

public class ModelPerformanceReport
{
    public Guid TrainingResultId { get; set; }
    public EvaluationMetrics TrainingMetrics { get; set; } = null!;
    public EvaluationMetrics ValidationMetrics { get; set; } = null!;
    public EvaluationMetrics? TestMetrics { get; set; }
    public IEnumerable<EvaluationMetrics> EpochMetrics { get; set; } = new List<EvaluationMetrics>();
    public string Summary { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class ModelComparisonResult
{
    public IEnumerable<ModelComparisonItem> Comparisons { get; set; } = new List<ModelComparisonItem>();
    public ModelComparisonItem BestModel { get; set; } = null!;
    public string ComparisonMetric { get; set; } = string.Empty;
    public DateTime ComparedAt { get; set; }
}

public class ModelComparisonItem
{
    public Guid TrainingResultId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public EvaluationMetrics Metrics { get; set; } = null!;
    public int Rank { get; set; }
}

public class CrossValidationResult
{
    public double MeanScore { get; set; }
    public double StandardDeviation { get; set; }
    public IEnumerable<double> FoldScores { get; set; } = new List<double>();
    public string Metric { get; set; } = string.Empty;
    public int Folds { get; set; }
}
