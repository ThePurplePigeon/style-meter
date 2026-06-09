using StyleMeter.Interop;

namespace StyleMeter.Tests;

public sealed class StyleMeterInteropModelTests
{
    [Fact]
    public void NullHeader_creates_safe_empty_observation()
    {
        var actionEffect = ObservedActionEffect.NullHeader(0xCAFE);

        Assert.Equal(0xCAFEu, actionEffect.CasterEntityId);
        Assert.Equal(StyleMeterActionKind.Unknown, actionEffect.ActionKind);
        Assert.Equal(0u, actionEffect.ActionId);
        Assert.Equal(0, actionEffect.SpellId);
        Assert.Equal(0, actionEffect.TargetCount);
        Assert.False(actionEffect.HasHeader);
    }

    [Fact]
    public void IsSelf_compares_caster_to_local_entity_id()
    {
        var actionEffect = new ObservedActionEffect(0xCAFE, StyleMeterActionKind.Action, 100, 0, 1, 0.6f, true);

        Assert.True(actionEffect.IsSelf(0xCAFE));
        Assert.False(actionEffect.IsSelf(0xBEEF));
    }

    [Theory]
    [InlineData(true, 100u, 0f, 2.5f, true)]
    [InlineData(true, 100u, 2.5f, 2.5f, false)]
    [InlineData(true, 0u, 0f, 2.5f, false)]
    [InlineData(false, 100u, 0f, 2.5f, false)]
    [InlineData(true, 100u, -0.001f, 2.5f, false)]
    [InlineData(true, 100u, 0f, 0f, false)]
    public void CastState_only_reports_active_action_casts(
        bool isActionKind,
        uint actionId,
        float elapsed,
        float total,
        bool expected)
    {
        var actionKind = isActionKind ? StyleMeterActionKind.Action : StyleMeterActionKind.Other;
        var castState = new StyleMeterCastState(actionKind, actionId, elapsed, total);

        Assert.Equal(expected, castState.IsInProgressActionCast);
    }
}
