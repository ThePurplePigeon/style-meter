using StyleMeter.Tracking;

namespace StyleMeter.Tests;

public sealed class StyleMeterComboEngineFunctionalTests
{
    private static readonly DateTime StartTime = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CurrentSnapshot_starts_empty()
    {
        var engine = CreateEngine(out _);

        var snapshot = engine.CurrentSnapshot;

        Assert.Equal(0, snapshot.ComboCount);
        Assert.Equal("D", snapshot.Rank);
        Assert.False(snapshot.IsActive);
        Assert.False(snapshot.IsFading);
        Assert.Equal(0, snapshot.OffGlobalCooldownCount);
        Assert.Equal(0, snapshot.ChainCount);
        Assert.Equal(0, snapshot.BestComboCount);
        Assert.Equal(0, snapshot.CurrentRecastSeconds);
        Assert.Equal(StyleMeterComboEngine.DefaultGraceThresholdSeconds, snapshot.GraceThresholdSeconds);
        Assert.Equal(DateTime.MinValue, snapshot.LastGcdTimeUtc);
        Assert.Equal(DateTime.MinValue, snapshot.ExpirationTimeUtc);
        Assert.Equal(DateTime.MinValue, snapshot.LastEndedTimeUtc);
    }

    [Fact]
    public void TryRecordGcd_starts_combo_and_sets_expiration()
    {
        var engine = CreateEngine(out var clock);

        var changed = engine.TryRecordGcd(100, 2.5f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(1, snapshot.ComboCount);
        Assert.Equal("D", snapshot.Rank);
        Assert.True(snapshot.IsActive);
        Assert.False(snapshot.IsFading);
        Assert.Equal(0, snapshot.OffGlobalCooldownCount);
        Assert.Equal(1, snapshot.ChainCount);
        Assert.Equal(1, snapshot.BestComboCount);
        Assert.Equal(2.5f, snapshot.CurrentRecastSeconds);
        Assert.Equal(0.5f, snapshot.GraceThresholdSeconds);
        Assert.Equal(clock.UtcNow, snapshot.LastGcdTimeUtc);
        Assert.Equal(StartTime.AddSeconds(3), snapshot.ExpirationTimeUtc);
    }

    [Fact]
    public void TryRecordGcd_before_expiration_increments_and_resets_recast_window()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Advance(2.9);
        var changed = engine.TryRecordGcd(101, 2.2f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(2, snapshot.ComboCount);
        Assert.Equal(2, snapshot.ChainCount);
        Assert.Equal(2, snapshot.BestComboCount);
        Assert.Equal("D", snapshot.Rank);
        Assert.Equal(2.2f, snapshot.CurrentRecastSeconds);
        Assert.Equal(clock.UtcNow.AddSeconds(2.7), snapshot.ExpirationTimeUtc);
    }

    [Fact]
    public void TryRecordGcd_at_exact_expiration_still_continues_combo()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3));
        var changed = engine.TryRecordGcd(101, 2.5f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(2, snapshot.ComboCount);
        Assert.True(snapshot.IsActive);
    }

    [Fact]
    public void TryRecordGcd_after_expiration_resets_combo_to_one()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        var changed = engine.TryRecordGcd(101, 2.5f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(1, snapshot.ComboCount);
        Assert.Equal("D", snapshot.Rank);
        Assert.True(snapshot.IsActive);
        Assert.Equal(clock.UtcNow.AddSeconds(3), snapshot.ExpirationTimeUtc);
    }

    [Theory]
    [InlineData(2.999)]
    [InlineData(3.0)]
    public void Tick_at_or_before_expiration_keeps_combo_active(double elapsedSeconds)
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(elapsedSeconds));
        var changed = engine.Tick();

        Assert.False(changed);
        Assert.True(engine.CurrentSnapshot.IsActive);
        Assert.False(engine.CurrentSnapshot.IsFading);
    }

    [Fact]
    public void Tick_after_expiration_ends_combo_and_starts_fade()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        var changed = engine.Tick();
        var snapshot = engine.CurrentSnapshot;

        Assert.True(changed);
        Assert.Equal(1, snapshot.ComboCount);
        Assert.False(snapshot.IsActive);
        Assert.True(snapshot.IsFading);
        Assert.Equal(clock.UtcNow, snapshot.LastEndedTimeUtc);
    }

    [Fact]
    public void DeferExpiration_keeps_combo_active_for_tracked_hardcast()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(2.9));
        Assert.True(engine.DeferExpiration(0.35f));

        clock.Set(StartTime.AddSeconds(3.1));
        Assert.False(engine.Tick());
        Assert.True(engine.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void DeferExpiration_does_not_revive_inactive_combo()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        Assert.True(engine.Tick());

        Assert.False(engine.DeferExpiration(0.35f));
        Assert.False(engine.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void CurrentSnapshot_after_fade_duration_stops_fading()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        Assert.True(engine.Tick());
        clock.Advance(StyleMeterComboEngine.FadeDurationSeconds + 0.001);

        Assert.False(engine.CurrentSnapshot.IsFading);
        Assert.Equal(0, engine.CurrentSnapshot.ComboCount);
        Assert.Equal(0, engine.CurrentSnapshot.ChainCount);
        Assert.Equal(1, engine.CurrentSnapshot.BestComboCount);
    }

    [Fact]
    public void TryRecordOffGlobalCooldown_counts_weaves_without_incrementing_combo_or_recast()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out var gcdSnapshot));

        clock.Advance(0.7);
        var changed = engine.TryRecordOffGlobalCooldown(200, out var weaveSnapshot);

        Assert.True(changed);
        Assert.Equal(1, weaveSnapshot.ComboCount);
        Assert.Equal(1, weaveSnapshot.OffGlobalCooldownCount);
        Assert.Equal(2, weaveSnapshot.ChainCount);
        Assert.Equal(1, weaveSnapshot.BestComboCount);
        Assert.Equal(gcdSnapshot.CurrentRecastSeconds, weaveSnapshot.CurrentRecastSeconds);
        Assert.Equal(gcdSnapshot.ExpirationTimeUtc, weaveSnapshot.ExpirationTimeUtc);
    }

    [Fact]
    public void TryRecordOffGlobalCooldown_is_ignored_when_combo_is_inactive()
    {
        var engine = CreateEngine(out _);

        var changed = engine.TryRecordOffGlobalCooldown(200, out var snapshot);

        Assert.False(changed);
        Assert.Equal(0, snapshot.ComboCount);
        Assert.Equal(0, snapshot.OffGlobalCooldownCount);
        Assert.Equal(0, snapshot.ChainCount);
        Assert.Equal(0, snapshot.BestComboCount);
    }

    [Fact]
    public void TryRecordOffGlobalCooldown_after_expiration_ends_combo_without_extending_it()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        var changed = engine.TryRecordOffGlobalCooldown(200, out var snapshot);

        Assert.False(changed);
        Assert.False(snapshot.IsActive);
        Assert.True(snapshot.IsFading);
        Assert.Equal(1, snapshot.ComboCount);
        Assert.Equal(1, snapshot.ChainCount);
    }

    [Fact]
    public void Duplicate_off_global_cooldown_inside_debounce_window_is_ignored()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));
        Assert.True(engine.TryRecordOffGlobalCooldown(200, out var first));

        clock.Advance(StyleMeterComboEngine.DuplicateCooldownWindowSeconds - 0.001);
        var changed = engine.TryRecordOffGlobalCooldown(200, out var duplicate);

        Assert.False(changed);
        Assert.Equal(first.OffGlobalCooldownCount, duplicate.OffGlobalCooldownCount);
        Assert.Equal(first.ChainCount, duplicate.ChainCount);
    }

    [Fact]
    public void New_combo_resets_off_global_cooldown_count()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));
        Assert.True(engine.TryRecordOffGlobalCooldown(200, out _));

        clock.Set(StartTime.AddSeconds(3.001));
        Assert.True(engine.TryRecordGcd(101, 2.5f, out var snapshot));

        Assert.Equal(1, snapshot.ComboCount);
        Assert.Equal(0, snapshot.OffGlobalCooldownCount);
        Assert.Equal(1, snapshot.ChainCount);
        Assert.Equal(1, snapshot.BestComboCount);
    }

    [Fact]
    public void Best_combo_count_preserves_highest_reached_combo_until_clear()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));
        clock.Advance(0.16);
        Assert.True(engine.TryRecordGcd(101, 2.5f, out _));
        clock.Advance(0.16);
        Assert.True(engine.TryRecordGcd(102, 2.5f, out var peak));
        Assert.Equal(3, peak.BestComboCount);

        clock.Set(peak.ExpirationTimeUtc.AddMilliseconds(1));
        Assert.True(engine.TryRecordGcd(103, 2.5f, out var resetCombo));

        Assert.Equal(1, resetCombo.ComboCount);
        Assert.Equal(3, resetCombo.BestComboCount);

        engine.Clear();
        Assert.Equal(0, engine.CurrentSnapshot.BestComboCount);
    }

    [Fact]
    public void TryRecordGcd_can_skip_combat_best_updates_for_out_of_combat_actions()
    {
        var engine = CreateEngine(out var clock);

        Assert.True(engine.TryRecordGcd(100, 2.5f, false, out var first));
        clock.Advance(0.16);
        Assert.True(engine.TryRecordGcd(101, 2.5f, false, out var second));

        Assert.Equal(1, first.ComboCount);
        Assert.Equal(2, second.ComboCount);
        Assert.Equal(0, second.BestComboCount);
    }

    [Fact]
    public void Clear_resets_combo_state()
    {
        var engine = CreateEngine(out _);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        engine.Clear();
        var snapshot = engine.CurrentSnapshot;

        Assert.Equal(0, snapshot.ComboCount);
        Assert.Equal("D", snapshot.Rank);
        Assert.False(snapshot.IsActive);
        Assert.False(snapshot.IsFading);
        Assert.Equal(0, snapshot.CurrentRecastSeconds);
        Assert.Equal(DateTime.MinValue, snapshot.LastGcdTimeUtc);
        Assert.Equal(DateTime.MinValue, snapshot.ExpirationTimeUtc);
        Assert.Equal(DateTime.MinValue, snapshot.LastEndedTimeUtc);
    }

    [Fact]
    public void Duplicate_same_action_inside_debounce_window_is_ignored()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out var first));

        clock.Advance(StyleMeterComboEngine.DuplicateCooldownWindowSeconds - 0.001);
        var changed = engine.TryRecordGcd(100, 1.5f, out var duplicate);

        Assert.False(changed);
        Assert.Equal(1, duplicate.ComboCount);
        Assert.Equal(first.ExpirationTimeUtc, duplicate.ExpirationTimeUtc);
        Assert.Equal(first.CurrentRecastSeconds, duplicate.CurrentRecastSeconds);
    }

    [Fact]
    public void Same_action_after_debounce_window_can_increment()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Advance(StyleMeterComboEngine.DuplicateCooldownWindowSeconds + 0.001);
        var changed = engine.TryRecordGcd(100, 2.5f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(2, snapshot.ComboCount);
    }

    [Fact]
    public void Different_action_inside_debounce_window_can_increment()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Advance(0.001);
        var changed = engine.TryRecordGcd(101, 2.5f, out var snapshot);

        Assert.True(changed);
        Assert.Equal(2, snapshot.ComboCount);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(99, 2)]
    public void Grace_threshold_is_clamped_for_normal_values(float configuredGrace, float expectedGrace)
    {
        var engine = CreateEngine(out _, () => configuredGrace);

        Assert.True(engine.TryRecordGcd(100, 2.5f, out var snapshot));

        Assert.Equal(expectedGrace, snapshot.GraceThresholdSeconds);
        Assert.Equal(StartTime.AddSeconds(2.5 + expectedGrace), snapshot.ExpirationTimeUtc);
    }

    [Theory]
    [MemberData(nameof(SpecialGraceThresholdCases))]
    public void Grace_threshold_handles_special_float_values(float configuredGrace, float expectedGrace)
    {
        var engine = CreateEngine(out _, () => configuredGrace);

        Assert.True(engine.TryRecordGcd(100, 2.5f, out var snapshot));

        Assert.Equal(expectedGrace, snapshot.GraceThresholdSeconds);
        Assert.Equal(StartTime.AddSeconds(2.5 + expectedGrace), snapshot.ExpirationTimeUtc);
    }

    [Fact]
    public void Grace_provider_exception_falls_back_to_default()
    {
        var engine = CreateEngine(out _, () => throw new InvalidOperationException("boom"));

        Assert.True(engine.TryRecordGcd(100, 2.5f, out var snapshot));

        Assert.Equal(StyleMeterComboEngine.DefaultGraceThresholdSeconds, snapshot.GraceThresholdSeconds);
        Assert.Equal(StartTime.AddSeconds(3), snapshot.ExpirationTimeUtc);
    }

    [Theory]
    [MemberData(nameof(RankCases))]
    public void GetRank_returns_expected_rank_thresholds(int comboCount, string expectedRank)
    {
        Assert.Equal(expectedRank, StyleMeterComboEngine.GetRank(comboCount));
    }

    [Theory]
    [MemberData(nameof(RankCases))]
    public void Recorded_combo_count_uses_expected_rank_thresholds(int comboCount, string expectedRank)
    {
        var engine = CreateEngine(out var clock);
        var snapshot = engine.CurrentSnapshot;

        for (var i = 1; i <= comboCount; i++)
        {
            Assert.True(engine.TryRecordGcd((uint)i, 2.5f, out snapshot));
            clock.Advance(0.16);
        }

        Assert.Equal(expectedRank, snapshot.Rank);
    }

    [Fact]
    public void Recast_changes_update_current_recast_and_expiration()
    {
        var engine = CreateEngine(out var clock);
        Assert.True(engine.TryRecordGcd(100, 2.5f, out _));

        clock.Advance(1);
        Assert.True(engine.TryRecordGcd(101, 1.94f, out var fastSnapshot));

        Assert.Equal(1.94f, fastSnapshot.CurrentRecastSeconds);
        AssertDateTimeNear(clock.UtcNow.AddSeconds(2.44), fastSnapshot.ExpirationTimeUtc);

        clock.Advance(1);
        Assert.True(engine.TryRecordGcd(102, 3.8f, out var slowSnapshot));

        Assert.Equal(3.8f, slowSnapshot.CurrentRecastSeconds);
        AssertDateTimeNear(clock.UtcNow.AddSeconds(4.3), slowSnapshot.ExpirationTimeUtc);
    }

    public static TheoryData<float, float> SpecialGraceThresholdCases => new()
    {
        { float.NaN, StyleMeterComboEngine.DefaultGraceThresholdSeconds },
        { float.PositiveInfinity, StyleMeterComboEngine.MaxGraceThresholdSeconds },
        { float.NegativeInfinity, StyleMeterComboEngine.MinGraceThresholdSeconds },
    };

    public static TheoryData<int, string> RankCases => new()
    {
        { -1, "D" },
        { 0, "D" },
        { 1, "D" },
        { 7, "D" },
        { 8, "C" },
        { 15, "C" },
        { 16, "B" },
        { 24, "B" },
        { 25, "A" },
        { 49, "A" },
        { 50, "S" },
        { 99, "S" },
        { 100, "SS" },
        { 151, "SS" },
        { 152, "SSS" },
        { 200, "SSS" },
    };

    private static StyleMeterComboEngine CreateEngine(out ManualStyleMeterClock clock, Func<float>? graceThresholdProvider = null)
    {
        clock = new ManualStyleMeterClock(StartTime);
        return new StyleMeterComboEngine(graceThresholdProvider ?? (() => StyleMeterComboEngine.DefaultGraceThresholdSeconds), clock);
    }

    private static void AssertDateTimeNear(DateTime expected, DateTime actual)
    {
        Assert.True(
            (actual - expected).Duration() <= TimeSpan.FromMilliseconds(1),
            $"Expected {actual:O} to be within 1 ms of {expected:O}.");
    }
}
