using StyleMeter.Actions;
using StyleMeter.Interop;
using StyleMeter.Tracking;

namespace StyleMeter.Tests;

public sealed class StyleMeterTrackerOrchestrationTests
{
    private static readonly DateTime StartTime = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Self_action_effect_records_resolved_gcd()
    {
        using var harness = TrackerHarness.Create();

        harness.ActionEffectSource.Raise(SelfAction(100));

        Assert.Equal(1, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(1, harness.Tracker.CurrentSnapshot.BestComboCount);
        Assert.True(harness.Tracker.CurrentSnapshot.IsActive);
        Assert.Equal(1, harness.ActionResolver.CallCount);
        Assert.Equal(1, harness.RecastProvider.CallCount);
        Assert.Equal(1, harness.ActionEffectSource.EnableCalls);
    }

    [Fact]
    public void Non_self_action_effect_is_ignored_before_resolution()
    {
        using var harness = TrackerHarness.Create();

        harness.ActionEffectSource.Raise(SelfAction(100) with { CasterEntityId = 0xBEEF });

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(0, harness.ActionResolver.CallCount);
        Assert.Equal(0, harness.RecastProvider.CallCount);
    }

    [Fact]
    public void Null_header_action_effect_is_skipped_before_resolution()
    {
        using var harness = TrackerHarness.Create();

        harness.ActionEffectSource.Raise(ObservedActionEffect.NullHeader(0xCAFE));

        AssertNoResolutionAttempt(harness);
        Assert.Contains("null header", harness.Diagnostics.SkippedReasons.Single());
    }

    [Fact]
    public void Non_action_kind_is_skipped_before_resolution()
    {
        using var harness = TrackerHarness.Create();

        harness.ActionEffectSource.Raise(SelfAction(100) with { ActionKind = StyleMeterActionKind.Other });

        AssertNoResolutionAttempt(harness);
        Assert.Contains("not action kind Action", harness.Diagnostics.SkippedReasons.Single());
    }

    [Fact]
    public void Disabled_tracking_state_clears_existing_combo()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionEffectSource.Raise(SelfAction(100));
        Assert.Equal(1, harness.Tracker.CurrentSnapshot.ComboCount);

        harness.GameState.CanTrackValue = false;
        harness.ActionEffectSource.Raise(SelfAction(101));

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.False(harness.Tracker.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void Leaving_combat_clears_combo_and_best_combo()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionEffectSource.Raise(SelfAction(100));
        harness.Clock.Advance(0.16);
        harness.ActionEffectSource.Raise(SelfAction(101));
        Assert.Equal(2, harness.Tracker.CurrentSnapshot.BestComboCount);

        harness.GameState.IsInCombatValue = false;
        harness.FrameworkUpdateSource.Raise();

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(0, harness.Tracker.CurrentSnapshot.BestComboCount);
        Assert.False(harness.Tracker.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void Out_of_combat_gcd_does_not_update_best_combo()
    {
        using var harness = TrackerHarness.Create();
        harness.GameState.IsInCombatValue = false;

        harness.ActionEffectSource.Raise(SelfAction(100));

        Assert.Equal(1, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(0, harness.Tracker.CurrentSnapshot.BestComboCount);
    }

    [Fact]
    public void Unresolved_action_effect_does_not_mutate_combo()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionResolver.ResolveGcd = (_, _) => (false, default, "none");

        harness.ActionEffectSource.Raise(SelfAction(100));

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(0, harness.RecastProvider.CallCount);
        Assert.Contains("not tracked PvE uptime or oGCD action", harness.Diagnostics.SkippedReasons.Single());
    }

    [Fact]
    public void Self_off_global_cooldown_action_adds_visual_chain_without_touching_recast()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionEffectSource.Raise(SelfAction(100));
        var firstExpiration = harness.Tracker.CurrentSnapshot.ExpirationTimeUtc;

        harness.ActionResolver.ResolveGcd = (actionId, spellId) => actionId == 200
            ? (false, default, "ability")
            : (true, CreateResolvedAction(actionId, spellId), string.Empty);
        harness.ActionResolver.ResolveOffGlobalCooldown = (actionId, spellId) => actionId == 200
            ? (true, CreateResolvedAction(actionId, spellId, actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId, cooldownGroup: 10), string.Empty)
            : (false, default, "not-ogcd");

        harness.Clock.Advance(0.5);
        harness.ActionEffectSource.Raise(SelfAction(200));

        var snapshot = harness.Tracker.CurrentSnapshot;
        Assert.Equal(1, snapshot.ComboCount);
        Assert.Equal(1, snapshot.OffGlobalCooldownCount);
        Assert.Equal(2, snapshot.ChainCount);
        Assert.Equal(firstExpiration, snapshot.ExpirationTimeUtc);
        Assert.Equal(1, harness.RecastProvider.CallCount);
        Assert.Equal(1, harness.ActionResolver.OffGlobalCooldownCallCount);
        Assert.Single(harness.Diagnostics.TrackedOffGlobalCooldownSnapshots);
    }

    [Fact]
    public void Off_global_cooldown_before_combo_is_ignored()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionResolver.ResolveGcd = (_, _) => (false, default, "ability");
        harness.ActionResolver.ResolveOffGlobalCooldown = (actionId, spellId) =>
            (true, CreateResolvedAction(actionId, spellId, actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId, cooldownGroup: 10), string.Empty);

        harness.ActionEffectSource.Raise(SelfAction(200));

        var snapshot = harness.Tracker.CurrentSnapshot;
        Assert.Equal(0, snapshot.ComboCount);
        Assert.Equal(0, snapshot.OffGlobalCooldownCount);
        Assert.Equal(0, snapshot.ChainCount);
        Assert.Equal(0, harness.RecastProvider.CallCount);
        Assert.Contains("oGCD ignored", harness.Diagnostics.SkippedReasons.Single());
    }

    [Fact]
    public void Framework_tick_defers_expiration_while_tracked_hardcast_is_active()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionEffectSource.Raise(SelfAction(100));

        harness.Clock.Set(StartTime.AddSeconds(2.9));
        harness.GameState.CurrentCastValue = new StyleMeterCastState(StyleMeterActionKind.Action, 200, 2.0f, 3.0f);

        harness.FrameworkUpdateSource.Raise();

        Assert.True(harness.Tracker.CurrentSnapshot.ExpirationTimeUtc > StartTime.AddSeconds(3));

        harness.GameState.CurrentCastValue = default;
        harness.Clock.Set(StartTime.AddSeconds(3.1));
        harness.FrameworkUpdateSource.Raise();

        Assert.True(harness.Tracker.CurrentSnapshot.IsActive);
    }

