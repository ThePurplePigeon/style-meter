using System;
using Dalamud.Plugin.Services;

namespace StyleMeter.Interop;

internal interface IFrameworkUpdateSource : IDisposable
{
    event Action? OnUpdate;
}

internal sealed class DalamudFrameworkUpdateSource : IFrameworkUpdateSource
{
    private readonly IFramework framework;

    public DalamudFrameworkUpdateSource(IFramework framework)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.framework.Update += this.OnFrameworkUpdate;
    }

    public event Action? OnUpdate;

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        this.OnUpdate?.Invoke();
    }
}
