using System.Threading.Tasks;
using StyleMeter.Tracking;

namespace StyleMeter.Tests;

public sealed class StyleMeterComboEngineCrashPreventionTests
{
    private static readonly DateTime StartTime = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(InvalidRecastCases))]
    public void TryRecordGcd_ignores_invalid_recasts_without_throwing(float recastSeconds)
    {
        var engine = CreateEngine(out _);

        var exception = Record.Exception(() =>
        {
            var changed = engine.TryRecordGcd(100, recastSeconds, out var snapshot);
            Assert.False(changed);
            Assert.Equal(0, snapshot.ComboCount);
            Assert.False(snapshot.IsActive);
        });

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(InvalidRecastCases))]
    public void Invalid_recasts_do_not_mutate_existing_combo(float recastSeconds)
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out var first));

        clock.Advance(1);
        var changed = engine.TryRecordGcd(101, recastSeconds, out var snapshot);

        Assert.False(changed);
        Assert.Equal(first.ComboCount, snapshot.ComboCount);
        Assert.Equal(first.Rank, snapshot.Rank);
        Assert.Equal(first.CurrentRecastSeconds, snapshot.CurrentRecastSeconds);
        Assert.Equal(first.ExpirationTimeUtc, snapshot.ExpirationTimeUtc);
    }

    [Fact]
    public void TryRecordGcd_near_DateTime_MaxValue_does_not_throw_or_mutate()
    {
        var clock = new ManualStyleMeterClock(DateTime.MaxValue.AddSeconds(-1));
        var engine = new StyleMeterComboEngine(() => 0.5f, clock);

        var exception = Record.Exception(() =>
        {
            var changed = engine.TryRecordGcd(100, 2.5f, out var snapshot);
            Assert.False(changed);
            Assert.Equal(0, snapshot.ComboCount);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Tick_and_snapshot_are_safe_around_DateTime_boundaries()
    {
        var clock = new ManualStyleMeterClock(DateTime.MinValue.AddSeconds(1));
        var engine = new StyleMeterComboEngine(() => 0.5f, clock);

        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(DateTime.MaxValue);
        var exception = Record.Exception(() =>
        {
            Assert.True(engine.Tick());
            _ = engine.CurrentSnapshot;
        });

        Assert.Null(exception);
        Assert.False(engine.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void Repeated_clear_and_tick_calls_are_idempotent()
    {
        var engine = CreateEngine(out _);

        for (var i = 0; i < 100; i++)
        {
            engine.Clear();
            Assert.False(engine.Tick());
        }

        var snapshot = engine.CurrentSnapshot;
        Assert.Equal(0, snapshot.ComboCount);
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void Null_clock_dependency_falls_back_to_system_clock()
    {
        var exception = Record.Exception(() =>
        {
            var engine = new StyleMeterComboEngine(() => 0.5f, null!);
            Assert.True(engine.TryRecordGcd(100, 2.5f, out var snapshot));
            Assert.Equal(1, snapshot.ComboCount);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Concurrent_records_ticks_clears_and_snapshots_do_not_throw()
    {
        var engine = CreateEngine(out _);

        var exception = Record.Exception(() =>
        {
            Parallel.For(0, 2_000, i =>
            {
                switch (i % 6)
                {
                    case 0:
                        engine.TryRecordGcd((uint)(i + 1), 2.5f, out _);
                        break;
                    case 1:
                        engine.TryRecordGcd((uint)(i + 1), 0f, out _);
                        break;
                    case 2:
                        engine.TryRecordOffGlobalCooldown((uint)(i + 1), out _);
                        break;
                    case 3:
                        engine.Tick();
                        break;
                    case 4:
                        _ = engine.CurrentSnapshot;
                        break;
                    default:
                        engine.Clear();
                        break;
                }
            });
        });

        Assert.Null(exception);
        var snapshot = engine.CurrentSnapshot;
        Assert.NotNull(snapshot.Rank);
        Assert.InRange(snapshot.ComboCount, 0, 2_000);
    }

    public static TheoryData<float> InvalidRecastCases => new()
    {
        0f,
        -0.001f,
        -2.5f,
        float.NaN,
        float.PositiveInfinity,
        float.NegativeInfinity,
    };

    private static StyleMeterComboEngine CreateEngine(out ManualStyleMeterClock clock)
    {
        clock = new ManualStyleMeterClock(StartTime);
        return new StyleMeterComboEngine(() => StyleMeterComboEngine.DefaultGraceThresholdSeconds, clock);
    }
}
