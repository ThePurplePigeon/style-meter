using StyleMeter.Tracking;
using StyleMeter.Windows;

namespace StyleMeter.Tests;

public sealed class StyleMeterOverlayMathTests
{
    private static readonly DateTime StartTime = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0.1f, 0.75f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(1.4f, 1.4f)]
    [InlineData(2.5f, 2.5f)]
    [InlineData(99f, 2.5f)]
    [InlineData(float.NegativeInfinity, 0.75f)]
    [InlineData(float.PositiveInfinity, 2.5f)]
    [InlineData(float.NaN, 1f)]
    public void NormalizeOverlayScale_clamps_invalid_and_extreme_values(float scale, float expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.NormalizeOverlayScale(scale));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Active_layout_drawable_bounds_stay_inside_reserved_canvas(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);

        Assert.True(layout.ContainsDrawableBounds());
        Assert.True(layout.Canvas.Contains(layout.Panel));
        Assert.True(layout.Canvas.Contains(layout.RankMedallion));
        Assert.True(layout.Canvas.Contains(layout.StatusChip));
        Assert.True(layout.Canvas.Contains(layout.BestBlock));
        Assert.True(layout.Canvas.Contains(layout.TimerRail));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Idle_layout_drawable_bounds_stay_inside_reserved_canvas(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateIdle(scale);

        Assert.True(layout.ContainsDrawableBounds());
        Assert.True(layout.Canvas.Contains(layout.Panel));
        Assert.True(layout.Canvas.Contains(layout.RankMedallion));
        Assert.True(layout.Canvas.Contains(layout.StatusChip));
        Assert.True(layout.Canvas.Contains(layout.BestBlock));
    }

    [Theory]
    [InlineData(0.75f, 0f)]
    [InlineData(1f, 0.25f)]
    [InlineData(1.4f, 0.5f)]
    [InlineData(2.5f, 1f)]
    public void Transition_layout_drawable_bounds_stay_inside_reserved_canvas(float scale, float progress)
    {
        var layout = StyleMeterOverlayLayout.CreateTransition(scale, progress);

        Assert.True(layout.ContainsDrawableBounds());
        Assert.True(layout.Canvas.Contains(layout.Panel));
        Assert.True(layout.Canvas.Contains(layout.RankMedallion));
        Assert.True(layout.Canvas.Contains(layout.StatusChip));
        Assert.True(layout.Canvas.Contains(layout.BestBlock));
        Assert.True(layout.Canvas.Contains(layout.TimerRail));
    }

    [Fact]
    public void Transition_layout_matches_active_and_idle_at_endpoints()
    {
        var active = StyleMeterOverlayLayout.CreateActive(1f);
        var idle = StyleMeterOverlayLayout.CreateIdle(1f);

        Assert.Equal(active.Panel.Width, StyleMeterOverlayLayout.CreateTransition(1f, 0f).Panel.Width, 3);
        Assert.Equal(active.Panel.Height, StyleMeterOverlayLayout.CreateTransition(1f, 0f).Panel.Height, 3);
        Assert.Equal(idle.Panel.Width, StyleMeterOverlayLayout.CreateTransition(1f, 1f).Panel.Width, 3);
        Assert.Equal(idle.Panel.Height, StyleMeterOverlayLayout.CreateTransition(1f, 1f).Panel.Height, 3);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Active_layout_reserves_bleed_padding_around_panel(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var expectedBleed = StyleMeterOverlayLayout.Bleed * StyleMeterOverlayMath.NormalizeOverlayScale(scale);

        Assert.Equal(expectedBleed, layout.Panel.Min.X, 3);
        Assert.Equal(expectedBleed, layout.Panel.Min.Y, 3);
        Assert.True(layout.CanvasSize.X > layout.Panel.Width);
        Assert.True(layout.CanvasSize.Y > layout.Panel.Height);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("SS")]
    [InlineData("SSS")]
    public void Rank_text_size_stays_under_default_font_pixelation_threshold(string rank)
    {
        var size = StyleMeterOverlayMath.GetRankTextSize(rank, 2.5f);

        Assert.InRange(size, 8f, 24f);
    }

    [Theory]
    [InlineData("D", 1f)]
    [InlineData("SS", 1f)]
    [InlineData("SSS", 1f)]
    [InlineData("SSS", 2.5f)]
    public void Rank_text_estimate_fits_inside_medallion(string rank, float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var textSize = StyleMeterOverlayMath.EstimateTextSize(rank, StyleMeterOverlayMath.GetRankTextSize(rank, scale));

        Assert.True(textSize.X <= layout.RankMedallion.Width);
        Assert.True(textSize.Y <= layout.RankMedallion.Height);
    }

    [Theory]
    [InlineData(-1, "x0")]
    [InlineData(1, "x1")]
    [InlineData(99, "x99")]
    [InlineData(999, "x999")]
    [InlineData(1000, "x999+")]
    [InlineData(10_000, "x999+")]
    public void FormatComboCount_keeps_large_values_compact(int comboCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatComboCount(comboCount));
    }

    [Theory]
    [InlineData(-1, "x0")]
    [InlineData(1, "x1")]
    [InlineData(999, "x999")]
    [InlineData(1000, "x999+")]
    public void FormatChainCount_keeps_large_values_compact(int chainCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatChainCount(chainCount));
    }

    [Theory]
    [InlineData(-1, "GCD ONLY")]
    [InlineData(0, "GCD ONLY")]
    [InlineData(1, "+1 WEAVE")]
    [InlineData(999, "+999 WEAVE")]
    [InlineData(1000, "+999 WEAVE")]
    public void FormatWeaveCount_keeps_values_compact(int offGlobalCooldownCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatWeaveCount(offGlobalCooldownCount));
    }

    [Theory]
    [InlineData(-1, "W0")]
    [InlineData(0, "W0")]
    [InlineData(1, "W1")]
    [InlineData(999, "W999")]
    [InlineData(1000, "W999")]
    public void FormatWeaveSummary_keeps_values_tiny(int offGlobalCooldownCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatWeaveSummary(offGlobalCooldownCount));
    }

    [Theory]
    [InlineData(-1, "BEST x0")]
    [InlineData(0, "BEST x0")]
    [InlineData(25, "BEST x25")]
    [InlineData(1000, "BEST x999+")]
    public void FormatBestCount_keeps_values_compact(int bestComboCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatBestCount(bestComboCount));
    }

    [Theory]
    [InlineData(0, 0, "GCD ONLY")]
    [InlineData(3, 0, "+3 WEAVE")]
    [InlineData(0, 25, "W0 / BEST x25")]
    [InlineData(3, 25, "W3 / BEST x25")]
    [InlineData(1000, 1000, "W999 / BEST x999+")]
    public void FormatChainDetail_combines_weaves_and_best_combo_compactly(int offGlobalCooldownCount, int bestComboCount, string expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.FormatChainDetail(offGlobalCooldownCount, bestComboCount));
    }

    [Fact]
    public void GetDisplayChainCount_uses_combined_gcd_and_ogcd_count()
    {
        var snapshot = new StyleMeterSnapshot(
            4,
            "D",
            true,
            false,
            2.5f,
            0.5f,
            StartTime,
            StartTime.AddSeconds(3),
            DateTime.MinValue,
            3,
            7);

        Assert.Equal(7, StyleMeterOverlayMath.GetDisplayChainCount(snapshot));
    }

    [Fact]
    public void ShouldDrawIdle_returns_true_after_fade_finishes()
    {
        var endedSnapshot = new StyleMeterSnapshot(
            4,
            "D",
            false,
            false,
            2.5f,
            0.5f,
            StartTime,
            StartTime.AddSeconds(3),
            StartTime.AddSeconds(3),
            2,
            6);

        Assert.True(StyleMeterOverlayMath.ShouldDrawIdle(endedSnapshot));
        Assert.False(StyleMeterOverlayMath.ShouldDrawIdle(FadingSnapshot(StartTime)));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Primary_overlay_text_sizes_stay_readable(float scale)
    {
        Assert.True(StyleMeterOverlayMath.GetComboTextSize(scale) >= 18f);
        Assert.True(StyleMeterOverlayMath.GetLabelTextSize(scale) >= 10f);
        Assert.True(StyleMeterOverlayMath.GetStatusTextSize(scale) >= 9f);
        Assert.True(StyleMeterOverlayMath.GetSubTextSize(scale) >= 9f);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Combo_text_estimate_fits_available_active_layout_space(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var comboText = StyleMeterOverlayMath.FormatComboCount(1000);
        var textSize = StyleMeterOverlayMath.EstimateComboGlyphSize(comboText, scale);
        var availableWidth = layout.ChainLabelPosition.X - layout.ComboPosition.X;

        Assert.True(textSize.X <= availableWidth);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Chain_text_estimate_fits_available_active_layout_space(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var chainText = StyleMeterOverlayMath.FormatChainCount(1000);
        var textSize = StyleMeterOverlayMath.EstimateTextSize(chainText, StyleMeterOverlayMath.GetChainTextSize(scale));
        var availableWidth = layout.StatusChip.Min.X - layout.ChainPosition.X;

        Assert.True(textSize.X <= availableWidth);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.4f)]
    [InlineData(2.5f)]
    public void Best_block_text_estimate_fits_layout_space(float scale)
    {
        var layout = StyleMeterOverlayLayout.CreateActive(scale);
        var labelSize = StyleMeterOverlayMath.EstimateTextSize("BEST", StyleMeterOverlayMath.GetBestLabelTextSize(scale));
        var valueSize = StyleMeterOverlayMath.EstimateTextSize("x999+", StyleMeterOverlayMath.GetBestValueTextSize(scale));
        var expectedWidth = labelSize.X + valueSize.X + (18f * StyleMeterOverlayMath.NormalizeOverlayScale(scale));

        Assert.True(expectedWidth <= layout.BestBlock.Width);
        Assert.True(MathF.Max(labelSize.Y, valueSize.Y) <= layout.BestBlock.Height);
    }

    [Fact]
    public void GetTimerProgress_returns_full_progress_at_window_start()
    {
        var snapshot = ActiveSnapshot(StartTime.AddSeconds(3), 2.5f, 0.5f);

        var progress = StyleMeterOverlayMath.GetTimerProgress(snapshot, StartTime);

        Assert.Equal(1f, progress);
    }

    [Fact]
    public void GetTimerProgress_returns_fractional_progress_inside_window()
    {
        var snapshot = ActiveSnapshot(StartTime.AddSeconds(3), 2.5f, 0.5f);

        var progress = StyleMeterOverlayMath.GetTimerProgress(snapshot, StartTime.AddSeconds(1.5));

        Assert.Equal(0.5f, progress, 3);
    }

    [Fact]
    public void GetTimerProgress_returns_zero_after_expiration()
    {
        var snapshot = ActiveSnapshot(StartTime.AddSeconds(3), 2.5f, 0.5f);

        var progress = StyleMeterOverlayMath.GetTimerProgress(snapshot, StartTime.AddSeconds(4));

        Assert.Equal(0f, progress);
    }

    [Theory]
    [MemberData(nameof(InvalidProgressSnapshots))]
    public void GetTimerProgress_handles_invalid_snapshot_values_without_nan(StyleMeterSnapshot snapshot)
    {
        var progress = StyleMeterOverlayMath.GetTimerProgress(snapshot, StartTime);

        Assert.False(float.IsNaN(progress));
        Assert.False(float.IsInfinity(progress));
        Assert.Equal(0f, progress);
    }

    [Fact]
    public void GetFadeAlpha_returns_one_at_fade_start()
    {
        var snapshot = FadingSnapshot(StartTime);

        var alpha = StyleMeterOverlayMath.GetFadeAlpha(snapshot, StartTime);

        Assert.Equal(1f, alpha);
    }

    [Fact]
    public void GetFadeAlpha_returns_fractional_alpha_during_fade()
    {
        var snapshot = FadingSnapshot(StartTime);

        var alpha = StyleMeterOverlayMath.GetFadeAlpha(snapshot, StartTime.AddSeconds(StyleMeterComboEngine.FadeDurationSeconds / 2));

        Assert.Equal(0.5f, alpha, 3);
    }

    [Fact]
    public void GetFadeAlpha_returns_zero_after_fade_duration()
    {
        var snapshot = FadingSnapshot(StartTime);

        var alpha = StyleMeterOverlayMath.GetFadeAlpha(snapshot, StartTime.AddSeconds(StyleMeterComboEngine.FadeDurationSeconds + 0.001));

        Assert.Equal(0f, alpha);
    }

    [Fact]
    public void GetFadeAlpha_handles_future_last_end_time_without_throwing()
    {
        var snapshot = FadingSnapshot(StartTime.AddSeconds(10));

        var alpha = StyleMeterOverlayMath.GetFadeAlpha(snapshot, StartTime);

        Assert.Equal(1f, alpha);
    }

    [Fact]
    public void GetEndTransitionProgress_clamps_to_unit_interval()
    {
        var snapshot = FadingSnapshot(StartTime);

        Assert.Equal(0f, StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, StartTime.AddSeconds(-1)));
        Assert.Equal(0f, StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, StartTime));
        Assert.Equal(0.5f, StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, StartTime.AddSeconds(StyleMeterComboEngine.FadeDurationSeconds / 2)), 3);
        Assert.Equal(1f, StyleMeterOverlayMath.GetEndTransitionProgress(snapshot, StartTime.AddSeconds(StyleMeterComboEngine.FadeDurationSeconds + 1)));
    }

    [Theory]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(float.PositiveInfinity)]
    public void Ending_transition_alphas_are_finite_and_clamped(float progress)
    {
        var activeAlpha = StyleMeterOverlayMath.GetEndingActiveContentAlpha(progress);
        var idleAlpha = StyleMeterOverlayMath.GetEndingIdleContentAlpha(progress);
        var layoutProgress = StyleMeterOverlayMath.GetEndLayoutProgress(progress);

        Assert.False(float.IsNaN(activeAlpha));
        Assert.False(float.IsInfinity(activeAlpha));
        Assert.False(float.IsNaN(idleAlpha));
        Assert.False(float.IsInfinity(idleAlpha));
        Assert.False(float.IsNaN(layoutProgress));
        Assert.False(float.IsInfinity(layoutProgress));
        Assert.InRange(activeAlpha, 0f, 1f);
        Assert.InRange(idleAlpha, 0f, 1f);
        Assert.InRange(layoutProgress, 0f, 1f);
    }

    [Fact]
    public void Ending_transition_crossfades_active_out_and_idle_in()
    {
        Assert.Equal(1f, StyleMeterOverlayMath.GetEndingActiveContentAlpha(0f));
        Assert.Equal(0f, StyleMeterOverlayMath.GetEndingIdleContentAlpha(0f));
        Assert.Equal(0f, StyleMeterOverlayMath.GetEndingActiveContentAlpha(1f));
        Assert.Equal(1f, StyleMeterOverlayMath.GetEndingIdleContentAlpha(1f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void GetPulseIntensity_returns_safe_unit_interval_values(double animationTimeSeconds)
    {
        var pulse = StyleMeterOverlayMath.GetPulseIntensity(animationTimeSeconds);

        Assert.False(float.IsNaN(pulse));
        Assert.False(float.IsInfinity(pulse));
        Assert.InRange(pulse, 0f, 1f);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(0.001f, true)]
    [InlineData(StyleMeterOverlayMath.TimerDangerThreshold, true)]
    [InlineData(0.23f, false)]
    [InlineData(1f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsTimerDanger_uses_expected_low_timer_threshold(float progress, bool expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.IsTimerDanger(progress));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.05f)]
    [InlineData(StyleMeterOverlayMath.TimerDangerThreshold)]
    [InlineData(0.5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void GetTimerDangerIntensity_returns_safe_unit_interval_values(float progress)
    {
        var danger = StyleMeterOverlayMath.GetTimerDangerIntensity(progress, 0.5f);

        Assert.False(float.IsNaN(danger));
        Assert.False(float.IsInfinity(danger));
        Assert.InRange(danger, 0f, 1f);
    }

    [Fact]
    public void GetTimerDangerIntensity_is_zero_outside_danger_state()
    {
        Assert.Equal(0f, StyleMeterOverlayMath.GetTimerDangerIntensity(0.5f, 1f));
        Assert.Equal(0f, StyleMeterOverlayMath.GetTimerDangerIntensity(0f, 1f));
    }

    [Fact]
    public void GetTimerDangerIntensity_is_positive_inside_danger_state()
    {
        Assert.True(StyleMeterOverlayMath.GetTimerDangerIntensity(0.1f, 0.5f) > 0f);
    }

    [Fact]
    public void GetTimerColor_shifts_toward_danger_color()
    {
        var rankColor = StyleMeterOverlayPalette.GetRankColor("A", 1f);
        var safeColor = StyleMeterOverlayPalette.GetTimerColor(rankColor, 0f, 1f);
        var dangerColor = StyleMeterOverlayPalette.GetTimerColor(rankColor, 1f, 1f);

        Assert.Equal(rankColor, safeColor);
        Assert.True(dangerColor.X >= dangerColor.Y);
        Assert.True(dangerColor.X >= dangerColor.Z);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(16, true)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(152, true)]
    [InlineData(153, false)]
    public void IsMilestoneComboCount_matches_rank_thresholds(int comboCount, bool expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.IsMilestoneComboCount(comboCount));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(7, 8, true)]
    [InlineData(8, 8, false)]
    [InlineData(9, 8, false)]
    [InlineData(15, 16, true)]
    [InlineData(99, 100, true)]
    [InlineData(152, 153, false)]
    public void ShouldTriggerMilestoneFlash_only_triggers_when_entering_milestones(
        int previousComboCount,
        int currentComboCount,
        bool expected)
    {
        Assert.Equal(expected, StyleMeterOverlayMath.ShouldTriggerMilestoneFlash(previousComboCount, currentComboCount));
    }

    [Fact]
    public void GetMilestoneFlashIntensity_fades_over_flash_duration()
    {
        Assert.Equal(1f, StyleMeterOverlayMath.GetMilestoneFlashIntensity(10, 10));
        Assert.Equal(0.5f, StyleMeterOverlayMath.GetMilestoneFlashIntensity(10, 10 + (StyleMeterOverlayMath.MilestoneFlashDurationSeconds / 2)), 3);
        Assert.Equal(0f, StyleMeterOverlayMath.GetMilestoneFlashIntensity(10, 10 + StyleMeterOverlayMath.MilestoneFlashDurationSeconds));
    }

    [Theory]
    [InlineData(double.NegativeInfinity, 10)]
    [InlineData(double.NaN, 10)]
    [InlineData(11, 10)]
    public void GetMilestoneFlashIntensity_handles_invalid_times(double triggerTimeSeconds, double animationTimeSeconds)
    {
        Assert.Equal(0f, StyleMeterOverlayMath.GetMilestoneFlashIntensity(triggerTimeSeconds, animationTimeSeconds));
    }

    [Fact]
    public void Animation_state_triggers_once_when_combo_enters_milestone()
    {
        var state = new StyleMeterOverlayAnimationState();

        Assert.Equal(0f, state.Update(ActiveSnapshot(1), 0).MilestoneFlashIntensity);
        Assert.Equal(1f, state.Update(ActiveSnapshot(8), 1).MilestoneFlashIntensity);

        var repeated = state.Update(ActiveSnapshot(8), 1.1).MilestoneFlashIntensity;

        Assert.InRange(repeated, 0f, 1f);
        Assert.True(repeated < 1f);
    }

    [Fact]
    public void Animation_state_clears_on_idle_snapshot()
    {
        var state = new StyleMeterOverlayAnimationState();

        Assert.Equal(1f, state.Update(ActiveSnapshot(8), 1).MilestoneFlashIntensity);
        Assert.Equal(0f, state.Update(IdleSnapshot(), 1.1).MilestoneFlashIntensity);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("C")]
    [InlineData("B")]
    [InlineData("A")]
    [InlineData("S")]
    [InlineData("SS")]
    [InlineData("SSS")]
    [InlineData("unknown")]
    public void GetRankColor_returns_visible_finite_colors(string rank)
    {
        var color = StyleMeterOverlayPalette.GetRankColor(rank, 1f);

        Assert.InRange(color.X, 0f, 1f);
        Assert.InRange(color.Y, 0f, 1f);
        Assert.InRange(color.Z, 0f, 1f);
        Assert.Equal(1f, color.W);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(2f, 1f)]
    [InlineData(float.NaN, 1f)]
    public void GetRankColor_clamps_alpha(float alpha, float expectedAlpha)
    {
        var color = StyleMeterOverlayPalette.GetRankColor("S", alpha);

        Assert.Equal(expectedAlpha, color.W);
    }

    [Fact]
    public void ToU32_handles_invalid_color_channels_without_throwing()
    {
        var color = new System.Numerics.Vector4(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 2f);

        var exception = Record.Exception(() => StyleMeterOverlayPalette.ToU32(color));

        Assert.Null(exception);
    }

    public static TheoryData<StyleMeterSnapshot> InvalidProgressSnapshots => new()
    {
        ActiveSnapshot(StartTime.AddSeconds(3), 0f, 0.5f),
        ActiveSnapshot(StartTime.AddSeconds(3), -1f, 0.5f),
        ActiveSnapshot(StartTime.AddSeconds(3), float.NaN, 0.5f),
        ActiveSnapshot(StartTime.AddSeconds(3), float.PositiveInfinity, 0.5f),
        ActiveSnapshot(StartTime.AddSeconds(3), 2.5f, float.NaN),
        ActiveSnapshot(StartTime.AddSeconds(3), 2.5f, float.PositiveInfinity),
        new(
            1,
            "D",
            false,
            false,
            2.5f,
            0.5f,
            StartTime,
            StartTime.AddSeconds(3),
            DateTime.MinValue),
    };

    private static StyleMeterSnapshot ActiveSnapshot(DateTime expirationTimeUtc, float recastSeconds, float graceThresholdSeconds)
    {
        return new StyleMeterSnapshot(
            1,
            "D",
            true,
            false,
            recastSeconds,
            graceThresholdSeconds,
            StartTime,
            expirationTimeUtc,
            DateTime.MinValue);
    }

    private static StyleMeterSnapshot ActiveSnapshot(int comboCount)
    {
        return new StyleMeterSnapshot(
            comboCount,
            StyleMeterComboEngine.GetRank(comboCount),
            true,
            false,
            2.5f,
            0.5f,
            StartTime,
            StartTime.AddSeconds(3),
            DateTime.MinValue);
    }

    private static StyleMeterSnapshot IdleSnapshot()
    {
        return new StyleMeterSnapshot(
            0,
            "D",
            false,
            false,
            0,
            0.5f,
            DateTime.MinValue,
            DateTime.MinValue,
            DateTime.MinValue);
    }

    private static StyleMeterSnapshot FadingSnapshot(DateTime lastEndedTimeUtc)
    {
        return new StyleMeterSnapshot(
            1,
            "D",
            false,
            true,
            2.5f,
            0.5f,
            StartTime,
            StartTime.AddSeconds(3),
            lastEndedTimeUtc);
    }
}
