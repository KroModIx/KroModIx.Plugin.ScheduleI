using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Reichert eine Row (Downloads oder Installiert) mit Nexus-
/// Metadaten an: Autor, Version, Summary, Cover. Nutzt entweder
/// <see cref="ScheduleOneInstallManifestStore"/> (Installed-Rows, wo die ModId
/// zur Install-Zeit persistiert wurde) oder <see cref="NexusFileNameParser"/>
/// (Downloads-Rows, wo die ModId aus dem Nexus-CDN-Filename kommt).
///
/// <para>Kernprinzip 6/8 aus dem KroModIx-Plugin-Skill: Rows in ALLEN
/// drei Tabs (Katalog, Downloads, Installiert) muessen visuell konsistent
/// sein — Cover + Autor + Version + Summary + Detail-Dialog.</para>
///
/// <para>Throttling: max 4 parallele API-Roundtrips (Nexus 250/h Free),
/// zwischen Batches 100 ms Delay. Ergebnis wird via
/// <see cref="Dispatcher.UIThread.InvokeAsync"/> zurueck in die Row
/// geschrieben (Cover/Author-Property).</para></summary>
public sealed class ScheduleOneNexusRowEnricher
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly INexusService _nexus;
    private readonly CoverCache _covers;
    private readonly IHostServices _host;
    private readonly SemaphoreSlim _apiGate = new(4);
    private readonly Dictionary<int, NexusModDetail> _detailCache = new();
    private readonly object _cacheLock = new();

    public ScheduleOneNexusRowEnricher(INexusService nexus, CoverCache covers, IHostServices host)
    { _nexus = nexus; _covers = covers; _host = host; }

    /// <summary>Zieht Detail + Cover fuer eine ModId und schreibt die Werte
    /// in die Row. No-op wenn Row bereits enriched oder ModId null.</summary>
    public async Task EnrichAsync(IScheduleOneEnrichableRow row, CancellationToken ct = default)
    {
        if (row.NexusModId is not int modId) return;
        if (row.IsEnriched) return;
        row.IsEnriched = true; // vor await setzen — verhindert Doppel-Enrich bei Refresh

        try
        {
            await _apiGate.WaitAsync(ct);
            NexusModDetail? detail;
            lock (_cacheLock) _detailCache.TryGetValue(modId, out detail);
            try
            {
                if (detail is null)
                {
                    detail = await _nexus.GetModDetailAsync(ScheduleOneNexusCatalog.GameSlug, modId, ct);
                    if (detail is not null)
                        lock (_cacheLock) _detailCache[modId] = detail;
                }
            }
            finally { _apiGate.Release(); }

            if (detail is null) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (string.IsNullOrWhiteSpace(row.NexusName)) row.NexusName = detail.Name;
                if (string.IsNullOrWhiteSpace(row.NexusAuthor)) row.NexusAuthor = detail.Author;
                if (string.IsNullOrWhiteSpace(row.NexusVersion)) row.NexusVersion = detail.Version;
                row.NexusSummary = detail.Summary;
                row.HasNexusMatch = true;
            });

            if (!string.IsNullOrEmpty(detail.PictureUrl))
            {
                var bytes = await _covers.GetOrDownloadBytesAsync(detail.PictureUrl);
                if (bytes is null) return;
                var bmp = await _host.Images.DecodeAsync(bytes, ct);
                if (bmp is null) return;
                await Dispatcher.UIThread.InvokeAsync(() => row.Cover = bmp);
            }
        }
        catch (OperationCanceledException) { /* Tab-Wechsel — silent */ }
        catch (Exception ex)
        {
            Log.Debug(ex, "Enrich fehlgeschlagen mod_id={Id}", modId);
        }
    }

    /// <summary>Enricht einen Batch von Rows (Downloads oder Installed).
    /// Sequentiell durchs Semaphore-Gate, kleine Delays zwischen Rows —
    /// UI-Message-Loop bekommt Luft, Nexus-Ratelimit wird nicht getriggert.</summary>
    public async Task EnrichBatchAsync(IEnumerable<IScheduleOneEnrichableRow> rows, CancellationToken ct = default)
    {
        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) return;
            await EnrichAsync(row, ct);
            await Task.Delay(50, ct);
        }
    }
}

/// <summary>Row-Kontrakt fuer den Enricher — sowohl DownloadRow als auch
/// ModRow implementieren das. Cover-Property + Nexus-Meta setzt der
/// Enricher (via UI-Thread).</summary>
public interface IScheduleOneEnrichableRow
{
    int? NexusModId { get; }
    bool IsEnriched { get; set; }
    Bitmap? Cover { get; set; }
    string NexusName { get; set; }
    string NexusAuthor { get; set; }
    string NexusVersion { get; set; }
    string NexusSummary { get; set; }
    bool HasNexusMatch { get; set; }
}
