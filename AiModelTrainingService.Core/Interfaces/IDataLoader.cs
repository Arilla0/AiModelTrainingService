
using AiModelTrainingService.Core.Entities;

namespace AiModelTrainingService.Core.Interfaces;

public interface IDataLoader
{
    Task<IEnumerable<OrderBookData>> LoadOrderBookDataAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderBookData>> LoadOrderBookDataFromStreamAsync(Stream dataStream, string format, CancellationToken cancellationToken = default);
    Task<bool> ValidateDataFormatAsync(string filePath, CancellationToken cancellationToken = default);
    Task<DataLoadResult> LoadAndValidateAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderBookData>> LoadOrderBookDataBySymbolAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<long> GetDataCountAsync(string filePath, CancellationToken cancellationToken = default);
}

public class DataLoadResult
{
    public bool IsSuccess { get; set; }
    public IEnumerable<OrderBookData> Data { get; set; } = new List<OrderBookData>();
    public string ErrorMessage { get; set; } = string.Empty;
    public long RecordCount { get; set; }
    public DateTime? MinTimestamp { get; set; }
    public DateTime? MaxTimestamp { get; set; }
    public IEnumerable<string> Symbols { get; set; } = new List<string>();
}
