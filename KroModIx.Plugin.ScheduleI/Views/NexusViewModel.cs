using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Nexus-Katalog-Tab. Voll-Katalog via GraphQL (kein API-Key
/// noetig fuer Read). Analog Cyberpunk — Pagination, Sort, Search,
/// Kategorie-Filter clientseitig.</summary>
public sealed partial class NexusViewModel : ObservableObject, IDisposable
{
    private readonly ScheduleOneNexusCatalog _catalog;
    private readonly CoverCache _covers;
    private readonly INexusService _nexus;
    private readonly ScheduleOneDownloader _downloader;
    private readonly DownloadEventBus _bus;
    private readonly IHostServices _host;
    private readonly EventHandler _apiKeyChangedHandler;
    private readonly System.Threading.SemaphoreSlim _loadGate = new(1, 1);

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private NexusSortOption? _selectedSort;
    [ObservableProperty] private bool _isPremium;
    [ObservableProperty] private string? _selectedCategory = "";
    [ObservableProperty] private string _coverProgressText = "";

    partial void OnSelectedSortChanged(NexusSortOption? value) { if (value is not null) _ = LoadFirstPageAsync(); }
    partial void OnSelectedCategoryChanged(string? value) => ApplyCategoryFilter();
    partial void OnIsPremiumChanged(bool value) { foreach (var r in Rows) r.IsPremium = value; }

    public ObservableCollection<NexusRow> Rows { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { "" };
    // v0.2.2: pro Nexus-ModId eine stabile Row-Instanz halten — STATIC,
    // damit selbst wenn der Host den NexusViewModel mehrfach instanziiert
    // (bei jedem Tab-Wechsel neue VM), alle VMs die SELBEN Row-Instanzen
    // teilen. Ohne static: 127 „Cover set"-Logs fuer 17 Mods aber 0
    // sichtbare Cover, weil jede VM ihren eigenen Row-Cache hatte und die
    // aktuelle DataContext-VM's Rows nie die Covers bekamen.
    private static readonly Dictionary<int, NexusRow> _rowsById = new();
    private static readonly object _rowsByIdLock = new();
    public IReadOnlyList<NexusSortOption> SortOptions { get; } = new[]
    {
        new NexusSortOption(Strings.T("sort.latest_update"), NexusSort.LatestUpdate),
        new NexusSortOption(Strings.T("sort.latest_add"), NexusSort.LatestAdd),
        new NexusSortOption(Strings.T("sort.most_endorsed"), NexusSort.MostEndorsed),
        new NexusSortOption(Strings.T("sort.most_downloaded"), NexusSort.MostDownloaded),
    };
    public bool HasMore => _catalog.HasMore;

    public NexusViewModel(ScheduleOneNexusCatalog catalog, CoverCache covers,
        INexusService nexus, ScheduleOneDownloader downloader, DownloadEventBus bus, IHostServices host)
    {
        _catalog = catalog; _covers = covers; _nexus = nexus;
        _downloader = downloader; _bus = bus; _host = host;
        _selectedSort = SortOptions[0];
        IsPremium = _nexus.IsPremium;
        _apiKeyChangedHandler = (_, _) => Dispatcher.UIThread.Post(() =>
        {
            IsPremium = _nexus.IsPremium;
            _ = LoadFirstPageAsync();
        });
        _nexus.ApiKeyChanged += _apiKeyChangedHandler;
        _ = InitialLoadAsync();
    }

    public void Dispose() => _nexus.ApiKeyChanged -= _apiKeyChangedHandler;

    private async Task InitialLoadAsync()
    {
        if (_catalog.Cached.Count == 0) await LoadFirstPageAsync();
        else RebuildRowsFromCatalog();
    }

    private void RebuildRowsFromCatalog()
    {
        RefreshCategoryOptions();
        Rows.Clear();
        var filter = SelectedCategory ?? "";
        foreach (var e in _catalog.Cached)
        {
            if (!string.IsNullOrEmpty(filter)
                && !string.Equals(e.Category, filter, StringComparison.OrdinalIgnoreCase))
                continue;
            Rows.Add(GetOrCreateRow(e));
        }
        UpdateStatus();
        OnPropertyChanged(nameof(HasMore));
    }

    /// <summary>v0.2.2: liefert die stabile Row-Instanz zu einem Katalog-
    /// Eintrag (per ModId gecached). Cover-Loads landen so IMMER auf der
    /// Row-Instanz die auch im ListBox sitzt — selbst nach Filter-Wechsel /
    /// Rebuild.</summary>
    private NexusRow GetOrCreateRow(NexusCatalogEntry e)
    {
        lock (_rowsByIdLock)
        {
            if (_rowsById.TryGetValue(e.ModId, out var existing))
            {
                existing.IsPremium = IsPremium;
                return existing;
            }
            var row = new NexusRow(e) { IsPremium = IsPremium };
            _rowsById[e.ModId] = row;
            return row;
        }
    }

    private void RefreshCategoryOptions()
    {
        var unique = _catalog.Cached
            .Select(e => e.Category?.Trim() ?? "")
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var desired = new List<string> { "" };
        desired.AddRange(unique);
        if (Categories.Count == desired.Count
            && Categories.SequenceEqual(desired, StringComparer.OrdinalIgnoreCase)) return;
        var preserve = SelectedCategory ?? "";
        Categories.Clear();
        foreach (var c in desired) Categories.Add(c);
        // v0.2.2: SelectedCategory NUR neu setzen wenn der Wert wirklich weg
        // waere — sonst triggert der Setter OnSelectedCategoryChanged →
        // ApplyCategoryFilter → Rows.Clear() mitten in einer laufenden
        // LoadCoversAsync-Loop. Ergebnis: 68 Cover-Sets fuer 17 Mods aber
        // 0 sichtbare Cover, weil die Row-Instanzen ausgetauscht werden
        // waehrend Cover-Property gesetzt wird.
        var newValue = Categories.Contains(preserve, StringComparer.OrdinalIgnoreCase) ? preserve : "";
        if (!string.Equals(newValue, SelectedCategory, StringComparison.Ordinal))
            SelectedCategory = newValue;
    }

    private void ApplyCategoryFilter()
    {
        Rows.Clear();
        var filter = SelectedCategory ?? "";
        foreach (var e in _catalog.Cached)
        {
            if (!string.IsNullOrEmpty(filter)
                && !string.Equals(e.Category, filter, StringComparison.OrdinalIgnoreCase))
                continue;
            Rows.Add(GetOrCreateRow(e));
        }
        UpdateStatus();
        _ = LoadCoversAsync(0);
    }

    private void UpdateStatus()
    {
        var loaded = _catalog.Cached.Count;
        var total = _catalog.TotalCount;
        StatusText = total > 0
            ? string.Format(Strings.T("status.mods_of"), loaded, total)
            : string.Format(Strings.T("status.mods_count_catalog"), loaded);
    }

    [RelayCommand]
    private async Task LoadFirstPageAsync()
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            IsBusy = true;
            StatusText = Strings.T("status.loading_catalog");
            await _catalog.LoadFirstPageAsync((SelectedSort ?? SortOptions[0]).Value, SearchQuery);
            RebuildRowsFromCatalog();
            _ = LoadCoversAsync(0);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Nexus-Load-First fehlgeschlagen");
            StatusText = Strings.T("status.error_prefix") + ex.Message;
        }
        finally { IsBusy = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_catalog.HasMore) return;
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            IsBusy = true;
            var before = _catalog.Cached.Count;
            await _catalog.LoadNextPageAsync();
            for (int i = before; i < _catalog.Cached.Count; i++)
                Rows.Add(GetOrCreateRow(_catalog.Cached[i]));
            UpdateStatus();
            OnPropertyChanged(nameof(HasMore));
            _ = LoadCoversAsync(before);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Nexus-Load-More fehlgeschlagen");
            StatusText = Strings.T("status.error_prefix") + ex.Message;
        }
        finally { IsBusy = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private Task SearchAsync() => LoadFirstPageAsync();

    private async Task LoadCoversAsync(int startIndex)
    {
        var snapshot = new List<NexusRow>();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            for (int i = startIndex; i < Rows.Count; i++) snapshot.Add(Rows[i]);
        });
        int pending = snapshot.Count(r => r.Cover is null && !string.IsNullOrEmpty(r.Source.PictureUrl));
        if (pending == 0) { await Dispatcher.UIThread.InvokeAsync(() => CoverProgressText = ""); return; }
        int done = 0;
        void UpdateProgress() => CoverProgressText = $"🖼 {done}/{pending}";
        await Dispatcher.UIThread.InvokeAsync(UpdateProgress);
        foreach (var row in snapshot)
        {
            if (string.IsNullOrEmpty(row.Source.PictureUrl)) continue;
            if (row.Cover is not null) continue;

            // v0.3.0: Bytes downloaden (CoverCache) und via Host-Baukasten
            // zu Avalonia-Bitmap decoden. Kein Bitmap-Ctor-Selbstbau mehr —
            // WebP/AVIF/DDS-Fallbacks + Thread-Affinity werden vom Host
            // erledigt.
            var bytes = await _covers.GetOrDownloadBytesAsync(row.Source.PictureUrl);
            if (bytes is null) { done++; await Dispatcher.UIThread.InvokeAsync(UpdateProgress); continue; }
            var bmp = await _host.Images.DecodeAsync(bytes);
            if (bmp is null)
            {
                _host.Logger.Debug("Cover-Decode fehlgeschlagen mod_id={Id}", row.Source.ModId);
                done++;
                await Dispatcher.UIThread.InvokeAsync(UpdateProgress);
                continue;
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                row.Cover = bmp;
                done++;
                UpdateProgress();
            });
            await Task.Delay(150);
        }
        await Dispatcher.UIThread.InvokeAsync(() => CoverProgressText = "");
    }

    [RelayCommand]
    private void OpenInBrowser(NexusRow? row)
    {
        if (row is null) return;
        _host.Shell.OpenExternalUrl(
            $"https://www.nexusmods.com/{ScheduleOneNexusCatalog.GameSlug}/mods/{row.Source.ModId}");
    }

    [RelayCommand]
    private void ShowDetail(NexusRow? row)
    {
        if (row is null) return;
        ScheduleOneNexusDetailLauncher.Show(row, _nexus, _covers, _host);
    }

    [RelayCommand]
    private async Task DownloadAsync(NexusRow? row)
    {
        if (row is null) return;
        if (!IsPremium)
        {
            _host.Notifications.Notify(Strings.T("notify.premium_required"), NotificationLevel.Warning);
            return;
        }
        using var scope = _host.BeginProgress($"Nexus: {row.Source.Name}");
        scope.Report(0, Strings.T("btn.download"));
        try
        {
            var progress = new Progress<double>(f =>
                scope.Report(f, $"{row.Source.Name} · {(int)(f * 100)}%"));
            var target = await _downloader.DownloadPrimaryAsync(row.Source.ModId, progress);
            if (target is null)
            {
                _host.Notifications.Notify(Strings.T("notify.download_fail"), NotificationLevel.Error);
                return;
            }
            _host.Notifications.Notify(
                Strings.T("notify.download_ok_prefix") + Path.GetFileName(target),
                NotificationLevel.Success);
            _bus.RaiseDownloadsChanged(Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "Nexus-Download fehlgeschlagen mod_id={Id}", row.Source.ModId);
            _host.Notifications.Notify("Download-Fehler: " + ex.Message, NotificationLevel.Error);
        }
    }
}