    [Fact]
    public void Reset_request_clears_combo()
    {
        using var harness = TrackerHarness.Create();
        harness.ActionEffectSource.Raise(SelfAction(100));
        Assert.Equal(1, harness.Tracker.CurrentSnapshot.ComboCount);

        harness.GameState.RaiseResetRequested();

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
    }

    [Fact]
    public void Disposed_tracker_unsubscribes_and_disposes_sources()
    {
        var harness = TrackerHarness.Create();

        harness.Tracker.Dispose();
        harness.ActionEffectSource.Raise(SelfAction(100));
        harness.FrameworkUpdateSource.Raise();
        harness.GameState.RaiseResetRequested();

        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(1, harness.ActionEffectSource.DisposeCalls);
        Assert.Equal(1, harness.FrameworkUpdateSource.DisposeCalls);
        Assert.Equal(1, harness.GameState.DisposeCalls);

        harness.Dispose();
        Assert.Equal(1, harness.ActionEffectSource.DisposeCalls);
    }

    private static ObservedActionEffect SelfAction(uint actionId)
    {
        return new ObservedActionEffect(0xCAFE, StyleMeterActionKind.Action, actionId, 0, 1, 0.6f, true);
    }

    private static void AssertNoResolutionAttempt(TrackerHarness harness)
    {
        Assert.Equal(0, harness.Tracker.CurrentSnapshot.ComboCount);
        Assert.Equal(0, harness.ActionResolver.CallCount);
        Assert.Equal(0, harness.RecastProvider.CallCount);
    }

