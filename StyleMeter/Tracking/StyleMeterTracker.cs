using System;
using StyleMeter.Actions;
using StyleMeter.Interop;

namespace StyleMeter.Tracking;

internal sealed class StyleMeterTracker : IDisposable
{
    private const float CastingExpirationHoldSeconds = 0.35f;

    private readonly IActionEffectSource actionEffectSource;
    private readonly IFrameworkUpdateSource frameworkUpdateSource;
    private readonly IStyleMeterGameState gameState;
    private readonly IActionResolver actionResolver;
    private readonly IRecastProvider recastProvider;
    private readonly IStyleMeterDiagnostics diagnostics;
    private readonly StyleMeterComboEngine comboEngine;

    private bool disposed;
    private bool wasInCombat;

    public StyleMeterTracker(
        Func<float> graceThresholdProvider,
        IActionEffectSource actionEffectSource,
        IFrameworkUpdateSource frameworkUpdateSource,
        IStyleMeterGameState gameState,
        IActionResolver actionResolver,
        IRecastProvider recastProvider,
        IStyleMeterDiagnostics diagnostics,
        StyleMeterComboEngine? comboEngine = null)
    {
        ArgumentNullException.ThrowIfNull(graceThresholdProvider);
        this.actionEffectSource = actionEffectSource ?? throw new ArgumentNullException(nameof(actionEffectSource));
        this.frameworkUpdateSource = frameworkUpdateSource ?? throw new ArgumentNullException(nameof(frameworkUpdateSource));
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
        this.recastProvider = recastProvider ?? throw new ArgumentNullException(nameof(recastProvider));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.comboEngine = comboEngine ?? new StyleMeterComboEngine(graceThresholdProvider);
        this.wasInCombat = this.gameState.IsInCombat;

        this.actionEffectSource.OnActionEffectReceived += this.OnActionEffectReceived;
        this.frameworkUpdateSource.OnUpdate += this.OnFrameworkUpdate;
        this.gameState.OnResetRequested += this.Clear;

        this.actionEffectSource.Enable();
    }

    public StyleMeterSnapshot CurrentSnapshot => this.comboEngine.CurrentSnapshot;

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        this.actionEffectSource.OnActionEffectReceived -= this.OnActionEffectReceived;
        this.frameworkUpdateSource.OnUpdate -= this.OnFrameworkUpdate;
        this.gameState.OnResetRequested -= this.Clear;

        this.actionEffectSource.Dispose();
        this.frameworkUpdateSource.Dispose();
        this.gameState.Dispose();
    }

    public void Clear()
    {
        this.comboEngine.Clear();
    }

    public void LogDiagnostics(string reason)
    {
        this.diagnostics.LogDiagnostics(reason, this.gameState.Snapshot, this.comboEngine.CurrentSnapshot);
    }

    private void OnActionEffectReceived(ObservedActionEffect actionEffect)
    {
        if (!actionEffect.HasHeader)
        {
            this.diagnostics.LogSkippedActionEffect(actionEffect, "null header");
            return;
        }

        var localEntityId = this.gameState.LocalPlayerEntityId;
        var canTrack = this.gameState.CanTrack;
        var isSelf = actionEffect.IsSelf(localEntityId);

        this.diagnostics.LogObservedActionEffect(actionEffect, localEntityId, canTrack, isSelf);

        if (!canTrack)
        {
            this.Clear();
            return;
        }

        if (!isSelf)
        {
            this.diagnostics.LogSkippedActionEffect(actionEffect, "not local player");
            return;
        }

        if (actionEffect.ActionKind != StyleMeterActionKind.Action)
        {
            this.diagnostics.LogSkippedActionEffect(actionEffect, "not action kind Action");
            return;
        }

        if (this.actionResolver.TryResolveTrackedUptimeAction(
                actionEffect.ActionId,
                actionEffect.SpellId,
                out var action,
                out var candidateDiagnostics))
        {
            var recast = this.recastProvider.GetUptimeRecastSeconds(action);
            if (this.comboEngine.TryRecordGcd(action.ActionId, recast.Seconds, this.gameState.IsInCombat, out var snapshot))
            {
                this.diagnostics.LogTrackedGcd(actionEffect, action, recast, snapshot);
                return;
            }

            this.diagnostics.LogSkippedActionEffect(actionEffect, $"combo engine rejected resolvedAction={action.ActionId} recast={recast.Seconds:F2}");
            return;
        }

        if (this.actionResolver.TryResolveTrackedOffGlobalCooldownAction(
                actionEffect.ActionId,
                actionEffect.SpellId,
                out var offGlobalCooldownAction,
                out var offGlobalCooldownDiagnostics))
        {
            if (this.comboEngine.TryRecordOffGlobalCooldown(offGlobalCooldownAction.ActionId, out var snapshot))
            {
                this.diagnostics.LogTrackedOffGlobalCooldown(actionEffect, offGlobalCooldownAction, snapshot);
                return;
            }

            this.diagnostics.LogSkippedActionEffect(actionEffect, $"oGCD ignored; combo inactive, expired, or duplicate; resolvedAction={offGlobalCooldownAction.ActionId}");
            return;
        }

        this.diagnostics.LogSkippedActionEffect(actionEffect, $"not tracked PvE uptime or oGCD action; gcdCandidates={candidateDiagnostics}; ogcdCandidates={offGlobalCooldownDiagnostics}");
    }

    private void OnFrameworkUpdate()
    {
        this.ResetIfCombatEnded();

        if (!this.gameState.CanTrack)
        {
            this.Clear();
            return;
        }

        if (this.TryGetTrackedCast(out var castAction, out var castState))
        {
            if (this.comboEngine.DeferExpiration(CastingExpirationHoldSeconds))
            {
                this.diagnostics.LogCastHold(castAction.ActionId, castAction.Name, castState.Elapsed, castState.Total);
            }

            return;
        }

        this.comboEngine.Tick();
    }

    private void ResetIfCombatEnded()
    {
        var isInCombat = this.gameState.IsInCombat;
        if (this.wasInCombat && !isInCombat)
        {
            this.Clear();
        }

        this.wasInCombat = isInCombat;
    }

    private bool TryGetTrackedCast(out ResolvedUptimeAction castAction, out StyleMeterCastState castState)
    {
        castState = this.gameState.CurrentCast;
        if (!castState.IsInProgressActionCast)
        {
            castAction = default;
            return false;
        }

        return this.actionResolver.TryResolveTrackedUptimeAction(
            castState.ActionId,
            0,
            out castAction,
            out _);
    }
}
