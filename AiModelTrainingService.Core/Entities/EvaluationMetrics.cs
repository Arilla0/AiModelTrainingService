
namespace AiModelTrainingService.Core.Entities;

public class EvaluationMetrics
{
    public Guid Id { get; set; }
    public Guid TrainingResultId { get; set; }
    public string MetricType { get; set; } = string.Empty; // Training, Validation, Test
    public double? Accuracy { get; set; }
    public double? Precision { get; set; }
    public double? Recall { get; set; }
    public double? F1Score { get; set; }
    public double? MeanSquaredError { get; set; }
    public double? MeanAbsoluteError { get; set; }
    public double? RootMeanSquaredError { get; set; }
    public double? R2Score { get; set; }
    public double? Loss { get; set; }
    public double? ValidationLoss { get; set; }
    public string CustomMetrics { get; set; } = string.Empty; // JSON for additional metrics
    public int Epoch { get; set; }
    public DateTime RecordedAt { get; set; }
    
    // Navigation properties
    public TrainingResult TrainingResult { get; set; } = null!;
}
