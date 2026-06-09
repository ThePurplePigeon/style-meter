using FFXIVClientStructs.FFXIV.Client.Game;

namespace StyleMeter.Interop;

internal enum StyleMeterActionKind
{
    Unknown,
    Action,
    Other,
}

internal readonly record struct ObservedActionEffect(
    uint CasterEntityId,
    StyleMeterActionKind ActionKind,
    uint ActionId,
    ushort SpellId,
    byte TargetCount,
    float AnimationLock,
    bool HasHeader)
{
    public static ObservedActionEffect NullHeader(uint casterEntityId)
    {
        return new ObservedActionEffect(casterEntityId, StyleMeterActionKind.Unknown, 0, 0, 0, 0, false);
    }

    public bool IsSelf(uint localEntityId)
    {
        return this.CasterEntityId == localEntityId;
    }
}

internal static class StyleMeterActionKindMapper
{
    public static StyleMeterActionKind FromDalamud(ActionType actionType)
    {
        return actionType == ActionType.Action ? StyleMeterActionKind.Action : StyleMeterActionKind.Other;
    }
}
