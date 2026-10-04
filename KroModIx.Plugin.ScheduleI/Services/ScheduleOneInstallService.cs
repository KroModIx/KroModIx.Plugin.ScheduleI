using System.IO;
using NLog;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Toggle + Uninstall fuer MelonLoader-Mods. Enable/Disable via
/// <c>.disabled</c>-Suffix (`Foo.dll` → `Foo.dll.disabled`) — MelonLoader
/// laedt nur Files mit Extension <c>.dll</c>, alles andere wird ignoriert.
/// Reversibel, kein Datei-Verlust. Der Ordner-Toggle-Zweig bleibt aus
/// API-Kompatibilitaet erhalten, wird von MelonLoader-Scans nie getriggert
/// (Scanner setzt <c>IsDirectory=false</c>).</summary>
public sealed class ScheduleOneInstallService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>Wirft, wenn der Eintrag einem fremden Mod-Manager gehört. Die
    /// Meldung nennt Verwalter, Folge und Ausweg — am 04.10.2026 hat genau so
    /// ein Löschen im Icarus-Plugin eine Mod aus dem Spiel genommen, ohne
    /// dass es auffiel.</summary>
    private static void NurWennUnser(ScheduleOneMod mod, string verb)
    {
        if (mod.CanModify) return;
        throw new InvalidOperationException(
            ForeignManagerDetection.Meldung(mod.Name, mod.ManagedBy, verb));
    }

    public string SetEnabled(ScheduleOneMod mod, bool enable)
    {
        NurWennUnser(mod, "umschalten");
        if (mod.IsDirectory) return SetDirEnabled(mod, enable);
        return SetFileEnabled(mod, enable);
    }

    private static string SetFileEnabled(ScheduleOneMod mod, bool enable)
    {
        var path = mod.Path;
        var dir = Path.GetDirectoryName(path)!;
        string newPath;
        if (enable)
        {
            if (!path.EndsWith(".disabled")) return path; // schon aktiv
            var basename = Path.GetFileName(path);
            var trimmed = basename[..^".disabled".Length];
            newPath = Path.Combine(dir, trimmed);
        }
        else
        {
            if (path.EndsWith(".disabled")) return path; // schon aus
            newPath = path + ".disabled";
        }
        if (File.Exists(newPath)) File.Delete(newPath);
        File.Move(path, newPath);
        Log.Info("Toggle-File: {From} -> {To}", path, newPath);
        return newPath;
    }

    private static string SetDirEnabled(ScheduleOneMod mod, bool enable)
    {
        var path = mod.Path;
        var parent = Path.GetDirectoryName(path)!;
        var name = new DirectoryInfo(path).Name;
        string newPath;
        if (enable)
        {
            if (!name.EndsWith(".disabled")) return path;
            var trimmed = name[..^".disabled".Length];
            newPath = Path.Combine(parent, trimmed);
        }
        else
        {
            if (name.EndsWith(".disabled")) return path;
            newPath = path + ".disabled";
        }
        if (Directory.Exists(newPath)) Directory.Delete(newPath, recursive: true);
        Directory.Move(path, newPath);
        Log.Info("Toggle-Dir: {From} -> {To}", path, newPath);
        return newPath;
    }

    public void Uninstall(ScheduleOneMod mod)
    {
        NurWennUnser(mod, "deinstallieren");
        if (mod.IsDirectory)
        {
            if (Directory.Exists(mod.Path)) Directory.Delete(mod.Path, recursive: true);
        }
        else
        {
            if (File.Exists(mod.Path)) File.Delete(mod.Path);
        }
        Log.Info("Uninstall: {Path}", mod.Path);
    }
}