    private sealed class TrackerHarness : IDisposable
    {
        private TrackerHarness(
            StyleMeterTracker tracker,
            ManualStyleMeterClock clock,
            FakeActionEffectSource actionEffectSource,
            FakeFrameworkUpdateSource frameworkUpdateSource,
            FakeGameState gameState,
            FakeActionResolver actionResolver,
            FakeRecastProvider recastProvider,
            FakeDiagnostics diagnostics)
        {
            this.Tracker = tracker;
            this.Clock = clock;
            this.ActionEffectSource = actionEffectSource;
            this.FrameworkUpdateSource = frameworkUpdateSource;
            this.GameState = gameState;
            this.ActionResolver = actionResolver;
            this.RecastProvider = recastProvider;
            this.Diagnostics = diagnostics;
        }

        public StyleMeterTracker Tracker { get; }

        public ManualStyleMeterClock Clock { get; }

        public FakeActionEffectSource ActionEffectSource { get; }

        public FakeFrameworkUpdateSource FrameworkUpdateSource { get; }

        public FakeGameState GameState { get; }

        public FakeActionResolver ActionResolver { get; }

        public FakeRecastProvider RecastProvider { get; }

        public FakeDiagnostics Diagnostics { get; }

        public static TrackerHarness Create()
        {
            var clock = new ManualStyleMeterClock(StartTime);
            var actionEffectSource = new FakeActionEffectSource();
            var frameworkUpdateSource = new FakeFrameworkUpdateSource();
            var gameState = new FakeGameState();
            var actionResolver = new FakeActionResolver();
            var recastProvider = new FakeRecastProvider();
            var diagnostics = new FakeDiagnostics();
            var engine = new StyleMeterComboEngine(() => StyleMeterComboEngine.DefaultGraceThresholdSeconds, clock);

            var tracker = new StyleMeterTracker(
                () => StyleMeterComboEngine.DefaultGraceThresholdSeconds,
                actionEffectSource,
                frameworkUpdateSource,
                gameState,
                actionResolver,
                recastProvider,
                diagnostics,
                engine);

            return new TrackerHarness(
                tracker,
                clock,
                actionEffectSource,
                frameworkUpdateSource,
                gameState,
                actionResolver,
                recastProvider,
                diagnostics);
        }

        public void Dispose()
        {
            this.Tracker.Dispose();
        }
    }

    private sealed class FakeActionEffectSource : IActionEffectSource
    {
        public event Action<ObservedActionEffect>? OnActionEffectReceived;

        public int EnableCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public void Enable()
        {
            this.EnableCalls++;
        }

        public void Dispose()
        {
            this.DisposeCalls++;
        }

        public void Raise(ObservedActionEffect actionEffect)
        {
            this.OnActionEffectReceived?.Invoke(actionEffect);
        }
    }

    private sealed class FakeFrameworkUpdateSource : IFrameworkUpdateSource
    {
        public event Action? OnUpdate;

        public int DisposeCalls { get; private set; }

        public void Dispose()
        {
            this.DisposeCalls++;
        }

        public void Raise()
        {
            this.OnUpdate?.Invoke();
        }
    }

    private sealed class FakeGameState : IStyleMeterGameState
    {
        public event Action? OnResetRequested;

        public uint LocalPlayerEntityId { get; set; } = 0xCAFE;

        public bool CanTrackValue { get; set; } = true;

        public bool IsInCombatValue { get; set; } = true;

        public StyleMeterCastState CurrentCastValue { get; set; }

        public int DisposeCalls { get; private set; }

        public bool CanTrack => this.CanTrackValue;

        public bool IsInCombat => this.IsInCombatValue;

