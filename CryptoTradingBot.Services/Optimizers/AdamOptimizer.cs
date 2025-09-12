
namespace CryptoTradingBot.Services.Optimizers;

public class AdamOptimizer : IOptimizer
{
    public float LearningRate { get; set; }
    private readonly float _beta1;
    private readonly float _beta2;
    private readonly float _epsilon;
    private int _step;

    public AdamOptimizer(float learningRate = 0.001f, float beta1 = 0.9f, float beta2 = 0.999f, float epsilon = 1e-8f)
    {
        LearningRate = learningRate;
        _beta1 = beta1;
        _beta2 = beta2;
        _epsilon = epsilon;
        _step = 0;
    }

    public void Step()
    {
        _step++;
        // Adam optimization step simulation
        // In a real implementation, this would update model parameters
    }

    public void UpdateLearningRate(int epoch, float validationLoss)
    {
        // Learning rate scheduling
        if (epoch > 0 && epoch % 20 == 0)
        {
            LearningRate *= 0.9f; // Reduce by 10% every 20 epochs
        }
    }

    public void Reset()
    {
        _step = 0;
    }
}
