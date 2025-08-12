
namespace AiModelTrainingService.Core.Entities;

public class TrainingMetric
{
    public Guid Id { get; set; }
    public Guid TrainingJobId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public int Epoch { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Navigation properties
    public TrainingJob TrainingJob { get; set; } = null!;
}
