using StyleMeter.Tracking;

namespace StyleMeter.Tests;

internal sealed class ManualStyleMeterClock : IStyleMeterClock
{
    private readonly object stateLock = new();
    private DateTime utcNow;

    public ManualStyleMeterClock(DateTime utcNow)
    {
        this.utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public DateTime UtcNow
    {
        get
        {
            lock (this.stateLock)
            {
                return this.utcNow;
            }
        }
    }

    public void Set(DateTime value)
    {
        lock (this.stateLock)
        {
            this.utcNow = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    public void Advance(double seconds)
    {
        lock (this.stateLock)
        {
            this.utcNow = this.utcNow.AddSeconds(seconds);
        }
    }
}
