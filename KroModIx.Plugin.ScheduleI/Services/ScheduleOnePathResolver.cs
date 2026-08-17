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
    public string GetModsDir(DetectedGame game) =>
        Path.Combine(game.InstallDir, "Mods");

    /// <summary>Optionaler UserLibs-Ordner (fuer shared dependencies von Mods).
    /// MelonLoader erstellt den automatisch beim ersten Start.</summary>
    public string GetUserLibsDir(DetectedGame game) =>
        Path.Combine(game.InstallDir, "UserLibs");

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
