using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;
using KroModIx.Plugin.ScheduleI.Views;

namespace KroModIx.Plugin.ScheduleI;

/// <summary>KroModIx-Plugin für Schedule I (TVGS, IL2CPP-basiert).
/// Drei Tabs (Installiert / Nexus / Downloads) + MelonLoader-Bootstrap-
/// Assistent (direkter Download vom offiziellen LavaGang/MelonLoader-
/// GitHub-Release). Nutzt Host-Contract <see cref="IHostServices.Nexus"/>
/// fuer den Katalog (Contracts v1.15+, oeffentliches GraphQL).
/// SharpCompress fuer ZIP/RAR/7z-Install.
///
/// <para>Portiert 1:1 vom DSP-Plugin-Muster (BepInEx → MelonLoader,
/// Mods/ statt BepInEx/plugins/, kein Ordner-Layout). Alle DSP-
/// Kernprinzipien greifen (Row-Konsistenz, Enricher, Detail-Launcher,
/// beide Nexus-Filename-Formate).</para></summary>
public sealed class ScheduleOnePlugin : IGameModPlugin, IUpdateNotifier
{
    public PluginMetadata Metadata { get; } = new(
        Id: "kroste.scheduleone",
        DisplayName: "Schedule I Mod-Manager",
        Version: "0.4.0",
        Author: "Kroste",
        Description: "Mod-Verwaltung für Schedule I (TVGS, IL2CPP). v0.4.0: " +
            "Detail-Dialog rendert Rich-HTML via _host.Descriptions.CreateRichView " +
            "(Host v1.21 HtmlRenderer-Baukasten) — Bold/Italic/Farben/Bilder/Listen " +
            "inline sichtbar statt Plain-Text-Wall. Plain-Text bleibt fuer KI-Prompts. " +
            "v0.3.0: BBCode-Parser wandert in Host-Baukasten `_host.Descriptions` " +
            "(Contracts v1.19) — Fix-Center statt Copy-Paste in 4 Plugins. " +
            "v0.2.0: Auto-Load-All-Katalog (Background-Fetch aller Seiten statt " +
            "nur 40), NexusDescriptionParser strippt BBCode sauber (v0.1 zeigte " +
            "rohen [center][url=..]-Muell im Detail-Dialog), Detail-Dialog " +
            "aufpoliert (920x760, Sektions-Cards, Cover 240x135). v0.1.0: " +
            "Drei Tabs (Installiert / Nexus-Katalog / Downloads), MelonLoader-Auto- " +
            "Install-Assistent (Direct-Download der offiziellen MelonLoader.x64.zip " +
            "vom LavaGang-GitHub-Release), Nexus-Voll-Katalog via GraphQL (Sort + " +
            "Search + Kategorie-Filter), Nexus-Detail-Dialog mit KI-Zusammenfassung, " +
            "SharpCompress-Auto-Layout-Install (Mods/ oder Flat), IUpdateNotifier " +
            "mit InstallManifest-Store, Row-Konsistenz in allen drei Tabs (Cover + " +
            "Details + Doppelklick), NexusFileNameParser matcht beide CDN-Formate " +
            "(Dash + Space). DE+EN. Async Refresh (kein UI-Freeze).");

    public IReadOnlyList<GameTarget> Targets { get; } = new[]
    {
        new GameTarget(
            GameId: "schedule-i",
            DisplayName: "Schedule I",
            SteamAppId: 3164500,
            AlternativeExecutableNames: new[] { "Schedule I.exe" },
            Platforms: Platforms.Both),
    };

