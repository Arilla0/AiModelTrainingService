
using AiModelTrainingService.Core.Enums;

namespace AiModelTrainingService.Core.Entities;

public class ModelDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ModelType Type { get; set; }
    public ModelStatus Status { get; set; }
    public string Configuration { get; set; } = string.Empty; // JSON configuration
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<TrainingJob> TrainingJobs { get; set; } = new List<TrainingJob>();
    public ICollection<Dataset> Datasets { get; set; } = new List<Dataset>();
}
