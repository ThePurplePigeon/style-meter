using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using StyleMeter.Actions;
using StyleMeter.Interop;
using StyleMeter.Tracking;
using StyleMeter.Windows;

namespace StyleMeter;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/stylemeter";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("StyleMeter");

    public Configuration Configuration { get; init; }
    internal StyleMeterTracker Tracker { get; init; }
    internal bool PreviewCombatOverlay { get; private set; }

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Log.Information(
            "Style Meter loading. Version={Version}, DebugLogging={DebugLogging}, ShowOverlay={ShowOverlay}",
            typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unknown",
            Configuration.DebugLogging,
            Configuration.ShowOverlay);

        try
        {
            var actionEffectSource = new ActionEffectHook(GameInteropProvider, Log);
            var frameworkUpdateSource = new DalamudFrameworkUpdateSource(Framework);
            var gameState = new DalamudStyleMeterGameState(ClientState, PlayerState, Condition);
            var actionSheet = new LuminaActionSheet(DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>());
            var candidateProvider = new ActionCandidateProvider(new ActionManagerAdjustedActionProvider());
            var actionResolver = new ActionResolver(candidateProvider, actionSheet);
            var recastProvider = new RecastProvider(new ActionManagerRecastSource());
            var diagnostics = new StyleMeterDiagnostics(Configuration, Log);

            Tracker = new StyleMeterTracker(
                () => Configuration.GraceThresholdSeconds,
                actionEffectSource,
                frameworkUpdateSource,
                gameState,
                actionResolver,
                recastProvider,
                diagnostics);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Style Meter failed to initialize tracker.");
            throw;
        }

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this)
        {
            IsOpen = Configuration.ShowOverlay,
        };

        this.windowSystem.AddWindow(ConfigWindow);
        this.windowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Style Meter. Use '/stylemeter config', '/stylemeter debug', or '/stylemeter diag'.",
        });

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("Style Meter loaded. Command={CommandName}", CommandName);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        this.windowSystem.RemoveAllWindows();

        Tracker.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    public void ToggleMainUi() => SetOverlayVisible(!Configuration.ShowOverlay);

    public void SetOverlayVisible(bool visible)
    {
        Configuration.ShowOverlay = visible;
        Configuration.Save();
        SyncMainWindowVisibility();
    }

    internal void SetPreviewCombatOverlay(bool visible)
    {
        PreviewCombatOverlay = visible;
        SyncMainWindowVisibility();
    }

    private void OnCommand(string command, string args)
    {
        var trimmedArgs = args.Trim();
        Log.Debug("Style Meter command received. Command={Command}, Args={Args}", command, trimmedArgs);

        if (trimmedArgs.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
            return;
        }

        if (trimmedArgs.Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.DebugLogging = !Configuration.DebugLogging;
            Configuration.Save();
            Log.Information("Style Meter debug logging is now {DebugLogging}", Configuration.DebugLogging);
            return;
        }

        if (trimmedArgs.Equals("diag", StringComparison.OrdinalIgnoreCase) ||
            trimmedArgs.Equals("diagnose", StringComparison.OrdinalIgnoreCase))
        {
            Tracker.LogDiagnostics("command");
            return;
        }

        ToggleMainUi();
    }

    private void SyncMainWindowVisibility()
    {
        MainWindow.IsOpen = Configuration.ShowOverlay || PreviewCombatOverlay;
    }
}
