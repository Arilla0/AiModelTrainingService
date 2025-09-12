
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CryptoTradingBot.Services.Implementations;

public class DataLoaderService : IDataLoader
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DataLoaderService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public DataLoaderService(IMemoryCache cache, ILogger<DataLoaderService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<OrderBookData>> LoadOrderBookDataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"orderbook_data_{Path.GetFileName(filePath)}_{File.GetLastWriteTime(filePath).Ticks}";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<OrderBookData>? cachedData))
        {
            _logger.LogInformation("Returning cached data for file: {FilePath}", filePath);
            return cachedData!;
        }

        _logger.LogInformation("Loading order book data from file: {FilePath}", filePath);

        var data = await LoadDataFromFileAsync(filePath, cancellationToken);
        
        _cache.Set(cacheKey, data, _cacheExpiration);
        _logger.LogInformation("Loaded and cached {Count} records from {FilePath}", data.Count(), filePath);
        
        return data;
    }

    public async Task<IEnumerable<OrderBookData>> LoadOrderBookDataFromStreamAsync(Stream dataStream, string format, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading order book data from stream with format: {Format}", format);

        return format.ToLowerInvariant() switch
        {
            "csv" => await LoadFromCsvStreamAsync(dataStream, cancellationToken),
            "json" => await LoadFromJsonStreamAsync(dataStream, cancellationToken),
            _ => throw new NotSupportedException($"Format '{format}' is not supported")
        };
    }

    public async Task<bool> ValidateDataFormatAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePath);
                return false;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".csv" => await ValidateCsvFormatAsync(filePath, cancellationToken),
                ".json" => await ValidateJsonFormatAsync(filePath, cancellationToken),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file format: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<DataLoadResult> LoadAndValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var isValid = await ValidateDataFormatAsync(filePath, cancellationToken);
            
            if (!isValid)
            {
                return new DataLoadResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid file format or file not found"
                };
            }

            var data = await LoadOrderBookDataAsync(filePath, cancellationToken);
            var dataList = data.ToList();
            
            return new DataLoadResult
            {
                IsSuccess = true,
                Data = dataList,
                RecordCount = dataList.Count,
                MinTimestamp = dataList.Any() ? dataList.Min(d => d.Timestamp) : null,
                MaxTimestamp = dataList.Any() ? dataList.Max(d => d.Timestamp) : null,
                Symbols = dataList.Select(d => d.Symbol).Distinct().ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading and validating data from: {FilePath}", filePath);
            return new DataLoadResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IEnumerable<OrderBookData>> LoadOrderBookDataBySymbolAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"orderbook_symbol_{symbol}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        
        if (_cache.TryGetValue(cacheKey, out IEnumerable<OrderBookData>? cachedData))
        {
            _logger.LogInformation("Returning cached data for symbol: {Symbol}", symbol);
            return cachedData!;
        }

        _logger.LogInformation("Loading order book data for symbol: {Symbol} from {StartDate} to {EndDate}", 
            symbol, startDate, endDate);

        // In a real implementation, this would query a database or API
        // For now, we'll simulate loading from a hypothetical data source
        var data = await SimulateSymbolDataLoadAsync(symbol, startDate, endDate, cancellationToken);
        
        _cache.Set(cacheKey, data, _cacheExpiration);
        return data;
    }

    public async Task<long> GetDataCountAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".csv" => await CountCsvRecordsAsync(filePath, cancellationToken),
                ".json" => await CountJsonRecordsAsync(filePath, cancellationToken),
                _ => 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting records in file: {FilePath}", filePath);
            return 0;
        }
    }

    private async Task<IEnumerable<OrderBookData>> LoadDataFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".csv" => await LoadFromCsvFileAsync(filePath, cancellationToken),
            ".json" => await LoadFromJsonFileAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException($"File extension '{extension}' is not supported")
        };
    }

    private async Task<IEnumerable<OrderBookData>> LoadFromCsvFileAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        return await LoadFromCsvStreamAsync(reader.BaseStream, cancellationToken);
    }

    private async Task<IEnumerable<OrderBookData>> LoadFromCsvStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        var records = new List<OrderBookData>();
        
        await foreach (var record in csv.GetRecordsAsync<OrderBookCsvRecord>(cancellationToken))
        {
            records.Add(new OrderBookData
            {
                Id = Guid.NewGuid(),
                Symbol = record.Symbol,
                Timestamp = record.Timestamp,
                BestBidPrice = record.BestBidPrice,
                BestAskPrice = record.BestAskPrice,
                BestBidQuantity = record.BestBidQuantity,
                BestAskQuantity = record.BestAskQuantity,
                Spread = record.BestAskPrice - record.BestBidPrice,
                MidPrice = (record.BestBidPrice + record.BestAskPrice) / 2,
                BidLevels = record.BidLevels,
                AskLevels = record.AskLevels,
                TotalBidVolume = record.TotalBidVolume,
                TotalAskVolume = record.TotalAskVolume,
                RawData = JsonConvert.SerializeObject(record),
                CreatedAt = DateTime.UtcNow
            });
        }

        return records;
    }

    private async Task<IEnumerable<OrderBookData>> LoadFromJsonFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await LoadFromJsonContentAsync(jsonContent, cancellationToken);
    }

    private async Task<IEnumerable<OrderBookData>> LoadFromJsonStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var jsonContent = await reader.ReadToEndAsync(cancellationToken);
        return await LoadFromJsonContentAsync(jsonContent, cancellationToken);
    }

    private async Task<IEnumerable<OrderBookData>> LoadFromJsonContentAsync(string jsonContent, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken); // Simulate async operation
        
        var jsonRecords = JsonConvert.DeserializeObject<List<OrderBookJsonRecord>>(jsonContent);
        if (jsonRecords == null) return new List<OrderBookData>();

        return jsonRecords.Select(record => new OrderBookData
        {
            Id = Guid.NewGuid(),
            Symbol = record.Symbol,
            Timestamp = record.Timestamp,
            BestBidPrice = record.BestBidPrice,
            BestAskPrice = record.BestAskPrice,
            BestBidQuantity = record.BestBidQuantity,
            BestAskQuantity = record.BestAskQuantity,
            Spread = record.BestAskPrice - record.BestBidPrice,
            MidPrice = (record.BestBidPrice + record.BestAskPrice) / 2,
            BidLevels = record.BidLevels,
            AskLevels = record.AskLevels,
            TotalBidVolume = record.TotalBidVolume,
            TotalAskVolume = record.TotalAskVolume,
            RawData = JsonConvert.SerializeObject(record),
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    private async Task<bool> ValidateCsvFormatAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            
            await csv.ReadAsync();
            csv.ReadHeader();
            
            var requiredHeaders = new[] { "Symbol", "Timestamp", "BestBidPrice", "BestAskPrice" };
            return requiredHeaders.All(header => csv.HeaderRecord?.Contains(header, StringComparer.OrdinalIgnoreCase) == true);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateJsonFormatAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var records = JsonConvert.DeserializeObject<List<OrderBookJsonRecord>>(content);
            return records != null && records.Any();
        }
        catch
        {
            return false;
        }
    }

    private async Task<long> CountCsvRecordsAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        long count = 0;
        
        // Skip header
        await reader.ReadLineAsync(cancellationToken);
        
        while (await reader.ReadLineAsync(cancellationToken) != null)
        {
            count++;
        }
        
        return count;
    }

    private async Task<long> CountJsonRecordsAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var records = JsonConvert.DeserializeObject<List<object>>(content);
        return records?.Count ?? 0;
    }

    private async Task<IEnumerable<OrderBookData>> SimulateSymbolDataLoadAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simulate database/API call
        
        var random = new Random();
        var data = new List<OrderBookData>();
        var currentDate = startDate;
        
        while (currentDate <= endDate)
        {
            var basePrice = 100m + (decimal)(random.NextDouble() * 50);
            
            data.Add(new OrderBookData
            {
                Id = Guid.NewGuid(),
                Symbol = symbol,
                Timestamp = currentDate,
                BestBidPrice = basePrice - 0.01m,
                BestAskPrice = basePrice + 0.01m,
                BestBidQuantity = random.Next(100, 1000),
                BestAskQuantity = random.Next(100, 1000),
                Spread = 0.02m,
                MidPrice = basePrice,
                BidLevels = random.Next(5, 20),
                AskLevels = random.Next(5, 20),
                TotalBidVolume = random.Next(1000, 10000),
                TotalAskVolume = random.Next(1000, 10000),
                RawData = "{}",
                CreatedAt = DateTime.UtcNow
            });
            
            currentDate = currentDate.AddMinutes(1);
        }
        
        return data;
    }

    private class OrderBookCsvRecord
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal BestBidPrice { get; set; }
        public decimal BestAskPrice { get; set; }
        public decimal BestBidQuantity { get; set; }
        public decimal BestAskQuantity { get; set; }
        public int BidLevels { get; set; }
        public int AskLevels { get; set; }
        public decimal TotalBidVolume { get; set; }
        public decimal TotalAskVolume { get; set; }
    }

    private class OrderBookJsonRecord
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal BestBidPrice { get; set; }
        public decimal BestAskPrice { get; set; }
        public decimal BestBidQuantity { get; set; }
        public decimal BestAskQuantity { get; set; }
        public int BidLevels { get; set; }
        public int AskLevels { get; set; }
        public decimal TotalBidVolume { get; set; }
        public decimal TotalAskVolume { get; set; }
    }
}
