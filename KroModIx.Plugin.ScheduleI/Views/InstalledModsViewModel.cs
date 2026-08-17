using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Installiert-Tab. Zeigt MelonLoader-Mods. Wenn MelonLoader nicht
/// installiert ist: Bootstrap-Assistent (LavaGang/MelonLoader-GitHub-Auto-Download).
/// <para>v0.2: Refresh laeuft off-thread (Kernprinzip 3), MelonLoader-Marker
/// wird lazy per Task.Run gecheckt. Rows-Rebuild danach auf UI-Thread.</para>
///
/// <para>v0.6: Rows tragen Cover/Author/Version/Summary aus dem
/// Nexus-Katalog — via <see cref="ScheduleOneInstallManifestStore"/> (persistierte
/// ModId zur Install-Zeit) + <see cref="ScheduleOneNexusRowEnricher"/>. Doppelklick
/// + Details-Button oeffnen das gleiche <see cref="NexusModDetailWindow"/>
/// wie der Katalog-Tab. Kernprinzip 6/7 aus dem KroModIx-Plugin-Skill.</para></summary>
public sealed partial class InstalledModsViewModel : ObservableObject, IDisposable
{
    private readonly DetectedGame _game;
    private readonly MelonLoaderScanner _scanner;
    private readonly ScheduleOneInstallService _installer;
    private readonly ScheduleOnePathResolver _paths;
    private readonly MelonLoaderBootstrapper _bootstrapper;
    private readonly DownloadEventBus _bus;
    private readonly ScheduleOneInstallManifestStore _manifests;
    private readonly INexusService _nexus;
    private readonly CoverCache _covers;
    private readonly ScheduleOneNexusRowEnricher _enricher;
    private readonly IHostServices _host;
    private readonly EventHandler _installedHandler;
    private CancellationTokenSource _enrichCts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsMelonLoaderBootstrap))]
    private string _statusText = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _filterText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsMelonLoaderBootstrap))]
    private bool _melonLoaderInstalled;

    public bool NeedsMelonLoaderBootstrap => !MelonLoaderInstalled;

    public ObservableCollection<ModRow> Rows { get; } = new();
    private List<ModRow> _allRows = new();

    public InstalledModsViewModel(DetectedGame game, MelonLoaderScanner scanner,
        ScheduleOneInstallService installer, ScheduleOnePathResolver paths,
        MelonLoaderBootstrapper bootstrapper, DownloadEventBus bus,
        ScheduleOneInstallManifestStore manifests, INexusService nexus,
        CoverCache covers, ScheduleOneNexusRowEnricher enricher, IHostServices host)
    {
        _game = game; _scanner = scanner; _installer = installer; _paths = paths;
        _bootstrapper = bootstrapper; _bus = bus;
        _manifests = manifests; _nexus = nexus; _covers = covers; _enricher = enricher;
        _host = host;
        _installedHandler = (_, _) => Dispatcher.UIThread.Post(() => _ = RefreshAsync());
        _bus.ModInstalled += _installedHandler;
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        _bus.ModInstalled -= _installedHandler;
        try { _enrichCts.Cancel(); } catch { }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = FilterText?.Trim() ?? "";
        Rows.Clear();
        var matched = string.IsNullOrEmpty(q)
            ? _allRows
            : _allRows.Where(r => r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var r in matched) Rows.Add(r);
    }

    /// <summary>Off-thread Refresh. MelonLoader-Marker-Check + Scan in Task.Run,
    /// nur Row-Rebuild + Status-Update sind UI-Thread. Kein Freeze beim
    /// initialen Plugin-Load — auch bei 100+ Plugins bleibt die App
    /// responsive (Kernprinzip 3).</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try { _enrichCts.Cancel(); } catch { }
        _enrichCts = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            var (melonLoaderOk, mods) = await Task.Run(() =>
            {
                var ok = _paths.LooksLikeMelonLoaderInstall(_game);
                var scan = ok
                    ? _scanner.ScanAll(_game)
                    : (IReadOnlyList<ScheduleOneMod>)Array.Empty<ScheduleOneMod>();
                return (ok, scan);
            });
            MelonLoaderInstalled = melonLoaderOk;
            if (!melonLoaderOk)
            {
                StatusText = Strings.T("status.no_melonloader");
                _allRows = new();
                Rows.Clear();
                return;
            }
            _allRows = mods.Select(BuildRow).ToList();
            var enabled = mods.Count(m => m.IsEnabled);
            var disabled = mods.Count - enabled;
            StatusText = mods.Count == 0
                ? Strings.T("status.no_mods")
                : string.Format(Strings.T("status.mods_count"), mods.Count, enabled, disabled);
            ApplyFilter();

            _ = _enricher.EnrichBatchAsync(_allRows.ToList(), _enrichCts.Token);
        }
        finally { IsBusy = false; }
    }

    private ModRow BuildRow(ScheduleOneMod mod)
    {
        var row = new ModRow(mod);
        // ModId aus persistiertem InstallManifest ziehen — beim Install
        // hat ScheduleOneZipInstaller den Nexus-Kontext dort abgelegt (v0.4).
        var key = ScheduleOneInstallManifestStore.BuildKey(mod.Name);
        var manifest = _manifests.TryGet(key);
        if (manifest is not null)
        {
            var modId = manifest.NexusModId;
            var version = manifest.NexusVersion;
            // v0.6.1: Stale-Manifest-Repair. Pre-v0.6.1 InstallManifests haben
            // NexusModId=null persistiert, weil der alte Parser das Dash-Format
            // nicht matchte. Wenn OriginalFilename da ist, jetzt mit dem
            // erweiterten Parser nachfassen — und das Manifest gleich fixen.
            if (modId is null && !string.IsNullOrWhiteSpace(manifest.OriginalFilename))
            {
                modId = NexusFileNameParser.TryExtractModId(manifest.OriginalFilename);
                version ??= NexusFileNameParser.TryExtractVersion(manifest.OriginalFilename);
                if (modId is not null)
                {
                    _manifests.Save(key, new ScheduleOneInstallManifest(
                        NexusModId: modId,
                        OriginalFilename: manifest.OriginalFilename,
                        NexusVersion: version,
                        InstalledAtUtc: manifest.InstalledAtUtc));
                }
            }
            row.NexusModId = modId;
            row.NexusVersion = version ?? "";
        }
        return row;
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        var dir = _paths.GetModsDir(_game);
        _host.Shell.OpenDirectory(dir);
    }

    [RelayCommand]
    private void ShowDetail(ModRow? row)
    {
        if (row?.NexusModId is not int modId) return;
        ScheduleOneNexusDetailLauncher.Show(modId, row.Cover, _nexus, _covers, _host);
    }

    [RelayCommand]
    private async Task InstallMelonLoaderAsync()
    {
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.melonloader_install_title"),
            string.Format(Strings.T("dialog.melonloader_install_msg"), _game.InstallDir),
            okLabel: Strings.T("dialog.melonloader_install_ok"));
        if (!ok) return;

        using var scope = _host.BeginProgress(Strings.T("progress.melonloader_install"));
        try
        {
            IsBusy = true;
            _host.Notifications.Notify(Strings.T("notify.melonloader_installing"), NotificationLevel.Info);
            var progress = new Progress<double>(f =>
                scope.Report(f, $"MelonLoader · {(int)(f * 100)}%"));
            var result = await _bootstrapper.InstallAsync(_game.InstallDir, progress);
            if (result.Success)
            {
                _host.Notifications.Notify(
                    string.Format(Strings.T("notify.melonloader_ok"), result.Version),
                    NotificationLevel.Success);
                await RefreshAsync();
            }
            else
            {
                _host.Notifications.Notify(
                    string.Format(Strings.T("notify.melonloader_fail"), result.ErrorMessage),
                    NotificationLevel.Error);
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(ModRow? row)
    {
        if (row is null) return;
        try
        {
            IsBusy = true;
            var newPath = _installer.SetEnabled(row.Mod, !row.Mod.IsEnabled);
            row.Mod = row.Mod with { IsEnabled = !row.Mod.IsEnabled, Path = newPath };
            row.OnModChanged();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Toggle fehlgeschlagen: {Name}", row.Mod.Name);
            await _host.Dialogs.ShowMessageAsync("Fehler", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UninstallAsync(ModRow? row)
    {
        if (row is null) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.uninstall_title"),
            string.Format(Strings.T("dialog.uninstall_msg"), row.Mod.Name, row.Mod.Path),
            okLabel: Strings.T("dialog.uninstall_ok"));
        if (!ok) return;
        try
        {
            IsBusy = true;
            _installer.Uninstall(row.Mod);
            _host.Notifications.Notify(Strings.T("notify.uninstalled_prefix") + row.Mod.Name,
                NotificationLevel.Success);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Uninstall fehlgeschlagen: {Name}", row.Mod.Name);
            await _host.Dialogs.ShowMessageAsync("Fehler", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DisableAllAsync()
    {
        var targets = Rows.Where(r => r.Mod.IsEnabled).ToList();
        if (targets.Count == 0)
        {
            _host.Notifications.Notify(Strings.T("notify.no_enabled_mods"), NotificationLevel.Info);
            return;
        }
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.disable_all_title"),
            string.Format(Strings.T("dialog.disable_all_msg"), targets.Count),
            okLabel: Strings.T("dialog.disable_all_ok"));
        if (!ok) return;
        int done = 0, failed = 0;
        using var scope = _host.BeginProgress(string.Format(Strings.T("progress.disable_bulk"), targets.Count));
        foreach (var row in targets)
        {
            scope.Report((double)(done + failed) / targets.Count,
                $"{done + failed + 1}/{targets.Count}: {row.Mod.Name}");
            try { _installer.SetEnabled(row.Mod, false); done++; }
            catch (Exception ex) { _host.Logger.Warn(ex, "Bulk-Disable {Name}", row.Mod.Name); failed++; }
        }
        _host.Notifications.Notify(string.Format(Strings.T("notify.bulk_disable_result"), done, failed),
            failed == 0 ? NotificationLevel.Success : NotificationLevel.Warning);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task EnableAllAsync()
    {
        var targets = Rows.Where(r => !r.Mod.IsEnabled).ToList();
        if (targets.Count == 0)
        {
            _host.Notifications.Notify(Strings.T("notify.no_disabled_mods"), NotificationLevel.Info);
            return;
        }
        int done = 0, failed = 0;
        using var scope = _host.BeginProgress(string.Format(Strings.T("progress.enable_bulk"), targets.Count));
        foreach (var row in targets)
        {
            scope.Report((double)(done + failed) / targets.Count,
                $"{done + failed + 1}/{targets.Count}: {row.Mod.Name}");
            try { _installer.SetEnabled(row.Mod, true); done++; }
            catch (Exception ex) { _host.Logger.Warn(ex, "Bulk-Enable {Name}", row.Mod.Name); failed++; }
        }
        _host.Notifications.Notify(string.Format(Strings.T("notify.bulk_enable_result"), done, failed),
            failed == 0 ? NotificationLevel.Success : NotificationLevel.Warning);
        await RefreshAsync();
    }
}

public sealed partial class ModRow : ObservableObject, IScheduleOneEnrichableRow
{
    public ModRow(ScheduleOneMod mod) => Mod = mod;
    [ObservableProperty] private ScheduleOneMod _mod;

    // ---- IScheduleOneEnrichableRow ----
    public int? NexusModId { get; set; }
    public bool IsEnriched { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    [NotifyPropertyChangedFor(nameof(NoCover))]
    private Bitmap? _cover;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _nexusName = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string _nexusAuthor = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string _nexusVersion = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _nexusSummary = "";
    [ObservableProperty] private bool _hasNexusMatch;

    public bool HasCover => Cover is not null;
    public bool NoCover => Cover is null;
    public bool HasSummary => !string.IsNullOrWhiteSpace(NexusSummary);

    public string DisplayName => string.IsNullOrWhiteSpace(NexusName) ? Mod.Name : NexusName;

    public string StatusLabel => Mod.IsEnabled ? Strings.T("row.status_active") : Strings.T("row.status_inactive");
    public string ToggleButtonLabel => Mod.IsEnabled ? Strings.T("btn.disable") : Strings.T("btn.enable");
    public string TypeIcon => Mod.IsDirectory ? "📁" : "🧩";
    public string SizeText => Mod.SizeBytes switch
    {
        < 1024 => $"{Mod.SizeBytes} B",
        < 1024 * 1024 => $"{Mod.SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{Mod.SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{Mod.SizeBytes / (1024.0 * 1024 * 1024):F2} GB",
    };
    public string SubtitleText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(NexusAuthor)) parts.Add(NexusAuthor);
            var v = NexusVersion?.Trim() ?? "";
            if (v.Length > 0) parts.Add(char.IsDigit(v[0]) ? "v" + v : v);
            parts.Add(Mod.IsDirectory ? "Ordner" : "DLL");
            parts.Add(SizeText);
            parts.Add(Mod.InstalledUtc.ToLocalTime().ToString("yyyy-MM-dd"));
            return string.Join(" · ", parts);
        }
    }

    public void OnModChanged()
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ToggleButtonLabel));
        OnPropertyChanged(nameof(SubtitleText));
    }
}
