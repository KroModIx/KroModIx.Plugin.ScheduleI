using System.IO;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Findet den MelonLoader-Mods-Ordner unter <c>&lt;InstallDir&gt;/Mods/</c>.
/// MelonLoader wird vom Plugin selbst per Auto-Bootstrap installiert
/// (Direct-Download der offiziellen MelonLoader.x64.zip vom
/// LavaGang/MelonLoader-GitHub-Release, entpackt ins Game-Root).
///
/// <para>Schedule I ist IL2CPP-basiert (siehe <c>GameAssembly.dll</c> im
/// Install-Dir) — MelonLoader ≥ v0.6 wird gebraucht.</para></summary>
public sealed class ScheduleOnePathResolver
{
    /// <summary>Absoluter Pfad zum MelonLoader-Mods-Ordner. Rueckgabe garantiert
    /// nicht dass er existiert — <see cref="LooksLikeMelonLoaderInstall"/> davor rufen.</summary>
    private static readonly string[] ModDirCandidates = { "Mods", "mods" };
    private static readonly string[] UserLibsCandidates = { "UserLibs", "userlibs" };

    /// <summary>Der vorhandene Mods-Ordner, oder null. Legt nichts an.</summary>
    public string? GetModsDir(DetectedGame game) =>
        string.IsNullOrEmpty(game.InstallDir)
            ? null
            : ModFolderDiscovery.Find(game.InstallDir, ModDirCandidates);

    /// <summary>Der Mods-Ordner, angelegt falls noetig (v0.6.0). MelonLoader
    /// legt ihn beim ersten Spielstart an — wer vorher moddet, stand bisher
    /// vor einer leeren Liste ohne Install-Ziel. Findet unter Linux auch ein
    /// abweichend geschriebenes <c>mods/</c>.</summary>
    public string? EnsureModsDir(DetectedGame game) =>
        string.IsNullOrEmpty(game.InstallDir)
            ? null
            : ModFolderDiscovery.FindOrCreate(game.InstallDir, ModDirCandidates);

    /// <summary>Optionaler UserLibs-Ordner (fuer shared dependencies von Mods).
    /// MelonLoader erstellt den automatisch beim ersten Start.</summary>
    public string? GetUserLibsDir(DetectedGame game) =>
        string.IsNullOrEmpty(game.InstallDir)
            ? null
            : ModFolderDiscovery.Find(game.InstallDir, UserLibsCandidates);

    /// <summary>MelonLoader-Marker: <c>version.dll</c> im Game-Root ist der
    /// Proxy-Loader den MelonLoader ins Game injiziert. Ohne den ist MelonLoader
    /// nicht installiert oder wurde manuell rausgenommen.</summary>
    public bool LooksLikeMelonLoaderInstall(DetectedGame game)
    {
        if (string.IsNullOrEmpty(game.InstallDir)) return false;
        return File.Exists(Path.Combine(game.InstallDir, "version.dll"))
            || File.Exists(Path.Combine(game.InstallDir, "MelonLoader", "net6", "MelonLoader.dll"));
    }
}
