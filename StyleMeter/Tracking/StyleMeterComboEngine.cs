using System;

namespace StyleMeter.Tracking;

public readonly record struct StyleMeterSnapshot(
    int ComboCount,
    string Rank,
    bool IsActive,
    bool IsFading,
    float CurrentRecastSeconds,
    float GraceThresholdSeconds,
    DateTime LastGcdTimeUtc,
    DateTime ExpirationTimeUtc,
    DateTime LastEndedTimeUtc,
    int OffGlobalCooldownCount = 0,
    int ChainCount = 0,
    int BestComboCount = 0);

internal sealed class StyleMeterComboEngine
{
    public const float DefaultGraceThresholdSeconds = 0.5f;
    public const float MinGraceThresholdSeconds = 0f;
    public const float MaxGraceThresholdSeconds = 2f;
    public const double DuplicateCooldownWindowSeconds = 0.15;
    public const double FadeDurationSeconds = 1.25;

    private readonly Func<float> graceThresholdProvider;
    private readonly IStyleMeterClock clock;
    private readonly object stateLock = new();

    private int comboCount;
    private string rank = "D";
    private bool isActive;
    private DateTime lastGcdTimeUtc = DateTime.MinValue;
    private DateTime expirationTimeUtc = DateTime.MinValue;
    private DateTime lastEndedTimeUtc = DateTime.MinValue;
    private float currentRecastSeconds;
    private uint lastObservedActionId;
    private DateTime lastObservedActionTimeUtc = DateTime.MinValue;
    private int offGlobalCooldownCount;
    private uint lastObservedOffGlobalCooldownActionId;
    private DateTime lastObservedOffGlobalCooldownActionTimeUtc = DateTime.MinValue;
    private int bestComboCount;

    public StyleMeterComboEngine(Func<float> graceThresholdProvider)
        : this(graceThresholdProvider, SystemStyleMeterClock.Instance)
    {
    }

    public StyleMeterComboEngine(Func<float> graceThresholdProvider, IStyleMeterClock clock)
    {
        this.graceThresholdProvider = graceThresholdProvider ?? (() => DefaultGraceThresholdSeconds);
        this.clock = clock ?? SystemStyleMeterClock.Instance;
    }

    public StyleMeterSnapshot CurrentSnapshot
    {
        get
        {
            lock (this.stateLock)
            {
                return this.CreateSnapshotUnsafe(this.clock.UtcNow);
            }
        }
    }

    public bool TryRecordGcd(uint actionId, float recastSeconds, out StyleMeterSnapshot snapshot)
    {
        return this.TryRecordGcd(actionId, recastSeconds, true, out snapshot);
    }

