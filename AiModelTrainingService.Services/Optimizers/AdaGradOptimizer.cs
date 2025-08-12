
namespace AiModelTrainingService.Services.Optimizers;

public class AdaGradOptimizer : IOptimizer
{
    public float LearningRate { get; set; }
    private readonly float _epsilon;

    public AdaGradOptimizer(float learningRate = 0.01f, float epsilon = 1e-8f)
    {
        LearningRate = learningRate;
        _epsilon = epsilon;
    }

    public void Step()
    {
        // AdaGrad optimization step simulation
        // In a real implementation, this would update model parameters
    }

    public void UpdateLearningRate(int epoch, float validationLoss)
    {
        // AdaGrad naturally adapts learning rate, but we can add additional scheduling
        if (epoch > 0 && epoch % 25 == 0)
        {
            LearningRate *= 0.95f; // Small reduction every 25 epochs
        }
    }

    public void Reset()
    {
        // Reset optimizer state
    }
}
