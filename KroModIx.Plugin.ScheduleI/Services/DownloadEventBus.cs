using System;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Plugin-interner Event-Bus fuer Cross-Tab-Refresh: Nexus- oder
/// Downloads-Tab feuert nach Download/Install ein Event, andere Tabs pollen
/// entsprechend. Kroste-Standard-Muster (siehe Skill).</summary>
public sealed class DownloadEventBus
{
    public event EventHandler<string?>? DownloadsChanged;
    public event EventHandler? ModInstalled;

    public void RaiseDownloadsChanged(string? filename = null)
        => DownloadsChanged?.Invoke(this, filename);
    public void RaiseModInstalled()
        => ModInstalled?.Invoke(this, EventArgs.Empty);
}
