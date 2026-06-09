using StyleMeter.Actions;

namespace StyleMeter.Tests;

public sealed class StyleMeterActionClassifierTests
{
    [Theory]
    [InlineData(true, false, 2, StyleMeterActionClassifier.GcdCooldownGroup, 0, true)]
    [InlineData(true, false, 3, 0, StyleMeterActionClassifier.GcdCooldownGroup, true)]
    [InlineData(true, false, 2, 4, 0, true)]
    [InlineData(true, false, 3, 0, 4, true)]
    [InlineData(true, false, 4, 4, 0, false)]
    [InlineData(true, false, 4, 0, 0, false)]
    [InlineData(true, false, 1, 0, 0, false)]
    [InlineData(true, false, 2, 0, 0, true)]
    [InlineData(true, false, 3, 0, 0, true)]
    [InlineData(true, false, 11, 0, 0, true)]
    [InlineData(false, false, 2, StyleMeterActionClassifier.GcdCooldownGroup, 0, true)]
    [InlineData(false, false, 2, 0, 0, true)]
    [InlineData(true, true, 2, StyleMeterActionClassifier.GcdCooldownGroup, 0, false)]
    [InlineData(true, false, 4, byte.MaxValue, byte.MaxValue, false)]
    public void IsTrackedGcdAction_filters_expected_action_shapes(
        bool isPlayerAction,
        bool isPvP,
        uint actionCategoryId,
        byte cooldownGroup,
        byte additionalCooldownGroup,
        bool expected)
    {
        var actual = StyleMeterActionClassifier.IsTrackedGcdAction(
            isPlayerAction,
            isPvP,
            actionCategoryId,
            cooldownGroup,
            additionalCooldownGroup);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsTrackedGcdAction_tracks_shared_gcd_combo_replacements_without_player_action_flag()
    {
        var action = CreateAction(
            1,
            "Generated Combo Hit",
            isPlayerAction: false,
            actionCategoryId: StyleMeterActionClassifier.AbilityActionCategoryId,
            cooldownGroup: StyleMeterActionClassifier.GcdCooldownGroup);

        Assert.True(StyleMeterActionClassifier.IsTrackedGcdAction(action));
    }

    [Fact]
    public void IsTrackedGcdAction_tracks_non_ability_non_pvp_uptime_categories_as_fallback()
    {
        var action = CreateAction(1, "Dance Step", actionCategoryId: 11);

        Assert.True(StyleMeterActionClassifier.IsTrackedGcdAction(action));
    }

    [Fact]
    public void IsTrackedGcdAction_rejects_auto_attacks_ogcd_abilities_and_pvp_actions()
    {
        Assert.False(StyleMeterActionClassifier.IsTrackedGcdAction(CreateAction(1, "Auto", actionCategoryId: 1)));
        Assert.False(StyleMeterActionClassifier.IsTrackedGcdAction(CreateAction(2, "oGCD", actionCategoryId: 4)));
        Assert.False(StyleMeterActionClassifier.IsTrackedGcdAction(CreateAction(3, "PvP", isPvP: true, cooldownGroup: 58)));
    }

    [Theory]
    [InlineData(false, 4, 0, 0, true)]
    [InlineData(false, 4, 10, 0, true)]
    [InlineData(false, 4, StyleMeterActionClassifier.GcdCooldownGroup, 0, false)]
    [InlineData(false, 4, 0, StyleMeterActionClassifier.GcdCooldownGroup, false)]
    [InlineData(true, 4, 0, 0, false)]
    [InlineData(false, 2, 0, 0, false)]
    [InlineData(false, 1, 0, 0, false)]
    public void IsTrackedOffGlobalCooldownAction_filters_expected_action_shapes(
        bool isPvP,
        uint actionCategoryId,
        byte cooldownGroup,
        byte additionalCooldownGroup,
        bool expected)
    {
        var actual = StyleMeterActionClassifier.IsTrackedOffGlobalCooldownAction(
            isPvP,
            actionCategoryId,
            cooldownGroup,
            additionalCooldownGroup);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsTrackedOffGlobalCooldownAction_tracks_pve_ability_rows_only()
    {
        Assert.True(StyleMeterActionClassifier.IsTrackedOffGlobalCooldownAction(CreateAction(10, "Weave", actionCategoryId: 4)));
        Assert.False(StyleMeterActionClassifier.IsTrackedOffGlobalCooldownAction(CreateAction(11, "Shared GCD Ability", actionCategoryId: 4, cooldownGroup: 58)));
        Assert.False(StyleMeterActionClassifier.IsTrackedOffGlobalCooldownAction(CreateAction(12, "Spell", actionCategoryId: 2)));
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
}
