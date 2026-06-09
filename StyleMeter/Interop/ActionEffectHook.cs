using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace StyleMeter.Interop;

internal interface IActionEffectSource : IDisposable
{
    event Action<ObservedActionEffect>? OnActionEffectReceived;

    void Enable();
}

internal unsafe sealed class ActionEffectHook : IActionEffectSource
{
    private readonly IPluginLog log;
    private readonly Hook<ActionEffectReceiveDelegate> actionEffectHook;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActionEffectReceiveDelegate(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds);

    public ActionEffectHook(IGameInteropProvider gameInteropProvider, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(gameInteropProvider);
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        var actionEffectReceiveAddress = (nint)ActionEffectHandler.MemberFunctionPointers.Receive;
        this.log.Information(
            "Style Meter initializing ActionEffectHandler.Receive hook at 0x{Address:X}. GCDCooldownGroup={GcdCooldownGroup}",
            actionEffectReceiveAddress,
            Actions.StyleMeterActionClassifier.GcdCooldownGroup);

        this.actionEffectHook = gameInteropProvider.HookFromAddress<ActionEffectReceiveDelegate>(
            actionEffectReceiveAddress,
            this.ActionEffectReceiveDetour);
    }

    public event Action<ObservedActionEffect>? OnActionEffectReceived;

    public void Enable()
    {
        this.actionEffectHook.Enable();
        this.log.Information("Style Meter ActionEffectHandler.Receive hook enabled.");
    }

    public void Dispose()
    {
        this.actionEffectHook.Dispose();
    }

    private void ActionEffectReceiveDetour(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        this.actionEffectHook.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);

        try
        {
            this.OnActionEffectReceived?.Invoke(CreateObservedActionEffect(casterEntityId, header));
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Failed to process action effect packet from caster 0x{CasterEntityId:X}.", casterEntityId);
        }
    }

    private static ObservedActionEffect CreateObservedActionEffect(uint casterEntityId, ActionEffectHandler.Header* header)
    {
        if (header is null)
        {
            return ObservedActionEffect.NullHeader(casterEntityId);
        }

        return new ObservedActionEffect(
            casterEntityId,
            StyleMeterActionKindMapper.FromDalamud((ActionType)header->ActionType),
            header->ActionId,
            header->SpellId,
            header->NumTargets,
            header->AnimationLock,
            true);
    }
}
