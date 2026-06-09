using System;
using Lumina.Excel;

using LuminaAction = Lumina.Excel.Sheets.Action;

namespace StyleMeter.Actions;

internal readonly record struct ActionMetadata(
    uint ActionId,
    string Name,
    bool IsPlayerAction,
    bool IsPvP,
    uint ActionCategoryId,
    byte CooldownGroup,
    byte AdditionalCooldownGroup,
    uint Recast100ms);

internal interface IActionSheet
{
    bool TryGetAction(uint actionId, out ActionMetadata action);
}

internal sealed class LuminaActionSheet : IActionSheet
{
    private readonly ExcelSheet<LuminaAction> actionSheet;

    public LuminaActionSheet(ExcelSheet<LuminaAction> actionSheet)
    {
        this.actionSheet = actionSheet ?? throw new ArgumentNullException(nameof(actionSheet));
    }

    public bool TryGetAction(uint actionId, out ActionMetadata action)
    {
        if (!this.actionSheet.TryGetRow(actionId, out var luminaAction))
        {
            action = default;
            return false;
        }

        action = new ActionMetadata(
            actionId,
            luminaAction.Name.ToString(),
            luminaAction.IsPlayerAction,
            luminaAction.IsPvP,
            luminaAction.ActionCategory.RowId,
            luminaAction.CooldownGroup,
            luminaAction.AdditionalCooldownGroup,
            luminaAction.Recast100ms);
        return true;
    }
}
