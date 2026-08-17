using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Laedt MelonLoader direkt vom LavaGang/MelonLoader-GitHub-Release
/// und entpackt es ins Game-Root. Ohne diesen Bootstrap-Service muesste der
/// User MelonLoader.Installer.exe ausfuehren (unter Proton umstaendlich) oder
/// MelonLoader.x64.zip manuell herunterladen und entpacken — genau der
/// Reibungspunkt den ein Modmanager wegnehmen soll (Skill Kernprinzip 6).
///
/// <para><b>Schedule I ist IL2CPP</b> (siehe <c>GameAssembly.dll</c> im
/// Install-Dir). MelonLoader ≥v0.6 hat IL2CPP-Support; wir nehmen das
/// aktuelle stable Release. Asset-Pattern: <c>MelonLoader.x64.zip</c>
/// (nicht <c>MelonLoader.Linux.x64.zip</c> — Schedule I laeuft unter Proton
/// mit der Windows-Version + WINE).</para></summary>
public sealed class MelonLoaderBootstrapper
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string ReleasesApi = "https://api.github.com/repos/LavaGang/MelonLoader/releases";

    private readonly HttpClient _http;

    /// <summary>Fallback-URL wenn die GitHub-API fehlschlaegt (rate limit,
    /// Netz weg). Bekannter stable Release der zum Schedule-I-Zeitpunkt
    /// aktuell war. Kann bei Bedarf per neuem Plugin-Release aktualisiert
    /// werden. GitHub-CDN erlaubt anonymous Downloads ohne API-Rate-Limit.</summary>
    private const string FallbackAsset =
        "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip";
    private const string FallbackVersion = "v0.7.3";

    public MelonLoaderBootstrapper(HttpClient http) => _http = http;

    /// <summary>Downloadet + entpackt MelonLoader x64 ins <paramref name="installDir"/>.
    /// Bricht bei jedem Fehler mit einer Message ab die dem User sagt was schiefging.</summary>
    public async Task<MelonLoaderInstallResult> InstallAsync(string installDir,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            progress?.Report(0.05);
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd("KroModIx-ScheduleI-Plugin/1.0");
            _http.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github+json");
            // Optional: GITHUB_TOKEN aus Env-Var → 5000 statt 60 req/h (analog PluginUpdateService v1.10.2).
            var ghToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(ghToken))
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ghToken);

            // Erst API probieren — liefert neueste stable Version.
            var (url, assetName, version) = await TryFindLatestFromApiAsync(ct);

            // Fallback: hartcoded latest-known-good (KEIN API-Call).
            // Greift bei GitHub-403 (rate limit), Netz-Ausfall oder wenn kein
            // stable Release die Assets liefert.
            if (url is null)
            {
                Log.Info("GitHub-API-Fallback aktiv — verwende {Ver} direkt", FallbackVersion);
                url = FallbackAsset;
                assetName = Path.GetFileName(FallbackAsset);
                version = FallbackVersion;
            }

            Log.Info("MelonLoader-Download: {Asset} von {Url}", assetName, url);
            progress?.Report(0.1);

            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var tmp = Path.Combine(Path.GetTempPath(),
                $"melonloader-scheduleone-{Guid.NewGuid():N}.zip");
            try
            {
                long total = resp.Content.Headers.ContentLength ?? 0;
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
                        if (total > 0 && progress is not null)
                            progress.Report(0.1 + (double)done / total * 0.7);
                    }
                }
                progress?.Report(0.85);

                // Ins Game-Root extrahieren — MelonLoader.x64.zip enthaelt
                // bereits MelonLoader/, Mods/, Plugins/, UserData/, UserLibs/,
                // dobby.dll, version.dll, NOTICE.txt auf Root-Ebene.
                await Task.Run(() =>
                {
                    using var zip = ZipFile.OpenRead(tmp);
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Directory-Marker
                        var target = Path.Combine(installDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        // Zip-Slip-Prevention
                        var full = Path.GetFullPath(target);
                        if (!full.StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Warn("Zip-Slip-Attempt: {Entry}", entry.FullName);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        entry.ExtractToFile(target, overwrite: true);
                    }
                }, ct);
                progress?.Report(1.0);
                Log.Info("MelonLoader {Ver} installiert nach {Dir}", version, installDir);
                return MelonLoaderInstallResult.Ok(version ?? "unbekannt");
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "MelonLoader-Install fehlgeschlagen");
            return MelonLoaderInstallResult.Fail(ex.Message);
        }
    }

    /// <summary>Versucht ueber die GitHub-API das neueste stable-MelonLoader-Release
    /// zu finden. Liefert (null, null, null) bei jedem Fehler (403 Rate-Limit,
    /// Netz weg, Kein-Match). Caller faellt dann auf <see cref="FallbackAsset"/>
    /// zurueck.</summary>
    private async Task<(string? Url, string? AssetName, string? Version)> TryFindLatestFromApiAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(ReleasesApi + "?per_page=10", ct);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Info("GitHub-API {Status} — nutze Fallback-Asset", (int)resp.StatusCode);
                return (null, null, null);
            }
            var releasesJson = await resp.Content.ReadAsStringAsync(ct);
            var releases = JsonSerializer.Deserialize<GhRelease[]>(releasesJson, JsonOpts);
            if (releases is null || releases.Length == 0) return (null, null, null);

            foreach (var rel in releases)
            {
                if (rel.Prerelease) continue;
                foreach (var asset in rel.Assets ?? Array.Empty<GhAsset>())
                {
                    var name = asset.Name ?? "";
                    // Wir wollen die reine Windows-x64-ZIP, NICHT die Linux-
                    // Variante (MelonLoader.Linux.x64.zip) und NICHT die
                    // .Installer.exe/.dmg.
                    if (name.Equals("MelonLoader.x64.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        return (asset.BrowserDownloadUrl, name, rel.TagName);
                    }
                }
            }
            return (null, null, null);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "GitHub-API-Query fehlgeschlagen — nutze Fallback");
            return (null, null, null);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        // GitHub-API liefert snake_case (tag_name, browser_download_url,
        // prerelease). PropertyNameCaseInsensitive matcht KEIN snake_case
        // — nur reine Case-Unterschiede.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class GhRelease
    {
        public string? TagName { get; set; }
        public bool Prerelease { get; set; }
        public GhAsset[]? Assets { get; set; }
    }
    private sealed class GhAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
    }
}

public sealed record MelonLoaderInstallResult(bool Success, string? Version, string? ErrorMessage)
{
    public static MelonLoaderInstallResult Ok(string version) => new(true, version, null);
    public static MelonLoaderInstallResult Fail(string message) => new(false, null, message);
}
