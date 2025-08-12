
using AiModelTrainingService.Core.Enums;

namespace AiModelTrainingService.Core.Entities;

public class ModelConfiguration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ModelType ModelType { get; set; }
    public string Algorithm { get; set; } = string.Empty; // RandomForest, LSTM, etc.
    public string Hyperparameters { get; set; } = string.Empty; // JSON serialized
    public string FeatureColumns { get; set; } = string.Empty; // JSON array of feature column names
    public string TargetColumn { get; set; } = string.Empty;
    public int TrainingWindowSize { get; set; }
    public int PredictionHorizon { get; set; }
    public double ValidationSplit { get; set; }
    public double TestSplit { get; set; }
    public string PreprocessingSteps { get; set; } = string.Empty; // JSON serialized preprocessing pipeline
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    
    // Navigation properties
    public ICollection<TrainingData> TrainingDataRecords { get; set; } = new List<TrainingData>();
    public ICollection<TrainingResult> TrainingResults { get; set; } = new List<TrainingResult>();
}
