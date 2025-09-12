
namespace CryptoTradingBot.Services.Optimizers;

public interface IOptimizer
{
    float LearningRate { get; set; }
    void Step();
    void UpdateLearningRate(int epoch, float validationLoss);
    void Reset();
}
