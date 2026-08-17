using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Direct-Download der Nexus-Mods (Premium-only, sonst null).
/// Analog Cyberpunk-Muster. Zielordner: Plugin-eigenes downloads/.</summary>
public sealed class ScheduleOneDownloader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly INexusService _nexus;
    private readonly HttpClient _http;
    private readonly ScheduleOnePaths _paths;

    public ScheduleOneDownloader(INexusService nexus, HttpClient http, ScheduleOnePaths paths)
    {
        _nexus = nexus;
        _http = http;
        _paths = paths;
    }

    /// <summary>Laedt das primary MAIN-File eines Nexus-Mods. Rueckgabe:
    /// lokaler Pfad oder null (Non-Premium / kein File / Fehler).</summary>
    public async Task<string?> DownloadPrimaryAsync(int modId,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var files = await _nexus.GetFilesAsync(ScheduleOneNexusCatalog.GameSlug, modId, ct);
        var primary = files.FirstOrDefault(f => f.IsPrimary && f.CategoryId == 1)
            ?? files.FirstOrDefault(f => f.CategoryId == 1)
            ?? files.FirstOrDefault();
        if (primary is null)
        {
            Log.Warn("Kein File fuer mod_id={Id}", modId);
            return null;
        }
        var url = await _nexus.GetDownloadLinkAsync(ScheduleOneNexusCatalog.GameSlug, modId, primary.FileId, ct);
        if (url is null)
        {
            Log.Warn("Kein Download-Link fuer mod_id={Id} file_id={FileId} (Premium noetig?)",
                modId, primary.FileId);
            return null;
        }

        var filename = string.IsNullOrEmpty(primary.FileName)
            ? Path.GetFileName(new Uri(url).AbsolutePath)
            : primary.FileName;
        foreach (var c in Path.GetInvalidFileNameChars())
            filename = filename.Replace(c, '_');
        var target = Path.Combine(_paths.DownloadsDir, filename);

        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength;
            var tmp = target + ".part";
            await using (var input = await resp.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(tmp))
            {
                var buf = new byte[81920];
                long done = 0;
                int n;
                while ((n = await input.ReadAsync(buf, ct)) > 0)
                {
                    await output.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    if (total is > 0 && progress is not null)
                        progress.Report((double)done / total.Value);
                }
            }
            File.Move(tmp, target, overwrite: true);
            Log.Info("Downloaded {File} ({Size} bytes)", filename, new FileInfo(target).Length);
            return target;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Download fehlgeschlagen mod_id={Id}", modId);
            return null;
        }
    }
}