        public StyleMeterCastState CurrentCast => this.CurrentCastValue;

        public StyleMeterGameStateSnapshot Snapshot => new(
            true,
            true,
            this.LocalPlayerEntityId,
            false,
            false,
            false,
            this.CanTrack,
            this.IsInCombat);

        public void Dispose()
        {
            this.DisposeCalls++;
        }

        public void RaiseResetRequested()
        {
            this.OnResetRequested?.Invoke();
        }
    }

    private sealed class FakeActionResolver : IActionResolver
    {
        public int CallCount { get; private set; }

        public int OffGlobalCooldownCallCount { get; private set; }

        public Func<uint, ushort, (bool Resolved, ResolvedUptimeAction Action, string Diagnostics)> ResolveGcd { get; set; } =
            (actionId, spellId) => (true, CreateResolvedAction(actionId, spellId), string.Empty);

        public Func<uint, ushort, (bool Resolved, ResolvedUptimeAction Action, string Diagnostics)> ResolveOffGlobalCooldown { get; set; } =
            (_, _) => (false, default, "not-ogcd");

        public bool TryResolveTrackedUptimeAction(
            uint observedActionId,
            ushort spellId,
            out ResolvedUptimeAction action,
            out string candidateDiagnostics)
        {
            this.CallCount++;
            var result = this.ResolveGcd(observedActionId, spellId);
            action = result.Action;
            candidateDiagnostics = result.Diagnostics;
            return result.Resolved;
        }

        public bool TryResolveTrackedOffGlobalCooldownAction(
            uint observedActionId,
            ushort spellId,
            out ResolvedUptimeAction action,
            out string candidateDiagnostics)
        {
            this.OffGlobalCooldownCallCount++;
            var result = this.ResolveOffGlobalCooldown(observedActionId, spellId);
            action = result.Action;
            candidateDiagnostics = result.Diagnostics;
            return result.Resolved;
        }
    }

    private sealed class FakeRecastProvider : IRecastProvider
    {
        public int CallCount { get; private set; }

        public RecastInfo RecastInfo { get; set; } = new(2.5f, "fake");

        public RecastInfo GetUptimeRecastSeconds(ResolvedUptimeAction action)
        {
            this.CallCount++;
            return this.RecastInfo;
        }
    }

    private sealed class FakeDiagnostics : IStyleMeterDiagnostics
    {
        public List<string> SkippedReasons { get; } = [];

        public List<StyleMeterSnapshot> TrackedOffGlobalCooldownSnapshots { get; } = [];

        public void LogDiagnostics(string reason, StyleMeterGameStateSnapshot gameState, StyleMeterSnapshot snapshot)
        {
        }

        public void LogObservedActionEffect(ObservedActionEffect actionEffect, uint localEntityId, bool canTrack, bool isSelf)
        {
        }

        public void LogSkippedActionEffect(ObservedActionEffect actionEffect, string reason)
        {
            this.SkippedReasons.Add(reason);
        }

        public void LogTrackedGcd(ObservedActionEffect actionEffect, ResolvedUptimeAction action, RecastInfo recast, StyleMeterSnapshot snapshot)
        {
        }

        public void LogTrackedOffGlobalCooldown(ObservedActionEffect actionEffect, ResolvedUptimeAction action, StyleMeterSnapshot snapshot)
        {
            this.TrackedOffGlobalCooldownSnapshots.Add(snapshot);
        }

        public void LogCastHold(uint actionId, string actionName, float elapsed, float total)
        {
        }
    }

    private static ResolvedUptimeAction CreateResolvedAction(
        uint actionId,
        ushort spellId,
        uint actionCategoryId = 2,
        byte cooldownGroup = StyleMeterActionClassifier.GcdCooldownGroup)
    {
        return new ResolvedUptimeAction(
            actionId,
            spellId,
            actionId,
            $"Action {actionId}",
            true,
            cooldownGroup,
            0,
            actionCategoryId,
            25,
            "fake");
    }
}