    public bool TryRecordGcd(uint actionId, float recastSeconds, bool countForCombatBest, out StyleMeterSnapshot snapshot)
    {
        var now = this.clock.UtcNow;

        lock (this.stateLock)
        {
            if (!IsUsableRecastSeconds(recastSeconds))
            {
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            if (this.IsDuplicateCooldownUnsafe(actionId, now))
            {
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            var graceThresholdSeconds = this.GetGraceThresholdSeconds();
            if (!TryCreateExpirationTime(now, recastSeconds + graceThresholdSeconds, out var expirationTime))
            {
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            var continuesCombo = this.isActive && now <= this.expirationTimeUtc;

            this.comboCount = continuesCombo ? this.comboCount + 1 : 1;
            if (countForCombatBest)
            {
                this.bestComboCount = Math.Max(this.bestComboCount, this.comboCount);
            }

            if (!continuesCombo)
            {
                this.offGlobalCooldownCount = 0;
                this.lastObservedOffGlobalCooldownActionId = 0;
                this.lastObservedOffGlobalCooldownActionTimeUtc = DateTime.MinValue;
            }

            this.rank = GetRank(this.comboCount);
            this.isActive = true;
            this.currentRecastSeconds = recastSeconds;
            this.lastGcdTimeUtc = now;
            this.expirationTimeUtc = expirationTime;
            this.lastEndedTimeUtc = DateTime.MinValue;
            this.lastObservedActionId = actionId;
            this.lastObservedActionTimeUtc = now;

            snapshot = this.CreateSnapshotUnsafe(now);
            return true;
        }
    }

    public bool TryRecordOffGlobalCooldown(uint actionId, out StyleMeterSnapshot snapshot)
    {
        var now = this.clock.UtcNow;

        lock (this.stateLock)
        {
            if (!this.isActive)
            {
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            if (now > this.expirationTimeUtc)
            {
                this.EndComboUnsafe(now);
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            if (this.IsDuplicateOffGlobalCooldownUnsafe(actionId, now))
            {
                snapshot = this.CreateSnapshotUnsafe(now);
                return false;
            }

            this.offGlobalCooldownCount++;
            this.lastObservedOffGlobalCooldownActionId = actionId;
            this.lastObservedOffGlobalCooldownActionTimeUtc = now;

            snapshot = this.CreateSnapshotUnsafe(now);
            return true;
        }
    }

    public bool Tick()
    {
        var now = this.clock.UtcNow;

        lock (this.stateLock)
        {
            if (!this.isActive || now <= this.expirationTimeUtc)
            {
                return false;
            }

            this.EndComboUnsafe(now);
            return true;
        }
    }

    public bool DeferExpiration(float seconds)
    {
        var now = this.clock.UtcNow;

        lock (this.stateLock)
        {
            if (!this.isActive || !TryCreateExpirationTime(now, seconds, out var holdUntil))
            {
                return false;
            }

            if (holdUntil > this.expirationTimeUtc)
            {
                this.expirationTimeUtc = holdUntil;
            }

            return true;
        }
    }

    public void Clear()
    {
        lock (this.stateLock)
        {
            this.ClearUnsafe();
        }
    }

    public float GetGraceThresholdSeconds()
    {
        float rawGraceThresholdSeconds;
        try
        {
            rawGraceThresholdSeconds = this.graceThresholdProvider();
        }
        catch
        {
            return DefaultGraceThresholdSeconds;
        }

        return NormalizeGraceThresholdSeconds(rawGraceThresholdSeconds);
    }

    public static float NormalizeGraceThresholdSeconds(float graceThresholdSeconds)
    {
        if (float.IsNaN(graceThresholdSeconds))
        {
            return DefaultGraceThresholdSeconds;
        }

        if (float.IsNegativeInfinity(graceThresholdSeconds))
        {
            return MinGraceThresholdSeconds;
        }

        if (float.IsPositiveInfinity(graceThresholdSeconds))
        {
            return MaxGraceThresholdSeconds;
        }

        return Math.Clamp(graceThresholdSeconds, MinGraceThresholdSeconds, MaxGraceThresholdSeconds);
    }

    public static string GetRank(int comboCount)
    {
        return comboCount switch
        {
            >= 152 => "SSS",
            >= 100 => "SS",
            >= 50 => "S",
            >= 25 => "A",
            >= 16 => "B",
            >= 8 => "C",
            _ => "D",
        };
    }

    private StyleMeterSnapshot CreateSnapshotUnsafe(DateTime now)
    {
        var isFading = false;
        if (!this.isActive && this.comboCount > 0 && this.lastEndedTimeUtc != DateTime.MinValue)
        {
            var elapsedSeconds = (now - this.lastEndedTimeUtc).TotalSeconds;
            isFading = elapsedSeconds >= 0 && elapsedSeconds <= FadeDurationSeconds;
        }

        var displayComboCount = isFading || this.isActive ? this.comboCount : 0;
        var displayOffGlobalCooldownCount = isFading || this.isActive ? this.offGlobalCooldownCount : 0;
        var displayChainCount = displayComboCount + displayOffGlobalCooldownCount;

        return new StyleMeterSnapshot(
            displayComboCount,
            this.rank,
            this.isActive,
            isFading,
            this.currentRecastSeconds,
            this.GetGraceThresholdSeconds(),
            this.lastGcdTimeUtc,
            this.expirationTimeUtc,
            this.lastEndedTimeUtc,
            displayOffGlobalCooldownCount,
            displayChainCount,
            this.bestComboCount);
    }

    private static bool IsUsableRecastSeconds(float recastSeconds)
    {
        return recastSeconds > 0 &&
               !float.IsNaN(recastSeconds) &&
               !float.IsInfinity(recastSeconds);
    }

    private static bool TryCreateExpirationTime(DateTime now, float durationSeconds, out DateTime expirationTime)
    {
        expirationTime = DateTime.MinValue;

        if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0)
        {
            return false;
        }

        try
        {
            expirationTime = now.AddSeconds(durationSeconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool IsDuplicateCooldownUnsafe(uint actionId, DateTime now)
    {
        return actionId == this.lastObservedActionId &&
               (now - this.lastObservedActionTimeUtc).TotalSeconds <= DuplicateCooldownWindowSeconds;
    }

    private bool IsDuplicateOffGlobalCooldownUnsafe(uint actionId, DateTime now)
    {
        return actionId == this.lastObservedOffGlobalCooldownActionId &&
               (now - this.lastObservedOffGlobalCooldownActionTimeUtc).TotalSeconds <= DuplicateCooldownWindowSeconds;
    }

    private void EndComboUnsafe(DateTime now)
    {
        this.isActive = false;
        this.lastEndedTimeUtc = now;
    }

    private void ClearUnsafe()
    {
        this.comboCount = 0;
        this.rank = "D";
        this.isActive = false;
        this.lastGcdTimeUtc = DateTime.MinValue;
        this.expirationTimeUtc = DateTime.MinValue;
        this.lastEndedTimeUtc = DateTime.MinValue;
        this.currentRecastSeconds = 0;
        this.lastObservedActionId = 0;
        this.lastObservedActionTimeUtc = DateTime.MinValue;
        this.offGlobalCooldownCount = 0;
        this.lastObservedOffGlobalCooldownActionId = 0;
        this.lastObservedOffGlobalCooldownActionTimeUtc = DateTime.MinValue;
        this.bestComboCount = 0;
    }
}

internal interface IStyleMeterClock
{
    DateTime UtcNow { get; }
}

internal sealed class SystemStyleMeterClock : IStyleMeterClock
{
    public static SystemStyleMeterClock Instance { get; } = new();

    private SystemStyleMeterClock()
    {
    }

    public DateTime UtcNow => DateTime.UtcNow;
}
