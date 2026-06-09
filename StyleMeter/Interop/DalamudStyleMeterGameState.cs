using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace StyleMeter.Interop;

internal readonly record struct StyleMeterGameStateSnapshot(
    bool IsLoggedIn,
    bool PlayerLoaded,
    uint PlayerEntityId,
    bool IsPvP,
    bool PvPDisplayActive,
    bool Unconscious,
    bool CanTrack,
    bool IsInCombat = false);

internal readonly record struct StyleMeterCastState(
    StyleMeterActionKind ActionKind,
    uint ActionId,
    float Elapsed,
    float Total)
{
    public bool IsInProgressActionCast =>
        this.ActionKind == StyleMeterActionKind.Action &&
        this.ActionId != 0 &&
        this.Total > 0 &&
        this.Elapsed >= 0 &&
        this.Elapsed < this.Total;
}

internal interface IStyleMeterGameState : IDisposable
{
    event Action? OnResetRequested;

    uint LocalPlayerEntityId { get; }

    bool CanTrack { get; }

    bool IsInCombat { get; }

    StyleMeterCastState CurrentCast { get; }

    StyleMeterGameStateSnapshot Snapshot { get; }
}

internal unsafe sealed class DalamudStyleMeterGameState : IStyleMeterGameState
{
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly ICondition condition;

    public DalamudStyleMeterGameState(
        IClientState clientState,
        IPlayerState playerState,
        ICondition condition)
    {
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.playerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));

        this.clientState.TerritoryChanged += this.OnTerritoryChanged;
        this.clientState.ClassJobChanged += this.OnClassJobChanged;
        this.clientState.Logout += this.OnLogout;
        this.clientState.EnterPvP += this.OnEnterPvP;
    }

    public event Action? OnResetRequested;

    public uint LocalPlayerEntityId => this.playerState.EntityId;

    public bool CanTrack =>
        this.clientState.IsLoggedIn &&
        !this.clientState.IsPvP &&
        !this.condition[ConditionFlag.PvPDisplayActive] &&
        !this.condition[ConditionFlag.Unconscious] &&
        this.playerState.IsLoaded;

    public bool IsInCombat => this.condition[ConditionFlag.InCombat];

    public StyleMeterCastState CurrentCast
    {
        get
        {
            var actionManager = ActionManager.Instance();
            if (actionManager is null)
            {
                return default;
            }

            return new StyleMeterCastState(
                StyleMeterActionKindMapper.FromDalamud(actionManager->CastActionType),
                actionManager->CastActionId,
                actionManager->CastTimeElapsed,
                actionManager->CastTimeTotal);
        }
    }

    public StyleMeterGameStateSnapshot Snapshot => new(
        this.clientState.IsLoggedIn,
        this.playerState.IsLoaded,
        this.playerState.EntityId,
        this.clientState.IsPvP,
        this.condition[ConditionFlag.PvPDisplayActive],
        this.condition[ConditionFlag.Unconscious],
        this.CanTrack,
        this.IsInCombat);

    public void Dispose()
    {
        this.clientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.clientState.ClassJobChanged -= this.OnClassJobChanged;
        this.clientState.Logout -= this.OnLogout;
        this.clientState.EnterPvP -= this.OnEnterPvP;
    }

    private void OnTerritoryChanged(uint territoryId) => this.OnResetRequested?.Invoke();

    private void OnClassJobChanged(uint classJobId) => this.OnResetRequested?.Invoke();

    private void OnLogout(int type, int code) => this.OnResetRequested?.Invoke();

    private void OnEnterPvP() => this.OnResetRequested?.Invoke();
}
