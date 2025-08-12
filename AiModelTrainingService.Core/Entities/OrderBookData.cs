
namespace AiModelTrainingService.Core.Entities;

public class OrderBookData
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal BestBidPrice { get; set; }
    public decimal BestAskPrice { get; set; }
    public decimal BestBidQuantity { get; set; }
    public decimal BestAskQuantity { get; set; }
    public decimal Spread { get; set; }
    public decimal MidPrice { get; set; }
    public int BidLevels { get; set; }
    public int AskLevels { get; set; }
    public decimal TotalBidVolume { get; set; }
    public decimal TotalAskVolume { get; set; }
    public string RawData { get; set; } = string.Empty; // JSON representation of full order book
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<TrainingData> TrainingDataRecords { get; set; } = new List<TrainingData>();
}
