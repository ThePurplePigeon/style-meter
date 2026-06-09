using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace StyleMeter.Actions;

internal readonly record struct ActionCandidate(uint ActionId, string Source);

internal interface IAdjustedActionProvider
{
    uint GetAdjustedActionId(uint actionId);
}

internal sealed unsafe class ActionManagerAdjustedActionProvider : IAdjustedActionProvider
{
    public uint GetAdjustedActionId(uint actionId)
    {
        if (actionId == 0)
        {
            return 0;
        }

        var actionManager = ActionManager.Instance();
        return actionManager is null ? actionId : actionManager->GetAdjustedActionId(actionId);
    }
}

internal sealed class ActionCandidateProvider
{
    private readonly IAdjustedActionProvider adjustedActionProvider;

    public ActionCandidateProvider(IAdjustedActionProvider adjustedActionProvider)
    {
        this.adjustedActionProvider = adjustedActionProvider ?? throw new ArgumentNullException(nameof(adjustedActionProvider));
    }

    public IReadOnlyList<ActionCandidate> GetCandidates(uint actionId, ushort spellId)
    {
        var candidates = new List<ActionCandidate>(4);

        AddCandidate(candidates, actionId, "header-action");

        if (spellId != 0)
        {
            AddCandidate(candidates, spellId, "header-spell");
        }

        AddCandidate(candidates, this.adjustedActionProvider.GetAdjustedActionId(actionId), "adjusted-header-action");

        if (spellId != 0)
        {
            AddCandidate(candidates, this.adjustedActionProvider.GetAdjustedActionId(spellId), "adjusted-header-spell");
        }

        return candidates;
    }

    private static void AddCandidate(List<ActionCandidate> candidates, uint actionId, string source)
    {
        if (actionId == 0)
        {
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].ActionId == actionId)
            {
                return;
            }
        }

        candidates.Add(new ActionCandidate(actionId, source));
    }
}
