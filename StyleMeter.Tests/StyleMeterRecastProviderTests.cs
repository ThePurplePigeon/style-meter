using StyleMeter.Actions;

namespace StyleMeter.Tests;

public sealed class StyleMeterRecastProviderTests
{
    [Fact]
    public void Selector_prefers_live_action_recast()
    {
        var recast = RecastFallbackSelector.Select(2.4f, 2.5f, 2_600, 30);

        Assert.Equal(2.4f, recast.Seconds);
        Assert.Equal("action", recast.Source);
    }

    [Fact]
    public void Selector_falls_back_to_shared_gcd_group()
    {
        var recast = RecastFallbackSelector.Select(0, 2.5f, 2_600, 30);

        Assert.Equal(2.5f, recast.Seconds);
        Assert.Equal("gcd-group", recast.Source);
    }

    [Fact]
    public void Selector_falls_back_to_adjusted_recast()
    {
        var recast = RecastFallbackSelector.Select(0, 0, 1_940, 30);

        Assert.Equal(1.94f, recast.Seconds, 3);
        Assert.Equal("adjusted", recast.Source);
    }

    [Fact]
    public void Selector_falls_back_to_lumina_recast()
    {
        var recast = RecastFallbackSelector.Select(0, 0, 0, 38);

        Assert.Equal(3.8f, recast.Seconds, 3);
        Assert.Equal("lumina-recast", recast.Source);
    }

    [Fact]
    public void Selector_uses_minimum_fallback_when_no_source_is_available()
    {
        var recast = RecastFallbackSelector.Select(0, 0, 0, 0);

        Assert.Equal(1f, recast.Seconds);
        Assert.Equal("minimum-fallback", recast.Source);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    public void Selector_ignores_invalid_live_recast_values(float invalidRecast)
    {
        var recast = RecastFallbackSelector.Select(invalidRecast, invalidRecast, 2_500, 30);

        Assert.Equal(2.5f, recast.Seconds);
        Assert.Equal("adjusted", recast.Source);
    }

    [Fact]
    public void Recast_provider_reads_source_values_in_fallback_order()
    {
        var recastSource = new FakeRecastSource
        {
            ActionRecastSeconds = 0,
            SharedGcdRecastSeconds = 0,
            AdjustedRecastMilliseconds = 0,
        };
        var provider = new RecastProvider(recastSource);

        var recast = provider.GetUptimeRecastSeconds(CreateResolvedAction(100, recast100ms: 25));

        Assert.Equal(2.5f, recast.Seconds);
        Assert.Equal("lumina-recast", recast.Source);
        Assert.Equal(100u, recastSource.LastActionId);
    }

    private static ResolvedUptimeAction CreateResolvedAction(uint actionId, uint recast100ms)
    {
        return new ResolvedUptimeAction(
            actionId,
            0,
            actionId,
            "Resolved",
            true,
            StyleMeterActionClassifier.GcdCooldownGroup,
            0,
            2,
            recast100ms,
            "test");
    }

    private sealed class FakeRecastSource : IRecastSource
    {
        public float ActionRecastSeconds { get; init; }

        public float SharedGcdRecastSeconds { get; init; }

        public int AdjustedRecastMilliseconds { get; init; }

        public uint LastActionId { get; private set; }

        public float GetActionRecastSeconds(uint actionId)
        {
            this.LastActionId = actionId;
            return this.ActionRecastSeconds;
        }

        public float GetSharedGcdRecastSeconds()
        {
            return this.SharedGcdRecastSeconds;
        }

        public int GetAdjustedRecastMilliseconds(uint actionId)
        {
            this.LastActionId = actionId;
            return this.AdjustedRecastMilliseconds;
        }
    }
}
