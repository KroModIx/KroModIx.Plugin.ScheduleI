using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

/// <summary>Downloads-Tab: listet Archive im Plugin-Downloads-Ordner,
/// bietet Install + Delete + Bulk-Install. ZIP/RAR/7z via SharpCompress.
/// Auto-Layout-Detection entscheidet zwischen direktem Extract vs
/// Mods/&lt;Root&gt;/-Wrap.
///
/// <para>v0.6: Rows sind vollwertige Mod-Ansichten mit Cover/Author/
/// Summary aus dem Nexus-Katalog (via <see cref="NexusFileNameParser"/> +
/// <see cref="ScheduleOneNexusRowEnricher"/>). Doppelklick + Details-Button oeffnen
/// das gleiche <see cref="NexusModDetailWindow"/> wie der Katalog-Tab —
/// Kernprinzip 6/7 aus dem KroModIx-Plugin-Skill.</para></summary>
public sealed partial class DownloadsViewModel : ObservableObject, IDisposable
{
    private readonly DetectedGame _game;
    private readonly ScheduleOnePaths _paths;
    private readonly ScheduleOneZipInstaller _installer;
    private readonly DownloadEventBus _bus;
    private readonly IHostServices _host;
    private readonly INexusService _nexus;
    private readonly CoverCache _covers;
    private readonly ScheduleOneNexusRowEnricher _enricher;
    private readonly EventHandler<string?> _downloadHandler;
    private CancellationTokenSource _enrichCts = new();

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<DownloadRow> Rows { get; } = new();

    public DownloadsViewModel(DetectedGame game, ScheduleOnePaths paths,
        ScheduleOneZipInstaller installer, DownloadEventBus bus,
        INexusService nexus, CoverCache covers, ScheduleOneNexusRowEnricher enricher,
        IHostServices host)
    {
        _game = game; _paths = paths; _installer = installer; _bus = bus;
        _nexus = nexus; _covers = covers; _enricher = enricher; _host = host;
        _downloadHandler = (_, _) => Dispatcher.UIThread.Post(Refresh);
        _bus.DownloadsChanged += _downloadHandler;
        Refresh();
    }

    public void Dispose()
    {
        _bus.DownloadsChanged -= _downloadHandler;
        try { _enrichCts.Cancel(); } catch { }
    }

