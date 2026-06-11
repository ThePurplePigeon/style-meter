using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StyleMeter.Tracking;

namespace StyleMeter.Windows;

public class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private bool previewCombatOverlay;

    public ConfigWindow(Plugin plugin) : base("Style Meter Configuration###StyleMeterConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(540, 560);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;
        this.configuration = plugin.Configuration;
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(1f, 0.82f, 0.18f, 1f), "STYLE METER");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.78f, 0.9f, 0.85f), "PvE GCD streak HUD");
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawVisibilitySection();
        this.DrawLayoutSection();
        this.DrawVisualsSection();
        this.DrawTrackingSection();
        this.DrawDiagnosticsSection();
        DrawCommandHelp();
    }

    public override void OnClose()
    {
        this.previewCombatOverlay = false;
        this.plugin.SetPreviewCombatOverlay(false);
    }

    private void DrawVisibilitySection()
    {
        DrawSectionHeader("Visibility");

        var showOverlay = this.configuration.ShowOverlay;
        if (ImGui.Checkbox("Show overlay", ref showOverlay))
        {
            this.plugin.SetOverlayVisible(showOverlay);
        }

        var autoHideOutsideCombat = this.configuration.AutoHideOutsideCombat;
        if (ImGui.Checkbox("Auto-hide outside combat", ref autoHideOutsideCombat))
        {
            this.configuration.AutoHideOutsideCombat = autoHideOutsideCombat;
            this.configuration.Save();
        }

        DrawHelpText("When enabled, the HUD is hidden until the player is in the game's combat state.");

        if (ImGui.Checkbox("Preview combat overlay", ref this.previewCombatOverlay))
        {
            this.plugin.SetPreviewCombatOverlay(this.previewCombatOverlay);
        }

        DrawHelpText("Temporarily shows the full combat HUD for positioning. This does not affect tracking.");
    }

    private void DrawLayoutSection()
    {
        DrawSectionHeader("Layout");

        var lockOverlay = this.configuration.LockOverlay;
        if (ImGui.Checkbox("Lock overlay position", ref lockOverlay))
        {
            this.configuration.LockOverlay = lockOverlay;
            this.configuration.Save();
        }

        var clickThroughOverlay = this.configuration.ClickThroughOverlay;
        if (ImGui.Checkbox("Click through overlay", ref clickThroughOverlay))
        {
            this.configuration.ClickThroughOverlay = clickThroughOverlay;
            this.configuration.Save();
        }

        DrawHelpText("Click-through also locks movement. Use /stylemeter config if you need to turn it off.");

        var overlayScale = StyleMeterOverlayMath.NormalizeOverlayScale(this.configuration.OverlayScale);
        if (ImGui.SliderFloat("Overlay scale", ref overlayScale, 0.75f, 2.5f, "%.2f"))
        {
            this.configuration.OverlayScale = StyleMeterOverlayMath.NormalizeOverlayScale(overlayScale);
            this.configuration.Save();
        }
    }

    private void DrawVisualsSection()
    {
        DrawSectionHeader("Visuals");

        var overlayOpacity = StyleMeterOverlayMath.NormalizeOverlayOpacity(this.configuration.OverlayOpacity);
        if (ImGui.SliderFloat("Overlay opacity", ref overlayOpacity, 0.2f, 1f, "%.2f"))
        {
            this.configuration.OverlayOpacity = StyleMeterOverlayMath.NormalizeOverlayOpacity(overlayOpacity);
            this.configuration.Save();
        }

        var animationIntensity = StyleMeterOverlayMath.NormalizeAnimationIntensity(this.configuration.AnimationIntensity);
        if (ImGui.SliderFloat("Animation intensity", ref animationIntensity, 0f, 1.5f, "%.2f"))
        {
            this.configuration.AnimationIntensity = StyleMeterOverlayMath.NormalizeAnimationIntensity(animationIntensity);
            this.configuration.Save();
        }

        var showBestBlock = this.configuration.ShowBestBlock;
        if (ImGui.Checkbox("Show best combo block", ref showBestBlock))
        {
            this.configuration.ShowBestBlock = showBestBlock;
            this.configuration.Save();
        }

        var showChainDetails = this.configuration.ShowChainDetails;
        if (ImGui.Checkbox("Show weave detail text", ref showChainDetails))
        {
            this.configuration.ShowChainDetails = showChainDetails;
            this.configuration.Save();
        }

        if (ImGui.Button("Reset visual settings"))
        {
            this.configuration.OverlayScale = 1.4f;
            this.configuration.OverlayOpacity = 1f;
            this.configuration.AnimationIntensity = 1f;
            this.configuration.ShowBestBlock = true;
            this.configuration.ShowChainDetails = true;
            this.configuration.Save();
        }
    }

    private void DrawTrackingSection()
    {
        DrawSectionHeader("Tracking");

        var graceThresholdSeconds = StyleMeterComboEngine.NormalizeGraceThresholdSeconds(this.configuration.GraceThresholdSeconds);
        if (ImGui.SliderFloat("Grace threshold", ref graceThresholdSeconds, 0f, 2f, "%.2f s"))
        {
            this.configuration.GraceThresholdSeconds = graceThresholdSeconds;
            this.configuration.Save();
        }

        DrawHelpText("Extra time after the current GCD recast before the chain ends.");

        if (ImGui.Button("Reset meter"))
        {
            this.plugin.Tracker.Clear();
        }
    }

    private void DrawDiagnosticsSection()
    {
        DrawSectionHeader("Diagnostics");

        var debugLogging = this.configuration.DebugLogging;
        if (ImGui.Checkbox("Debug logging", ref debugLogging))
        {
            this.configuration.DebugLogging = debugLogging;
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Write diagnostics"))
        {
            this.plugin.Tracker.LogDiagnostics("config");
        }
    }

    private static void DrawCommandHelp()
    {
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.68f, 0.74f, 0.84f, 0.9f), "/stylemeter toggles the overlay.");
        ImGui.TextColored(new Vector4(0.68f, 0.74f, 0.84f, 0.9f), "/stylemeter config opens this window.");
    }

    private static void DrawSectionHeader(string text)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.82f, 0.88f, 1f, 0.92f), text);
        ImGui.Spacing();
    }

    private static void DrawHelpText(string text)
    {
        ImGui.PushTextWrapPos();
        ImGui.TextColored(new Vector4(0.62f, 0.68f, 0.78f, 0.86f), text);
        ImGui.PopTextWrapPos();
    }
}
