
using CryptoTradingBot.Services.Models;
using Newtonsoft.Json;

namespace CryptoTradingBot.Services.Utilities;

public class CheckpointManager
{
    private readonly string _checkpointDir;
    private readonly bool _saveBestOnly;
    private float _bestScore = float.MaxValue;
    private int _bestEpoch = -1;

    public CheckpointManager(string modelPath, bool saveBestOnly = true)
    {
        _checkpointDir = Path.Combine(Path.GetDirectoryName(modelPath)!, "checkpoints");
        _saveBestOnly = saveBestOnly;
        Directory.CreateDirectory(_checkpointDir);
    }

    public async Task SaveCheckpoint(DeepLOBModel model, int epoch, float score, object trainMetrics, object valMetrics)
    {
        var shouldSave = !_saveBestOnly || score < _bestScore;

        if (shouldSave)
        {
            if (score < _bestScore)
            {
                _bestScore = score;
                _bestEpoch = epoch;
            }

            var checkpointPath = Path.Combine(_checkpointDir, $"checkpoint_epoch_{epoch}");
            model.SaveModel(checkpointPath);

            var metadata = new
            {
                Epoch = epoch,
                Score = score,
                TrainMetrics = trainMetrics,
                ValidationMetrics = valMetrics,
                IsBest = score == _bestScore,
                SavedAt = DateTime.UtcNow
            };

            var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            await File.WriteAllTextAsync($"{checkpointPath}_metadata.json", metadataJson);

            // Save best checkpoint separately
            if (score == _bestScore)
            {
                var bestCheckpointPath = Path.Combine(_checkpointDir, "best_checkpoint");
                model.SaveModel(bestCheckpointPath);
                await File.WriteAllTextAsync($"{bestCheckpointPath}_metadata.json", metadataJson);
            }
        }
    }

    public async Task LoadBestCheckpoint(DeepLOBModel model)
    {
        var bestCheckpointPath = Path.Combine(_checkpointDir, "best_checkpoint");
        if (File.Exists($"{bestCheckpointPath}.json"))
        {
            model.LoadModel(bestCheckpointPath);
        }
    }

    public async Task<CheckpointInfo?> GetLastCheckpoint()
    {
        var checkpointFiles = Directory.GetFiles(_checkpointDir, "*_metadata.json")
            .Where(f => !f.Contains("best_checkpoint"))
            .OrderByDescending(f => File.GetCreationTime(f))
            .FirstOrDefault();

        if (checkpointFiles == null) return null;

        var json = await File.ReadAllTextAsync(checkpointFiles);
        var metadata = JsonConvert.DeserializeObject<dynamic>(json);

        return new CheckpointInfo
        {
            Epoch = metadata?.Epoch ?? 0,
            Score = metadata?.Score ?? 0f,
            Path = checkpointFiles.Replace("_metadata.json", "")
        };
    }

    public float BestScore => _bestScore;
    public int BestEpoch => _bestEpoch;
}

public class CheckpointInfo
{
    public int Epoch { get; set; }
    public float Score { get; set; }
    public string Path { get; set; } = string.Empty;
}
