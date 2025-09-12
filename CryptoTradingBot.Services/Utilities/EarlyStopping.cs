
namespace CryptoTradingBot.Services.Utilities;

public class EarlyStopping
{
    private readonly int _patience;
    private readonly float _minDelta;
    private readonly string _monitor;
    private int _waitCount;
    private float _bestValue;
    private bool _isMaximizing;

    public EarlyStopping(int patience = 10, float minDelta = 0.001f, string monitor = "val_loss")
    {
        _patience = patience;
        _minDelta = minDelta;
        _monitor = monitor;
        _waitCount = 0;
        _isMaximizing = monitor.Contains("acc") || monitor.Contains("f1") || monitor.Contains("precision") || monitor.Contains("recall");
        _bestValue = _isMaximizing ? float.MinValue : float.MaxValue;
    }

    public bool ShouldStop(float currentValue)
    {
        bool improved = _isMaximizing 
            ? currentValue > _bestValue + _minDelta
            : currentValue < _bestValue - _minDelta;

        if (improved)
        {
            _bestValue = currentValue;
            _waitCount = 0;
            return false;
        }

        _waitCount++;
        return _waitCount >= _patience;
    }

    public void Reset()
    {
        _waitCount = 0;
        _bestValue = _isMaximizing ? float.MinValue : float.MaxValue;
    }

    public float BestValue => _bestValue;
    public int WaitCount => _waitCount;
}