public sealed partial class NexusRow : ObservableObject
{
    public NexusRow(NexusCatalogEntry source) => Source = source;
    public NexusCatalogEntry Source { get; }
    [ObservableProperty] private bool _isPremium;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    [NotifyPropertyChangedFor(nameof(NoCover))]
    private Bitmap? _cover;
    public bool HasCover => Cover is not null;
    public bool NoCover => Cover is null;

    public string Name => Source.Name;
    public string Author => Source.Author;
    public string Summary => Source.Summary;
    public string VersionDisplay
    {
        get
        {
            var v = Source.Version?.Trim() ?? "";
            if (v.Length == 0) return "";
            return char.IsDigit(v[0]) ? "v" + v : v;
        }
    }
    public string EndorsementsText => Source.Endorsements > 0 ? $"👍 {Source.Endorsements}" : "";
    public string UpdatedText
    {
        get
        {
            var delta = DateTime.UtcNow - Source.UpdatedUtc;
            if (delta.TotalDays < 1) return "heute";
            if (delta.TotalDays < 2) return "gestern";
            if (delta.TotalDays < 30) return $"vor {(int)delta.TotalDays} Tagen";
            if (delta.TotalDays < 365) return $"vor {(int)(delta.TotalDays / 30)} Monaten";
            return Source.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }
}

public sealed record NexusSortOption(string Label, NexusSort Value);
