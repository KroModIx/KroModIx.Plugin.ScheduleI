using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Shared-Launcher fuer das Nexus-Detail-Window — von allen drei
/// Tabs aufrufbar (Katalog, Downloads, Installiert). Erspart pro Tab die
/// gleichen 4 Zeilen Owner-Lookup + Window-Show.</summary>
internal static class ScheduleOneNexusDetailLauncher
{
    public static void Show(int modId, Bitmap? prefilledCover,
        INexusService nexus, CoverCache covers, IHostServices host)
    {
        var vm = new NexusModDetailViewModel(modId, prefilledCover, nexus, covers, host);
        var window = new NexusModDetailWindow { DataContext = vm };
        var owner = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) window.Show(owner); else window.Show();
    }

    public static void Show(NexusRow row, INexusService nexus, CoverCache covers, IHostServices host)
    {
        var vm = new NexusModDetailViewModel(row, nexus, covers, host);
        var window = new NexusModDetailWindow { DataContext = vm };
        var owner = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null) window.Show(owner); else window.Show();
    }
}
