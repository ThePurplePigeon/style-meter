using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using StyleMeter.Tracking;

namespace StyleMeter.Windows;

public class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Style Meter Configuration###StyleMeterConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(500, 420);
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

        var showOverlay = this.configuration.ShowOverlay;
        if (ImGui.Checkbox("Show overlay", ref showOverlay))
        {
            this.plugin.SetOverlayVisible(showOverlay);
        }

        var lockOverlay = this.configuration.LockOverlay;
        if (ImGui.Checkbox("Lock overlay position", ref lockOverlay))
        {
            this.configuration.LockOverlay = lockOverlay;
            this.configuration.Save();
        }

        var debugLogging = this.configuration.DebugLogging;
        if (ImGui.Checkbox("Debug logging", ref debugLogging))
        {
            this.configuration.DebugLogging = debugLogging;
            this.configuration.Save();
        }

        ImGui.Spacing();

        var overlayScale = this.configuration.OverlayScale;
        if (ImGui.SliderFloat("Overlay scale", ref overlayScale, 0.75f, 2.5f, "%.2f"))
        {
            this.configuration.OverlayScale = overlayScale;
            this.configuration.Save();
        }

        var graceThresholdSeconds = this.configuration.GraceThresholdSeconds;
        if (ImGui.SliderFloat("Grace threshold", ref graceThresholdSeconds, 0f, 2f, "%.2f s"))
        {
            this.configuration.GraceThresholdSeconds = graceThresholdSeconds;
            this.configuration.Save();
        }

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.82f, 0.88f, 1f, 0.9f), "Preview");
        ImGui.TextColored(new Vector4(0.62f, 0.68f, 0.78f, 0.82f), "Preview at the current overlay scale.");
        ImGui.Spacing();

        var nowUtc = DateTime.UtcNow;
        StyleMeterOverlayRenderer.Draw(CreatePreviewSnapshot(nowUtc), nowUtc, ImGui.GetTime(), overlayScale);

        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Reset meter"))
        {
            this.plugin.Tracker.Clear();
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.68f, 0.74f, 0.84f, 0.9f), "/stylemeter toggles the overlay.");
        ImGui.TextColored(new Vector4(0.68f, 0.74f, 0.84f, 0.9f), "/stylemeter config opens this window.");
    }

    private static StyleMeterSnapshot CreatePreviewSnapshot(DateTime nowUtc)
    {
        return new StyleMeterSnapshot(
            25,
            "S",
            true,
            false,
            2.5f,
            0.5f,
            nowUtc.AddSeconds(-1.2),
            nowUtc.AddSeconds(1.8),
            DateTime.MinValue,
            8,
            33,
            42);
    }
}
