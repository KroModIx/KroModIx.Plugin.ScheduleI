using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KroModIx.Plugin.Contracts;
using NLog;
using SharpCompress.Archives;

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
    private readonly ScheduleOneInstallManifestStore? _manifests;

    public ScheduleOneZipInstaller(ScheduleOneInstallManifestStore? manifests = null)
    {
        _manifests = manifests;
    }

    public ScheduleOneZipInstallResult Install(string archivePath, DetectedGame game)
    {
        if (!File.Exists(archivePath))
            return ScheduleOneZipInstallResult.Fail($"Archiv nicht gefunden: {archivePath}");
        var installDir = game.InstallDir;
        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            return ScheduleOneZipInstallResult.Fail($"Schedule-I-InstallDir ungueltig: {installDir}");

        try
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries
                .Where(e => !e.IsDirectory && !string.IsNullOrEmpty(e.Key))
                .ToList();
            if (entries.Count == 0)
                return ScheduleOneZipInstallResult.Fail("Archiv ist leer.");

            var normalized = entries.Select(e => (e.Key ?? "").Replace('\\', '/')).ToList();

            // 1) Bekanntes Layout — enthaelt Mods/ oder UserLibs/ oder Plugins/
            // auf irgend-einer Ebene (typisch bei kompletten Mod-Bundles).
            bool knownLayout = normalized.Any(p =>
                p.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("UserLibs/", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase));
            if (knownLayout)
            {
                var installed = ExtractDirect(entries, installDir);
                WriteManifests(installed, archivePath);
                return ScheduleOneZipInstallResult.Ok(
                    $"Direkt-Layout: {installed.Count} Datei(en) ins Game-Root extrahiert.", installed);
            }

            // 2) Fallback: alle .dll finden und flach nach Mods/ extrahieren.
            // Gilt fuer Nexus-Mods die einfach nur eine DLL ausliefern oder
            // eine DLL in einem Root-Ordner (den wir strippen).
            var modsDir = Path.Combine(installDir, "Mods");
            Directory.CreateDirectory(modsDir);
            var dlls = entries.Where(e =>
                (e.Key ?? "").EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
            if (dlls.Count == 0)
                return ScheduleOneZipInstallResult.Fail(
                    "Archiv enthaelt keine .dll — kein MelonLoader-Mod erkennbar.");

            var installedFlat = new List<string>();
            foreach (var e in dlls)
            {
                var name = Path.GetFileName(e.Key!);
                var dst = Path.Combine(modsDir, name);
                ExtractOne(e, dst);
                installedFlat.Add(dst);
            }
            WriteManifests(installedFlat, archivePath);
            return ScheduleOneZipInstallResult.Ok(
                $"Flat-Layout: {installedFlat.Count} DLL(s) nach Mods/ extrahiert.",
                installedFlat);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Install fehlgeschlagen: {Archive}", archivePath);
            return ScheduleOneZipInstallResult.Fail($"Fehler: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> ExtractDirect(IEnumerable<IArchiveEntry> entries, string installDir)
    {
        var installed = new List<string>();
        foreach (var e in entries)
        {
            var name = (e.Key ?? "").Replace('\\', '/');
            if (string.IsNullOrEmpty(name) || name.EndsWith('/')) continue;
            if (name.Contains("..")) { Log.Warn("Zip-Slip: {N}", name); continue; }
            var dst = Path.Combine(installDir, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            ExtractOne(e, dst);
            installed.Add(dst);
        }
        return installed;
    }

    private static void ExtractOne(IArchiveEntry entry, string destination)
    {
        using var input = entry.OpenEntryStream();
        using var output = File.Create(destination);
        input.CopyTo(output);
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

    public static readonly string[] SupportedExtensions = new[] { ".zip", ".rar", ".7z" };
}

public sealed record ScheduleOneZipInstallResult(bool Success, string Message, IReadOnlyList<string> InstalledPaths)
{
    public static ScheduleOneZipInstallResult Ok(string msg, IReadOnlyList<string> paths) => new(true, msg, paths);
    public static ScheduleOneZipInstallResult Fail(string msg) => new(false, msg, Array.Empty<string>());
}
