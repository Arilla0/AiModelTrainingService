
namespace CryptoTradingBot.Api.Models;

public class StartTradingModelTrainingRequest
{
    public Guid ModelConfigurationId { get; set; }
    public Guid DatasetId { get; set; }
}

public class ExportModelRequest
{
    public string Format { get; set; } = "onnx";
}

public class ExtractFeaturesRequest
{
    public IEnumerable<Guid> OrderBookDataIds { get; set; } = new List<Guid>();
    public Guid ModelConfigurationId { get; set; }
}

public class FeatureImportanceRequest
{
    public IEnumerable<Guid> TrainingDataIds { get; set; } = new List<Guid>();
}

public class FeatureSelectionRequest
{
    public IEnumerable<Guid> TrainingDataIds { get; set; } = new List<Guid>();
    public IEnumerable<string> SelectedFeatures { get; set; } = new List<string>();
}