    private IHostServices? _host;
    private ScheduleOnePathResolver? _paths;
    private MelonLoaderScanner? _scanner;
    private ScheduleOneInstallService? _installer;
    private ScheduleOnePaths? _pluginPaths;
    private ScheduleOneNexusCatalog? _catalog;
    private ScheduleOneDownloader? _downloader;
    private ScheduleOneZipInstaller? _zipInstaller;
    private ScheduleOneInstallManifestStore? _manifests;
    private ScheduleOneUpdateChecker? _updateChecker;
    private CoverCache? _covers;
    private DownloadEventBus? _bus;
    private MelonLoaderBootstrapper? _bootstrapper;
    private ScheduleOneNexusRowEnricher? _enricher;
    private IReadOnlyList<DetectedGame> _activatedGames = Array.Empty<DetectedGame>();

    public Task InitializeAsync(IHostServices host,
        IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        _host = host;
        Strings.Init(host.Localization);
        _paths = new ScheduleOnePathResolver();
        _scanner = new MelonLoaderScanner(_paths);
        _installer = new ScheduleOneInstallService();
        _pluginPaths = new ScheduleOnePaths(host);
        _catalog = new ScheduleOneNexusCatalog(host.Nexus);
        _downloader = new ScheduleOneDownloader(host.Nexus,
            host.CreateHttpClient("scheduleone-downloads"), _pluginPaths);
        _manifests = new ScheduleOneInstallManifestStore(host);
        _zipInstaller = new ScheduleOneZipInstaller(_manifests);
        _updateChecker = new ScheduleOneUpdateChecker(_manifests, _catalog);
        _covers = new CoverCache(host.CreateHttpClient("scheduleone-covers"), host);
        _bus = new DownloadEventBus();
        _bootstrapper = new MelonLoaderBootstrapper(host.CreateHttpClient("scheduleone-melonloader-bootstrap"));
        _enricher = new ScheduleOneNexusRowEnricher(host.Nexus, _covers, host);
        _activatedGames = activatedGames;

        // v0.4: Auto-Update-Check nach 15s Bootstrap-Delay (analog Cyberpunk).
        // Nach jedem ModInstalled-Event via DownloadEventBus erneut triggern
        // damit der Sidebar-Badge nach Install sinkt.
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch { return; }
            try { await _updateChecker.CheckAsync(ct); }
            catch (Exception ex) { host.Logger.Debug(ex, "Auto-Update-Check fehlgeschlagen"); }
            try { await host.RequestUpdateBadgeRefreshAsync(); } catch { }
        }, ct);
        _bus.ModInstalled += (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                try { await _updateChecker.CheckAsync(); } catch { }
                try { await host.RequestUpdateBadgeRefreshAsync(); } catch { }
            });
        };

        foreach (var game in activatedGames)
        {
            if (_paths.LooksLikeMelonLoaderInstall(game))
                host.Logger.Info("Schedule I initialisiert (MelonLoader erkannt): {Dir}", game.InstallDir);
            else
                host.Logger.Info("Schedule I initialisiert — MelonLoader fehlt (Bootstrap-Assistent im Installiert-Tab): {Dir}",
                    game.InstallDir);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (_host is null || _paths is null || _scanner is null || _installer is null
            || _pluginPaths is null || _catalog is null || _downloader is null
            || _zipInstaller is null || _covers is null || _bus is null || _bootstrapper is null
            || _manifests is null || _enricher is null)
            yield break;
        yield return new InstalledTab(game, _scanner, _installer, _paths, _bootstrapper, _bus,
            _manifests, _host.Nexus, _covers, _enricher, _host);
        yield return new NexusTab(_catalog, _covers, _host.Nexus, _downloader, _bus, _host);
        yield return new DownloadsTab(game, _pluginPaths, _zipInstaller, _bus,
            _host.Nexus, _covers, _enricher, _host);
    }

    public Task ShutdownAsync()
    {
        _host?.Logger.Info("Schedule I shutdown");
        return Task.CompletedTask;
    }

    // ---- IUpdateNotifier (v0.4) ----

    public Task<IReadOnlyList<GameUpdateInfo>> GetPendingUpdatesAsync(CancellationToken ct)
    {
        if (_updateChecker is null || _activatedGames.Count == 0)
            return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(Array.Empty<GameUpdateInfo>());
        var count = _updateChecker.PendingCount;
        if (count <= 0)
            return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(Array.Empty<GameUpdateInfo>());
        var summary = count == 1
            ? $"1 Mod-Update verfuegbar: {_updateChecker.Pending[0].InstalledName}"
            : $"{count} Mod-Updates verfuegbar";
        var infos = _activatedGames
            .Where(g => g.Target.SteamAppId is int)
            .Select(g => new GameUpdateInfo(g.Target.SteamAppId!.Value, count, summary))
            .ToList();
        return Task.FromResult<IReadOnlyList<GameUpdateInfo>>(infos);
    }

    private sealed class InstalledTab : IGameTabContribution
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

        public InstalledTab(DetectedGame game, MelonLoaderScanner scanner,
            ScheduleOneInstallService installer, ScheduleOnePathResolver paths,
            MelonLoaderBootstrapper bootstrapper, DownloadEventBus bus,
            ScheduleOneInstallManifestStore manifests, INexusService nexus,
            CoverCache covers, ScheduleOneNexusRowEnricher enricher, IHostServices host)
        { _game = game; _scanner = scanner; _installer = installer; _paths = paths;
          _bootstrapper = bootstrapper; _bus = bus; _manifests = manifests;
          _nexus = nexus; _covers = covers; _enricher = enricher; _host = host; }

        public string Id => "installed";
        public string Label => Strings.T("tab.installed");
        public string Icon => "\U0001F9E9";
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new InstalledModsView
            {
                DataContext = new InstalledModsViewModel(_game, _scanner, _installer, _paths,
                    _bootstrapper, _bus, _manifests, _nexus, _covers, _enricher, _host),
            };
    }

    private sealed class NexusTab : IGameTabContribution
    {
        private readonly ScheduleOneNexusCatalog _catalog;
        private readonly CoverCache _covers;
        private readonly INexusService _nexus;
        private readonly ScheduleOneDownloader _downloader;
        private readonly DownloadEventBus _bus;
        private readonly IHostServices _host;

        public NexusTab(ScheduleOneNexusCatalog catalog, CoverCache covers, INexusService nexus,
            ScheduleOneDownloader downloader, DownloadEventBus bus, IHostServices host)
        { _catalog = catalog; _covers = covers; _nexus = nexus; _downloader = downloader;
          _bus = bus; _host = host; }

        public string Id => "nexus";
        public string Label => Strings.T("tab.nexus");
        public string Icon => "\U0001F310"; // 🌐
        public int Order => 10;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new NexusView
            {
                DataContext = new NexusViewModel(_catalog, _covers, _nexus, _downloader, _bus, _host),
            };
    }

    private sealed class DownloadsTab : IGameTabContribution
    {
        private readonly DetectedGame _game;
        private readonly ScheduleOnePaths _paths;
        private readonly ScheduleOneZipInstaller _installer;
        private readonly DownloadEventBus _bus;
        private readonly INexusService _nexus;
        private readonly CoverCache _covers;
        private readonly ScheduleOneNexusRowEnricher _enricher;
        private readonly IHostServices _host;

        public DownloadsTab(DetectedGame game, ScheduleOnePaths paths, ScheduleOneZipInstaller installer,
            DownloadEventBus bus, INexusService nexus, CoverCache covers,
            ScheduleOneNexusRowEnricher enricher, IHostServices host)
        { _game = game; _paths = paths; _installer = installer; _bus = bus;
          _nexus = nexus; _covers = covers; _enricher = enricher; _host = host; }

        public string Id => "downloads";
        public string Label => Strings.T("tab.downloads");
        public string Icon => "\U0001F4E5"; // 📥
        public int Order => 20;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new DownloadsView
            {
                DataContext = new DownloadsViewModel(_game, _paths, _installer, _bus,
                    _nexus, _covers, _enricher, _host),
            };
    }
}
