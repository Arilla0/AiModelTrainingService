namespace CryptoTradingBot.Core.Interfaces;

public class ModelMetadata
{
    public string ModelPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