    /// <summary>Snapshot VOR jedem File-Write (Kernprinzip 6). Fehler
    /// duerfen den Install NIEMALS blockieren — der User will installieren,
    /// nicht den Backup-Service debuggen. Zurueckspielen laeuft ueber das
    /// Backups-Fenster (Sidebar-Kontextmenue), bewusst ohne Auto-Rollback.</summary>
    private async Task TrySnapshotAsync(string label)
    {
        try
        {
            var dirs = new List<string>();
            if (Directory.Exists(Path.Combine(_game.InstallDir, "Mods"))) dirs.Add(Path.Combine(_game.InstallDir, "Mods"));
            if (dirs.Count == 0) return;
            var gameKey = _game.Target.SteamAppId is int appId ? $"steam:{appId}" : _game.InstallDir;
            await _host.Backup.CreateSnapshotAsync(
                pluginId: "kroste.scheduleone", gameKey: gameKey,
                directories: dirs, label: label);
            await _host.Backup.PruneAsync("kroste.scheduleone", gameKey, keepLast: 10);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Snapshot fehlgeschlagen (Install laeuft trotzdem): {Label}", label);
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        try { _enrichCts.Cancel(); } catch { }
        _enrichCts = new CancellationTokenSource();
        Rows.Clear();
        if (!Directory.Exists(_paths.DownloadsDir))
        {
            StatusText = string.Format(Strings.T("status.downloads_dir_missing"), _paths.DownloadsDir);
            return;
        }
        var files = Directory.EnumerateFiles(_paths.DownloadsDir)
            .Where(f => ScheduleOneZipInstaller.SupportedExtensions.Any(ext =>
                f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .ToList();
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            var row = new DownloadRow(f, info.Name, info.Length, info.LastWriteTimeUtc)
            {
                NexusModId = NexusFileNameParser.TryExtractModId(info.Name),
                NexusName = NexusFileNameParser.TryExtractModName(info.Name) ?? "",
                NexusVersion = NexusFileNameParser.TryExtractVersion(info.Name) ?? "",
            };
            Rows.Add(row);
        }
        StatusText = files.Count == 0
            ? string.Format(Strings.T("status.no_zips_hint"), _paths.DownloadsDir)
            : string.Format(Strings.T("status.zips_ready"), files.Count);

        _ = _enricher.EnrichBatchAsync(Rows.ToList(), _enrichCts.Token);
    }

    [RelayCommand]
    private void OpenDownloadsFolder() => _host.Shell.OpenDirectory(_paths.DownloadsDir);

    [RelayCommand]
    private void ShowDetail(DownloadRow? row)
    {
        if (row?.NexusModId is not int modId) return;
        ScheduleOneNexusDetailLauncher.Show(modId, row.Cover, _nexus, _covers, _host);
    }

    [RelayCommand]
    private async Task InstallRowAsync(DownloadRow? row)
    {
        if (row is null) return;
        try
        {
            IsBusy = true;
            using var scope = _host.BeginProgress($"Install: {row.FileName}");
            scope.Report(0, "Extract …");
            await TrySnapshotAsync($"Vor Install von {row.FileName}");
            var result = await Task.Run(() => _installer.Install(row.FilePath, _game));
            scope.Report(1.0, "OK");
            _host.Notifications.Notify(
                (result.Success ? "✓ " : "✗ ") + result.Message,
                result.Success ? NotificationLevel.Success : NotificationLevel.Error);
            if (result.Success) _bus.RaiseModInstalled();
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Install-Row fehlgeschlagen: {File}", row.FileName);
            _host.Notifications.Notify("Fehler: " + ex.Message, NotificationLevel.Error);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InstallAllAsync()
    {
        if (Rows.Count == 0) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.install_all_title"),
            string.Format(Strings.T("dialog.install_all_msg"), Rows.Count),
            okLabel: Strings.T("dialog.install_all_ok"));
        if (!ok) return;
        int done = 0, failed = 0;
        // Bulk: EIN Snapshot vor der ganzen Schleife, nicht pro Row — beim
        // Rollback will der User zurueck auf den Stand VOR dem Batch.
        await TrySnapshotAsync($"Vor Bulk-Install ({Rows.Count} Archive)");
        using var scope = _host.BeginProgress(string.Format(Strings.T("progress.install_zips"), Rows.Count));
        var snapshot = Rows.ToList();
        foreach (var row in snapshot)
        {
            scope.Report((double)(done + failed) / snapshot.Count,
                $"{done + failed + 1}/{snapshot.Count}: {row.FileName}");
            try
            {
                var r = await Task.Run(() => _installer.Install(row.FilePath, _game));
                if (r.Success) done++; else failed++;
            }
            catch { failed++; }
        }
        _host.Notifications.Notify(
            string.Format(Strings.T("notify.bulk_install_result"), done, failed),
            failed == 0 ? NotificationLevel.Success : NotificationLevel.Warning);
        _bus.RaiseModInstalled();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DownloadRow? row)
    {
        if (row is null) return;
        var ok = await _host.Dialogs.ConfirmAsync(
            Strings.T("dialog.delete_zip_title"),
            string.Format(Strings.T("dialog.delete_zip_msg"), row.FileName),
            okLabel: Strings.T("dialog.delete_zip_ok"));
        if (!ok) return;
        try { File.Delete(row.FilePath); Refresh(); }
        catch (Exception ex) { _host.Notifications.Notify("Delete-Fehler: " + ex.Message, NotificationLevel.Error); }
    }
}

public sealed partial class DownloadRow : ObservableObject, IScheduleOneEnrichableRow
{
    public DownloadRow(string filePath, string fileName, long sizeBytes, DateTime downloadedUtc)
    {
        FilePath = filePath;
        FileName = fileName;
        SizeBytes = sizeBytes;
        DownloadedUtc = downloadedUtc;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public long SizeBytes { get; }
    public DateTime DownloadedUtc { get; }

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

    public string DisplayName => string.IsNullOrWhiteSpace(NexusName) ? FileName : NexusName;

    public string SizeText => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB",
    };

    public string SubtitleText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(NexusAuthor)) parts.Add(NexusAuthor);
            var v = NexusVersion?.Trim() ?? "";
            if (v.Length > 0) parts.Add(char.IsDigit(v[0]) ? "v" + v : v);
            parts.Add(SizeText);
            parts.Add(DownloadedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            return string.Join(" · ", parts);
        }
    }
}
