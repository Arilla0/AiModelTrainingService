
using CryptoTradingBot.Core.Enums;

namespace CryptoTradingBot.Core.Entities;

public class TrainingJob
{
    public Guid Id { get; set; }
    public Guid ModelDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TrainingStatus Status { get; set; }
    public string Parameters { get; set; } = string.Empty; // JSON parameters
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public double? Accuracy { get; set; }
    public double? Loss { get; set; }
    public int Epochs { get; set; }
    public string? ModelPath { get; set; }
    
    // Navigation properties
    public ModelDefinition ModelDefinition { get; set; } = null!;
    public ICollection<TrainingMetric> Metrics { get; set; } = new List<TrainingMetric>();
}
