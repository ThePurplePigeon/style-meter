using System;
using System.Collections.Generic;

namespace StyleMeter.Actions;

internal readonly record struct ResolvedUptimeAction(
    uint ObservedActionId,
    ushort SpellId,
    uint ActionId,
    string Name,
    bool IsPlayerAction,
    byte CooldownGroup,
    byte AdditionalCooldownGroup,
    uint ActionCategoryId,
    uint Recast100ms,
    string ResolutionSource);

internal interface IActionResolver
{
    bool TryResolveTrackedUptimeAction(
        uint observedActionId,
        ushort spellId,
        out ResolvedUptimeAction action,
        out string candidateDiagnostics);

    bool TryResolveTrackedOffGlobalCooldownAction(
        uint observedActionId,
        ushort spellId,
        out ResolvedUptimeAction action,
        out string candidateDiagnostics);
}

internal sealed class ActionResolver : IActionResolver
{
    private readonly ActionCandidateProvider candidateProvider;
    private readonly IActionSheet actionSheet;

    public ActionResolver(ActionCandidateProvider candidateProvider, IActionSheet actionSheet)
    {
        this.candidateProvider = candidateProvider ?? throw new ArgumentNullException(nameof(candidateProvider));
        this.actionSheet = actionSheet ?? throw new ArgumentNullException(nameof(actionSheet));
    }

    public bool TryResolveTrackedUptimeAction(
        uint observedActionId,
        ushort spellId,
        out ResolvedUptimeAction action,
        out string candidateDiagnostics)
    {
        return this.TryResolveAction(
            observedActionId,
            spellId,
            StyleMeterActionClassifier.IsTrackedGcdAction,
            out action,
            out candidateDiagnostics);
    }

    public bool TryResolveTrackedOffGlobalCooldownAction(
        uint observedActionId,
        ushort spellId,
        out ResolvedUptimeAction action,
        out string candidateDiagnostics)
    {
        return this.TryResolveAction(
            observedActionId,
            spellId,
            StyleMeterActionClassifier.IsTrackedOffGlobalCooldownAction,
            out action,
            out candidateDiagnostics);
    }

    private bool TryResolveAction(
        uint observedActionId,
        ushort spellId,
        Func<ActionMetadata, bool> classifier,
        out ResolvedUptimeAction action,
        out string candidateDiagnostics)
    {
        var rejectedCandidates = new List<string>();

        foreach (var candidate in this.candidateProvider.GetCandidates(observedActionId, spellId))
        {
            if (!this.actionSheet.TryGetAction(candidate.ActionId, out var metadata))
            {
                rejectedCandidates.Add($"{candidate.Source}:{candidate.ActionId}=missing-row");
                continue;
            }

            if (!classifier(metadata))
            {
                rejectedCandidates.Add(
                    $"{candidate.Source}:{candidate.ActionId} \"{metadata.Name}\" cat={metadata.ActionCategoryId} player={metadata.IsPlayerAction} pvp={metadata.IsPvP} cd={metadata.CooldownGroup}/{metadata.AdditionalCooldownGroup} recast={metadata.Recast100ms}");
                continue;
            }

            action = new ResolvedUptimeAction(
                observedActionId,
                spellId,
                metadata.ActionId,
                metadata.Name,
                metadata.IsPlayerAction,
                metadata.CooldownGroup,
                metadata.AdditionalCooldownGroup,
                metadata.ActionCategoryId,
                metadata.Recast100ms,
                candidate.Source);
            candidateDiagnostics = string.Empty;
            return true;
        }

        action = default;
        candidateDiagnostics = rejectedCandidates.Count == 0
            ? "none"
            : string.Join("; ", rejectedCandidates);
        return false;
    }
}
