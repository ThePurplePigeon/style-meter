using System;
using Dalamud.Configuration;

namespace StyleMeter;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ShowOverlay { get; set; } = true;
    public bool LockOverlay { get; set; }
    public bool ClickThroughOverlay { get; set; }
    public bool AutoHideOutsideCombat { get; set; }
    public bool DebugLogging { get; set; }
    public float OverlayScale { get; set; } = 1.4f;
    public float OverlayOpacity { get; set; } = 1f;
    public float AnimationIntensity { get; set; } = 1f;
    public float GraceThresholdSeconds { get; set; } = 0.5f;
    public bool ShowBestBlock { get; set; } = true;
    public bool ShowChainDetails { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
