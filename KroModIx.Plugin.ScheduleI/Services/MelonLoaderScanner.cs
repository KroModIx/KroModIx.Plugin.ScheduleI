using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Enumeriert alle MelonLoader-Mods unter <c>&lt;InstallDir&gt;/Mods/</c>.
/// MelonLoader hat KEIN Ordner-Layout — jede Mod ist eine einzelne .dll
/// direkt im Mods-Root. Ordner unter Mods/ werden ignoriert (MelonLoader
/// scannt sie nicht rekursiv, ein Mod-Autor der einen Ordner ausliefert,
/// verkabelt ein User-Setup falsch).
///
/// <para>Toggle via <c>.disabled</c>-Suffix — MelonLoader laedt nur
/// Extension <c>.dll</c>.</para></summary>
public sealed class MelonLoaderScanner
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly ScheduleOnePathResolver _paths;

    public MelonLoaderScanner(ScheduleOnePathResolver paths) => _paths = paths;

    public IReadOnlyList<ScheduleOneMod> ScanAll(DetectedGame game)
    {
        var dir = _paths.GetModsDir(game);
        if (!Directory.Exists(dir))
        {
            Log.Debug("Mods-Ordner existiert nicht: {Dir}", dir);
            return Array.Empty<ScheduleOneMod>();
        }

        var result = new List<ScheduleOneMod>();
        foreach (var f in EnumerateSafe(dir, "*.dll*"))
        {
            var name = Path.GetFileName(f);
            var (baseName, enabled) = ClassifyDllName(name);
            if (baseName is null) continue;
            var info = new FileInfo(f);
            result.Add(new ScheduleOneMod(
                Path: f,
                Name: baseName,
                IsEnabled: enabled,
                IsDirectory: false,
                SizeBytes: info.Length,
                InstalledUtc: info.LastWriteTimeUtc));
        }

        return result.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Klassifiziert einen Dateinamen im Mods-Root:
    /// <c>Foo.dll</c> → (Foo, true), <c>Foo.dll.disabled</c> → (Foo, false),
    /// alles andere → (null, _).</summary>
    private static (string? BaseName, bool Enabled) ClassifyDllName(string filename)
    {
        if (filename.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return (filename[..^".dll".Length], true);
        if (filename.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase))
            return (filename[..^".dll.disabled".Length], false);
        return (null, false);
    }

    private static IEnumerable<string> EnumerateSafe(string dir, string pattern)
    {
        try { return Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly); }
        catch { return Array.Empty<string>(); }
    }
}
