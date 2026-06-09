namespace StyleMeter.Actions;

internal static class StyleMeterActionClassifier
{
    public const byte GcdCooldownGroup = 58;
    public const uint AutoAttackActionCategoryId = 1;
    public const uint AbilityActionCategoryId = 4;

    public static bool IsTrackedGcdAction(ActionMetadata action)
    {
        return IsTrackedGcdAction(
            action.IsPlayerAction,
            action.IsPvP,
            action.ActionCategoryId,
            action.CooldownGroup,
            action.AdditionalCooldownGroup);
    }

    public static bool IsTrackedGcdAction(
        bool isPlayerAction,
        bool isPvP,
        uint actionCategoryId,
        byte cooldownGroup,
        byte additionalCooldownGroup)
    {
        if (isPvP)
        {
            return false;
        }

        var isSharedGcd = cooldownGroup == GcdCooldownGroup || additionalCooldownGroup == GcdCooldownGroup;
        if (isSharedGcd)
        {
            return true;
        }

        return actionCategoryId is not AutoAttackActionCategoryId and not AbilityActionCategoryId;
    }

    public static bool IsTrackedOffGlobalCooldownAction(ActionMetadata action)
    {
        return IsTrackedOffGlobalCooldownAction(
            action.IsPvP,
            action.ActionCategoryId,
            action.CooldownGroup,
            action.AdditionalCooldownGroup);
    }

    public static bool IsTrackedOffGlobalCooldownAction(
        bool isPvP,
        uint actionCategoryId,
        byte cooldownGroup,
        byte additionalCooldownGroup)
    {
        if (isPvP || actionCategoryId != AbilityActionCategoryId)
        {
            return false;
        }

        return cooldownGroup != GcdCooldownGroup && additionalCooldownGroup != GcdCooldownGroup;
    }
}
