namespace DesktopBubbles.Core;

/// <summary>Time-weighted CPU interval observations. Spread is not measurement accuracy.</summary>
public sealed class CpuSeries(int processors)
{
    private readonly Queue<double> history = new();
    private double? lastWall, lastCpu;
    private double varianceSum;
    public double ActiveSeconds { get; private set; }
    public double Mean { get; private set; }
    public double Current { get; private set; }
    public double Spread => ActiveSeconds > 0 ? Math.Sqrt(Math.Max(0, varianceSum / ActiveSeconds)) : 0;
    public int Samples { get; private set; }
    public long WorkingSetBytes { get; private set; }
    public IReadOnlyCollection<double> History => history;
    public void Break() { lastWall = lastCpu = null; }
    public bool Observe(double wallSeconds, double cpuSeconds, long workingSetBytes)
    {
        if (!double.IsFinite(wallSeconds) || !double.IsFinite(cpuSeconds) || wallSeconds < 0 || cpuSeconds < 0) return false;
        WorkingSetBytes = Math.Max(0, workingSetBytes);
        if (lastWall is null || lastCpu is null) { lastWall = wallSeconds; lastCpu = cpuSeconds; return false; }
        double duration = wallSeconds - lastWall.Value, cpu = cpuSeconds - lastCpu.Value;
        if (duration <= 0) return false;
        lastWall = wallSeconds; lastCpu = cpuSeconds;
        if (cpu < 0) return false;
        Current = Math.Clamp(cpu / duration / Math.Max(1, processors) * 100, 0, 100);
        double newWeight = ActiveSeconds + duration, difference = Current - Mean;
        Mean += duration / newWeight * difference;
        varianceSum += duration * difference * (Current - Mean);
        ActiveSeconds = newWeight; Samples++;
        history.Enqueue(Current); if (history.Count > 90) history.Dequeue();
        return true;
    }
}
