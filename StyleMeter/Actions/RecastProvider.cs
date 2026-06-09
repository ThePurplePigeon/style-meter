using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace StyleMeter.Actions;

internal readonly record struct RecastInfo(float Seconds, string Source);

internal interface IRecastProvider
{
    RecastInfo GetUptimeRecastSeconds(ResolvedUptimeAction action);
}

internal interface IRecastSource
{
    float GetActionRecastSeconds(uint actionId);

    float GetSharedGcdRecastSeconds();

    int GetAdjustedRecastMilliseconds(uint actionId);
}

internal sealed unsafe class ActionManagerRecastSource : IRecastSource
{
    public float GetActionRecastSeconds(uint actionId)
    {
        var actionManager = ActionManager.Instance();
        return actionManager is null ? 0 : actionManager->GetRecastTime(ActionType.Action, actionId);
    }

    public float GetSharedGcdRecastSeconds()
    {
        var actionManager = ActionManager.Instance();
        if (actionManager is null)
        {
            return 0;
        }

        var gcdRecastDetail = actionManager->GetRecastGroupDetail(StyleMeterActionClassifier.GcdCooldownGroup - 1);
        return gcdRecastDetail is null ? 0 : gcdRecastDetail->Total;
    }

    public int GetAdjustedRecastMilliseconds(uint actionId)
    {
        return ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId);
    }
}

internal sealed class RecastProvider : IRecastProvider
{
    private readonly IRecastSource recastSource;

    public RecastProvider(IRecastSource recastSource)
    {
        this.recastSource = recastSource ?? throw new ArgumentNullException(nameof(recastSource));
    }

    public RecastInfo GetUptimeRecastSeconds(ResolvedUptimeAction action)
    {
        return RecastFallbackSelector.Select(
            this.recastSource.GetActionRecastSeconds(action.ActionId),
            this.recastSource.GetSharedGcdRecastSeconds(),
            this.recastSource.GetAdjustedRecastMilliseconds(action.ActionId),
            action.Recast100ms);
    }
}

internal static class RecastFallbackSelector
{
    public static RecastInfo Select(
        float actionRecastSeconds,
        float sharedGcdRecastSeconds,
        int adjustedRecastMilliseconds,
        uint luminaRecast100ms)
    {
        if (IsUsableSeconds(actionRecastSeconds))
        {
            return new RecastInfo(actionRecastSeconds, "action");
        }

        if (IsUsableSeconds(sharedGcdRecastSeconds))
        {
            return new RecastInfo(sharedGcdRecastSeconds, "gcd-group");
        }

        if (adjustedRecastMilliseconds > 0)
        {
            return new RecastInfo(adjustedRecastMilliseconds / 1000f, "adjusted");
        }

        if (luminaRecast100ms > 0)
        {
            return new RecastInfo(luminaRecast100ms / 10f, "lumina-recast");
        }

        return new RecastInfo(1f, "minimum-fallback");
    }

    private static bool IsUsableSeconds(float seconds)
    {
        return seconds > 0 &&
               !float.IsNaN(seconds) &&
               !float.IsInfinity(seconds);
    }
}
