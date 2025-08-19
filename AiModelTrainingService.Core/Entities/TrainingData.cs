
namespace AiModelTrainingService.Core.Entities;

public class TrainingData
{
    public Guid Id { get; set; }
    public Guid OrderBookDataId { get; set; }
    public Guid ModelConfigurationId { get; set; }
    public string Features { get; set; } = string.Empty; // JSON serialized features
    public string Labels { get; set; } = string.Empty; // JSON serialized labels/targets
    public decimal? PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string DataType { get; set; } = string.Empty; // Train, Validation, Test
    public DateTime ProcessedAt { get; set; }
    public string ProcessingVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public OrderBookData OrderBookData { get; set; } = null!;
    public ModelConfiguration ModelConfiguration { get; set; } = null!;
}
