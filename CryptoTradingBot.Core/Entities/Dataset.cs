
namespace CryptoTradingBot.Core.Entities;

public class Dataset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Format { get; set; } = string.Empty; // CSV, JSON, etc.
    public int RecordCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<ModelDefinition> ModelDefinitions { get; set; } = new List<ModelDefinition>();
}
