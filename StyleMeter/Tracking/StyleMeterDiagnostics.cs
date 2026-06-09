using System;
using Dalamud.Plugin.Services;
using StyleMeter.Actions;
using StyleMeter.Interop;

namespace StyleMeter.Tracking;

internal interface IStyleMeterDiagnostics
{
    void LogDiagnostics(string reason, StyleMeterGameStateSnapshot gameState, StyleMeterSnapshot snapshot);

    void LogObservedActionEffect(ObservedActionEffect actionEffect, uint localEntityId, bool canTrack, bool isSelf);

    void LogSkippedActionEffect(ObservedActionEffect actionEffect, string reason);

    void LogTrackedGcd(
        ObservedActionEffect actionEffect,
        ResolvedUptimeAction action,
        RecastInfo recast,
        StyleMeterSnapshot snapshot);

    void LogTrackedOffGlobalCooldown(
        ObservedActionEffect actionEffect,
        ResolvedUptimeAction action,
        StyleMeterSnapshot snapshot);

    void LogCastHold(uint actionId, string actionName, float elapsed, float total);
}

internal sealed class StyleMeterDiagnostics : IStyleMeterDiagnostics
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;

    private uint lastLoggedCastHoldActionId;
    private DateTime lastLoggedCastHoldUtc = DateTime.MinValue;

    public StyleMeterDiagnostics(Configuration configuration, IPluginLog log)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void LogDiagnostics(string reason, StyleMeterGameStateSnapshot gameState, StyleMeterSnapshot snapshot)
    {
        this.log.Information(
            "Style Meter diagnostics ({Reason}): IsLoggedIn={IsLoggedIn}, PlayerLoaded={PlayerLoaded}, PlayerEntityId=0x{PlayerEntityId:X}, IsPvP={IsPvP}, PvPDisplay={PvPDisplay}, Unconscious={Unconscious}, CanTrack={CanTrack}, IsInCombat={IsInCombat}, Combo={Combo}, Best={Best}, Rank={Rank}, Active={Active}, DebugLogging={DebugLogging}",
            reason,
            gameState.IsLoggedIn,
            gameState.PlayerLoaded,
            gameState.PlayerEntityId,
            gameState.IsPvP,
            gameState.PvPDisplayActive,
            gameState.Unconscious,
            gameState.CanTrack,
            gameState.IsInCombat,
            snapshot.ComboCount,
            snapshot.BestComboCount,
            snapshot.Rank,
            snapshot.IsActive,
            this.configuration.DebugLogging);
    }

    public void LogObservedActionEffect(ObservedActionEffect actionEffect, uint localEntityId, bool canTrack, bool isSelf)
    {
        if (!this.configuration.DebugLogging)
        {
            return;
        }

        this.log.Debug(
            "Style Meter observed action effect: caster=0x{CasterEntityId:X}, local=0x{LocalEntityId:X}, self={IsSelf}, canTrack={CanTrack}, actionKind={ActionKind}, action={ActionId}, spellId={SpellId}, targets={TargetCount}, animationLock={AnimationLock:F3}",
            actionEffect.CasterEntityId,
            localEntityId,
            isSelf,
            canTrack,
            actionEffect.ActionKind,
            actionEffect.ActionId,
            actionEffect.SpellId,
            actionEffect.TargetCount,
            actionEffect.AnimationLock);
    }

    public void LogSkippedActionEffect(ObservedActionEffect actionEffect, string reason)
    {
        if (!this.configuration.DebugLogging)
        {
            return;
        }

        this.log.Debug(
            "Style Meter skipped action effect: reason={Reason}, caster=0x{CasterEntityId:X}, actionKind={ActionKind}, action={ActionId}, spellId={SpellId}",
            reason,
            actionEffect.CasterEntityId,
            actionEffect.ActionKind,
            actionEffect.ActionId,
            actionEffect.SpellId);
    }

    public void LogTrackedGcd(
        ObservedActionEffect actionEffect,
        ResolvedUptimeAction action,
        RecastInfo recast,
        StyleMeterSnapshot snapshot)
    {
        if (!this.configuration.DebugLogging)
        {
            return;
        }

        this.log.Debug(
            "Style Meter GCD tracked: observedAction={ObservedActionId}, spellId={SpellId}, resolvedAction={ResolvedActionId} \"{ActionName}\", resolution={ResolutionSource}, combo={ComboCount}, best={BestComboCount}, recast={RecastSeconds:F2}, recastSource={RecastSource}, grace={GraceThresholdSeconds:F2}, rank={Rank}, category={ActionCategoryId}, isPlayerAction={IsPlayerAction}, cooldownGroup={CooldownGroup}, additionalCooldownGroup={AdditionalCooldownGroup}, recast100ms={Recast100ms}",
            actionEffect.ActionId,
            actionEffect.SpellId,
            action.ActionId,
            action.Name,
            action.ResolutionSource,
            snapshot.ComboCount,
            snapshot.BestComboCount,
            snapshot.CurrentRecastSeconds,
            recast.Source,
            snapshot.GraceThresholdSeconds,
            snapshot.Rank,
            action.ActionCategoryId,
            action.IsPlayerAction,
            action.CooldownGroup,
            action.AdditionalCooldownGroup,
            action.Recast100ms);
    }

    public void LogTrackedOffGlobalCooldown(
        ObservedActionEffect actionEffect,
        ResolvedUptimeAction action,
        StyleMeterSnapshot snapshot)
    {
        if (!this.configuration.DebugLogging)
        {
            return;
        }

        this.log.Debug(
            "Style Meter oGCD tracked: observedAction={ObservedActionId}, spellId={SpellId}, resolvedAction={ResolvedActionId} \"{ActionName}\", resolution={ResolutionSource}, combo={ComboCount}, weaves={OffGlobalCooldownCount}, chain={ChainCount}, category={ActionCategoryId}, cooldownGroup={CooldownGroup}, additionalCooldownGroup={AdditionalCooldownGroup}",
            actionEffect.ActionId,
            actionEffect.SpellId,
            action.ActionId,
            action.Name,
            action.ResolutionSource,
            snapshot.ComboCount,
            snapshot.OffGlobalCooldownCount,
            snapshot.ChainCount,
            action.ActionCategoryId,
            action.CooldownGroup,
            action.AdditionalCooldownGroup);
    }

    public void LogCastHold(uint actionId, string actionName, float elapsed, float total)
    {
        if (!this.configuration.DebugLogging)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (actionId == this.lastLoggedCastHoldActionId &&
            (now - this.lastLoggedCastHoldUtc).TotalSeconds < 1)
        {
            return;
        }

        this.lastLoggedCastHoldActionId = actionId;
        this.lastLoggedCastHoldUtc = now;

        this.log.Debug(
            "Style Meter holding combo during tracked cast: action={ActionId} \"{ActionName}\", elapsed={Elapsed:F2}, total={Total:F2}",
            actionId,
            actionName,
            elapsed,
            total);
    }
}
