using System;
using System.IO;
using System.Net.Http;
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
/// mit der Windows-Version + WINE).</para>
///
/// <para><b>Seit v0.7.0 über <c>IHostServices.GitHub</c> und
/// <c>.Archives</c></b> (Host v1.33.0), und <b>die fest hinterlegte
/// Ausweich-URL ist weg.</b> Sie zeigte auf <c>v0.7.3</c> und wäre mit jeder
/// neuen MelonLoader-Ausgabe weiter veraltet — ein Nutzer, der beim
/// GitHub-Limit landet, hätte stillschweigend eine alte Fassung bekommen,
/// ohne es zu erfahren. Der Umleitungs-Pfad des Host-Baukastens liefert
/// stattdessen immer den aktuellen Tag, und der Dateiname ist eine
/// Konvention, die wir kennen — die URL lässt sich daraus bilden. Kein
/// API-Aufruf, kein Limit, nicht veraltend.</para></summary>
public sealed class MelonLoaderBootstrapper
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private const string Repo = "LavaGang/MelonLoader";

    /// <summary>Die Windows-x64-ZIP. <b>Nicht</b>
    /// <c>MelonLoader.Linux.x64.zip</c> und nicht die
    /// <c>.Installer.exe</c> — Schedule I laeuft unter Proton mit der
    /// Windows-Fassung.</summary>
    private const string AssetName = "MelonLoader.x64.zip";

    private readonly HttpClient _http;
    private readonly IGitHubService _gitHub;
    private readonly IArchiveService _archives;

    public MelonLoaderBootstrapper(HttpClient http, IGitHubService gitHub, IArchiveService archives)
    {
        _http = http;
        _gitHub = gitHub;
        _archives = archives;
    }

    /// <summary>Downloadet + entpackt MelonLoader x64 ins <paramref name="installDir"/>.
    /// Bricht bei jedem Fehler mit einer Message ab die dem User sagt was schiefging.</summary>
    public async Task<MelonLoaderInstallResult> InstallAsync(string installDir,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            progress?.Report(0.05);

            var (url, version) = await ResolveAssetAsync(ct).ConfigureAwait(false);
            if (url is null)
                return MelonLoaderInstallResult.Fail(
                    "Kein MelonLoader-Release gefunden — weder über die GitHub-API noch über "
                    + "den Umleitungs-Pfad. Besteht eine Internetverbindung?");

            Log.Info("MelonLoader-Download: {Asset} {Ver} von {Url}", AssetName, version, url);
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
                var r = await Task.Run(() => _archives.Extract(tmp, installDir), ct)
                    .ConfigureAwait(false);
                if (r.SkippedUnsafe.Count > 0)
                {
                    // Bei MelonLoader selbst waere das ein Alarmzeichen: das
                    // ist ein Release eines bekannten Projekts, nicht ein
                    // Nutzer-Archiv. Lieber abbrechen und melden.
                    Log.Warn("MelonLoader-Archiv enthielt {Count} Ausbruchsversuch(e): {Entries}",
                        r.SkippedUnsafe.Count, string.Join(", ", r.SkippedUnsafe));
                    return MelonLoaderInstallResult.Fail(
                        $"Das MelonLoader-Archiv enthielt {r.SkippedUnsafe.Count} Eintrag/Einträge, "
                        + "die aus dem Spielverzeichnis herausschreiben wollten. Abgebrochen — "
                        + "das sollte bei einem offiziellen Release nicht vorkommen.");
                }

                progress?.Report(1.0);
                Log.Info("MelonLoader {Ver} installiert nach {Dir} ({N} Datei(en))",
                    version, installDir, r.Count);
                return MelonLoaderInstallResult.Ok(version ?? "unbekannt");
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* Aufräumen darf scheitern */ }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "MelonLoader-Install fehlgeschlagen");
            return MelonLoaderInstallResult.Fail(ex.Message);
        }
    }

    /// <summary>Erst die Dateiliste der neuesten stabilen Ausgabe; greift die
    /// Raten-Sperre, kennt der Baukasten nur den Tag — dann wird die URL aus
    /// Tag und Dateiname gebildet.
    ///
    /// <para>Das ist der Grund, warum die fest hinterlegte URL entfallen
    /// konnte: der Umleitungs-Pfad kostet kein Kontingent und liefert
    /// trotzdem <b>die aktuelle</b> Ausgabe.</para></summary>
    private async Task<(string? Url, string? Version)> ResolveAssetAsync(CancellationToken ct)
    {
        var hit = await _gitHub.FindLatestAssetAsync(Repo,
            n => n.Equals(AssetName, StringComparison.OrdinalIgnoreCase), ct).ConfigureAwait(false);
        if (hit is not null)
            return (hit.Value.Asset.DownloadUrl, hit.Value.Release.Tag);

        var release = await _gitHub.GetLatestReleaseAsync(Repo, ct).ConfigureAwait(false);
        if (release is null) return (null, null);

        Log.Info("Dateiliste nicht abrufbar ({Grund}) — URL aus Tag {Tag} und Namenskonvention",
            _gitHub.IsRateLimited ? "GitHub-Limit erreicht" : "keine passende Datei gemeldet",
            release.Tag);
        return (_gitHub.BuildAssetUrl(Repo, release.Tag, AssetName), release.Tag);
    }
}

public sealed record MelonLoaderInstallResult(bool Success, string? Version, string? ErrorMessage)
{
    public static MelonLoaderInstallResult Ok(string version) => new(true, version, null);
    public static MelonLoaderInstallResult Fail(string message) => new(false, null, message);
}
