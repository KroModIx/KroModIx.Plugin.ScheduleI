using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Installiert ein Nexus-Mod-Archiv (ZIP/RAR/7z) unter
/// <c>&lt;InstallDir&gt;/Mods/</c>. MelonLoader hat kein Ordner-Layout
/// (anders als BepInEx), also ist die Auto-Layout-Detection hier
/// einfacher als bei einem BepInEx-basierten Plugin:
/// <list type="bullet">
/// <item>Archive enthaelt <c>Mods/&lt;irgendwas&gt;</c> auf einem Level →
/// direktes Extract ins Game-Root (behaelt Ordner-Struktur, deckt auch
/// <c>UserLibs/</c>-Payloads mit ab).</item>
/// <item>Archive enthaelt DLL(s) direkt auf Root-Ebene oder in einem
/// Sub-Ordner → alle .dll nach <c>Mods/</c> flach entpacken.</item>
/// </list>
///
/// <para>Kein Manifest-Update fuer irrelevante Dateien (README, config
/// samples, docs) — nur .dll-Payloads gehen ins Manifest, sonst schrottet
/// jede Uninstallation Text-Files die anderen Mods gehoeren koennten.</para></summary>
public sealed class ScheduleOneZipInstaller
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IArchiveService _archives;
    private readonly ScheduleOneInstallManifestStore? _manifests;

    public ScheduleOneZipInstaller(IArchiveService archives,
        ScheduleOneInstallManifestStore? manifests = null)
    {
        _archives = archives;
        _manifests = manifests;
    }

    /// <summary>Endungs-Vorfilter fuer den Downloads-Tab. Kommt aus dem
    /// Host-Baukasten, damit ein dort neu unterstuetztes Format nicht in
    /// neun Plugins nachgetragen werden muss.</summary>
    public IReadOnlyList<string> SupportedExtensions => _archives.SupportedExtensions;

    public bool HasSupportedExtension(string path) => _archives.HasSupportedExtension(path);

    public ScheduleOneZipInstallResult Install(string archivePath, DetectedGame game)
    {
        if (!File.Exists(archivePath))
            return ScheduleOneZipInstallResult.Fail($"Archiv nicht gefunden: {archivePath}");
        var installDir = game.InstallDir;
        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            return ScheduleOneZipInstallResult.Fail($"Schedule-I-InstallDir ungueltig: {installDir}");

        try
        {
            // Am Inhalt pruefen, nicht an der Endung: ein Download mit
            // falscher Endung landete sonst unveraendert im Spiel.
            if (_archives.DetectKind(archivePath) == ArchiveKind.Unknown)
                return ScheduleOneZipInstallResult.Fail(
                    "Das ist kein lesbares Archiv (ZIP/RAR/7z) — eventuell ein abgebrochener Download.");

            var entries = _archives.List(archivePath);
            if (entries.Count == 0)
                return ScheduleOneZipInstallResult.Fail("Archiv ist leer.");

            // 1) Bekanntes Layout — enthaelt Mods/ oder UserLibs/ oder Plugins/
            // auf irgend-einer Ebene (typisch bei kompletten Mod-Bundles).
            bool knownLayout = entries.Any(e =>
                e.Path.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase) ||
                e.Path.StartsWith("UserLibs/", StringComparison.OrdinalIgnoreCase) ||
                e.Path.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase));
            if (knownLayout)
            {
                var r = _archives.Extract(archivePath, installDir);
                if (Abgelehnt(r) is { } warnung)
                    return ScheduleOneZipInstallResult.Fail(warnung);
                WriteManifests(r.ExtractedPaths, archivePath);
                return ScheduleOneZipInstallResult.Ok(
                    $"Direkt-Layout: {r.Count} Datei(en) ins Game-Root extrahiert.",
                    r.ExtractedPaths);
            }

            // 2) Fallback: alle .dll finden und flach nach Mods/ extrahieren.
            // Gilt fuer Nexus-Mods die einfach nur eine DLL ausliefern oder
            // eine DLL in einem Root-Ordner (den wir strippen).
            // v0.6.0: anlegen statt annehmen — ohne vorherigen MelonLoader-Start
            // existiert Mods/ nicht, und der Install lief ins Leere.
            var modsDir = ModFolderDiscovery.FindOrCreate(installDir, "Mods", "mods")
                          ?? Path.Combine(installDir, "Mods");
            Directory.CreateDirectory(modsDir);
            if (!entries.Any(e => e.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                return ScheduleOneZipInstallResult.Fail(
                    "Archiv enthaelt keine .dll — kein MelonLoader-Mod erkennbar.");

            var flach = _archives.Extract(archivePath, modsDir, new ArchiveExtractOptions(
                Filter: p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
                Flatten: true));
            if (Abgelehnt(flach) is { } warnung2)
                return ScheduleOneZipInstallResult.Fail(warnung2);
            WriteManifests(flach.ExtractedPaths, archivePath);
            return ScheduleOneZipInstallResult.Ok(
                $"Flat-Layout: {flach.Count} DLL(s) nach Mods/ extrahiert.",
                flach.ExtractedPaths);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Install fehlgeschlagen: {Archive}", archivePath);
            return ScheduleOneZipInstallResult.Fail($"Fehler: {ex.Message}");
        }
    }

    /// <summary>Hat der Ausbruch-Schutz Eintraege abgelehnt, bricht der
    /// Install mit Meldung ab — statt still das zu installieren was durchkam.
    /// Das trifft auch ein bloss kaputtes Archiv mit einem krummen Eintrag
    /// unter zweihundert; bewusst, denn ein Archiv das aus dem
    /// Spielverzeichnis herausschreiben will ist nicht „ueberwiegend in
    /// Ordnung", und die Entscheidung gehoert dem Nutzer.</summary>
    private static string? Abgelehnt(ArchiveExtractResult r)
    {
        if (r.SkippedUnsafe.Count == 0) return null;
        Log.Warn("Ausbruchsversuch im Archiv, {Count} Eintrag/Eintraege abgelehnt: {Entries}",
            r.SkippedUnsafe.Count, string.Join(", ", r.SkippedUnsafe));
        return $"Abgebrochen: {r.SkippedUnsafe.Count} Eintrag/Eintraege wollten aus dem "
             + "Spielverzeichnis herausschreiben — "
             + string.Join(", ", r.SkippedUnsafe.Take(3))
             + (r.SkippedUnsafe.Count > 3 ? ", …" : "")
             + $". {r.Count} Datei(en) waren schon geschrieben, bevor das auffiel.";
    }

    /// <summary>Fuer jeden installierten DLL-Basename ein Manifest im
    /// <see cref="ScheduleOneInstallManifestStore"/> speichern. ModId +
    /// Version aus dem Nexus-CDN-Filename (Dash- oder Space-Format) —
    /// sonst leer, dann kein Update-Discovery moeglich fuer diesen Mod.</summary>
    private void WriteManifests(IReadOnlyList<string> installedPaths, string archivePath)
    {
        if (_manifests is null) return;
        var archiveName = Path.GetFileName(archivePath);
        var nexusModId = NexusFileNameParser.TryExtractModId(archiveName);
        var nexusVersion = NexusFileNameParser.TryExtractVersion(archiveName);

        var dllBasenames = installedPaths
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in dllBasenames)
        {
            var key = ScheduleOneInstallManifestStore.BuildKey(name);
            _manifests.Save(key, new ScheduleOneInstallManifest(
                NexusModId: nexusModId,
                OriginalFilename: archiveName,
                NexusVersion: nexusVersion,
                InstalledAtUtc: DateTime.UtcNow));
        }
    }

}

public sealed record ScheduleOneZipInstallResult(bool Success, string Message, IReadOnlyList<string> InstalledPaths)
{
    public static ScheduleOneZipInstallResult Ok(string msg, IReadOnlyList<string> paths) => new(true, msg, paths);
    public static ScheduleOneZipInstallResult Fail(string msg) => new(false, msg, Array.Empty<string>());
}
