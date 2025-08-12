
using AiModelTrainingService.Core.Enums;

namespace AiModelTrainingService.Core.Entities;

public class TrainingResult
{
    public Guid Id { get; set; }
    public Guid ModelConfigurationId { get; set; }
    public Guid TrainingJobId { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public string ModelArtifacts { get; set; } = string.Empty; // JSON serialized model artifacts info
    public TrainingStatus Status { get; set; }
    public DateTime TrainingStartedAt { get; set; }
    public DateTime? TrainingCompletedAt { get; set; }
    public TimeSpan? TrainingDuration { get; set; }
    public int EpochsCompleted { get; set; }
    public int TotalEpochs { get; set; }
    public string TrainingLogs { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ModelConfiguration ModelConfiguration { get; set; } = null!;
    public TrainingJob TrainingJob { get; set; } = null!;
    public ICollection<EvaluationMetrics> EvaluationMetrics { get; set; } = new List<EvaluationMetrics>();
}
