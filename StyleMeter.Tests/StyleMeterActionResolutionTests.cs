using StyleMeter.Actions;

namespace StyleMeter.Tests;

public sealed class StyleMeterActionResolutionTests
{
    [Fact]
    public void Candidate_provider_returns_unique_candidates_in_preferred_order()
    {
        var adjustedProvider = new FakeAdjustedActionProvider(new Dictionary<uint, uint>
        {
            [100] = 101,
            [200] = 201,
        });
        var candidateProvider = new ActionCandidateProvider(adjustedProvider);

        var candidates = candidateProvider.GetCandidates(100, 200).ToArray();

        Assert.Collection(
            candidates,
            candidate =>
            {
                Assert.Equal(100u, candidate.ActionId);
                Assert.Equal("header-action", candidate.Source);
            },
            candidate =>
            {
                Assert.Equal(200u, candidate.ActionId);
                Assert.Equal("header-spell", candidate.Source);
            },
            candidate =>
            {
                Assert.Equal(101u, candidate.ActionId);
                Assert.Equal("adjusted-header-action", candidate.Source);
            },
            candidate =>
            {
                Assert.Equal(201u, candidate.ActionId);
                Assert.Equal("adjusted-header-spell", candidate.Source);
            });
    }

    [Fact]
    public void Candidate_provider_removes_zeroes_and_duplicate_adjusted_ids()
    {
        var adjustedProvider = new FakeAdjustedActionProvider(new Dictionary<uint, uint>
        {
            [100] = 100,
        });
        var candidateProvider = new ActionCandidateProvider(adjustedProvider);

        var candidates = candidateProvider.GetCandidates(100, 100).ToArray();

        var candidate = Assert.Single(candidates);
        Assert.Equal(100u, candidate.ActionId);
        Assert.Equal("header-action", candidate.Source);
    }

    [Fact]
    public void Resolver_uses_adjusted_candidate_when_header_action_is_replaced()
    {
        var adjustedProvider = new FakeAdjustedActionProvider(new Dictionary<uint, uint>
        {
            [10_000] = 20_000,
        });
        var actionSheet = new FakeActionSheet
        {
            [20_000] = CreateAction(20_000, "Combo Replacement", cooldownGroup: StyleMeterActionClassifier.GcdCooldownGroup),
        };
        var resolver = new ActionResolver(new ActionCandidateProvider(adjustedProvider), actionSheet);

        var resolved = resolver.TryResolveTrackedUptimeAction(10_000, 0, out var action, out var diagnostics);

        Assert.True(resolved);
        Assert.Equal(20_000u, action.ActionId);
        Assert.Equal("adjusted-header-action", action.ResolutionSource);
        Assert.Equal(string.Empty, diagnostics);
    }

    [Fact]
    public void Resolver_can_resolve_from_spell_id_when_action_id_is_not_sheet_backed()
    {
        var actionSheet = new FakeActionSheet
        {
            [123] = CreateAction(123, "Spell Id GCD", actionCategoryId: 3),
        };
        var resolver = new ActionResolver(new ActionCandidateProvider(new FakeAdjustedActionProvider()), actionSheet);

        var resolved = resolver.TryResolveTrackedUptimeAction(0, 123, out var action, out _);

        Assert.True(resolved);
        Assert.Equal(123u, action.ActionId);
        Assert.Equal("header-spell", action.ResolutionSource);
    }

    [Fact]
    public void Resolver_can_resolve_off_global_cooldown_ability_candidates()
    {
        var adjustedProvider = new FakeAdjustedActionProvider(new Dictionary<uint, uint>
        {
            [10_000] = 20_000,
        });
        var actionSheet = new FakeActionSheet
        {
            [20_000] = CreateAction(
                20_000,
                "Adjusted Weave",
                actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId),
        };
        var resolver = new ActionResolver(new ActionCandidateProvider(adjustedProvider), actionSheet);

        var resolved = resolver.TryResolveTrackedOffGlobalCooldownAction(10_000, 0, out var action, out var diagnostics);

        Assert.True(resolved);
        Assert.Equal(20_000u, action.ActionId);
        Assert.Equal("adjusted-header-action", action.ResolutionSource);
        Assert.Equal(string.Empty, diagnostics);
    }

    [Fact]
    public void Resolver_does_not_treat_shared_gcd_abilities_as_off_global_cooldowns()
    {
        var actionSheet = new FakeActionSheet
        {
            [123] = CreateAction(
                123,
                "Shared GCD Ability",
                actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId,
                cooldownGroup: StyleMeterActionClassifier.GcdCooldownGroup),
        };
        var resolver = new ActionResolver(new ActionCandidateProvider(new FakeAdjustedActionProvider()), actionSheet);

        var resolved = resolver.TryResolveTrackedOffGlobalCooldownAction(123, 0, out _, out var diagnostics);

        Assert.False(resolved);
        Assert.Contains("Shared GCD Ability", diagnostics);
        Assert.Contains("cd=58/0", diagnostics);
    }

    [Fact]
    public void Resolver_reports_missing_and_untracked_candidates_for_diagnostics()
    {
        var adjustedProvider = new FakeAdjustedActionProvider(new Dictionary<uint, uint>
        {
            [100] = 101,
        });
        var actionSheet = new FakeActionSheet
        {
            [101] = CreateAction(
                101,
                "PvP Ability",
                isPvP: true,
                actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId),
        };
        var resolver = new ActionResolver(new ActionCandidateProvider(adjustedProvider), actionSheet);

        var resolved = resolver.TryResolveTrackedUptimeAction(100, 0, out _, out var diagnostics);

        Assert.False(resolved);
        Assert.Contains("header-action:100=missing-row", diagnostics);
        Assert.Contains("adjusted-header-action:101", diagnostics);
        Assert.Contains("pvp=True", diagnostics);
    }

    private static ActionMetadata CreateAction(
        uint actionId,
        string name,
        bool isPlayerAction = true,
        bool isPvP = false,
        uint actionCategoryId = 2,
        byte cooldownGroup = 0,
        byte additionalCooldownGroup = 0,
        uint recast100ms = 25)
    {
        return new ActionMetadata(
            actionId,
            name,
            isPlayerAction,
            isPvP,
            actionCategoryId,
            cooldownGroup,
            additionalCooldownGroup,
            recast100ms);
    }

    private sealed class FakeAdjustedActionProvider : IAdjustedActionProvider
    {
        private readonly Dictionary<uint, uint> adjustedActionIds;

        public FakeAdjustedActionProvider(Dictionary<uint, uint>? adjustedActionIds = null)
        {
            this.adjustedActionIds = adjustedActionIds ?? [];
        }

        public uint GetAdjustedActionId(uint actionId)
        {
            return this.adjustedActionIds.TryGetValue(actionId, out var adjustedActionId)
                ? adjustedActionId
                : actionId;
        }
    }

    private sealed class FakeActionSheet : Dictionary<uint, ActionMetadata>, IActionSheet
    {
        public bool TryGetAction(uint actionId, out ActionMetadata action)
        {
            return this.TryGetValue(actionId, out action);
        }
    }
}
