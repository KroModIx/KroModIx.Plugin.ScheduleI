using System.IO;
using FluentAssertions;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

public class MelonLoaderScannerTests
{
    /// <summary>MelonLoader hat KEIN Ordner-Layout — nur flat DLLs im Mods-Root
    /// (aktiv + <c>.disabled</c>-Variante). Sub-Ordner werden ignoriert.</summary>
    [Fact]
    public void ScanAll_DetectsFlatDllsOnly_IgnoresSubdirectories()
    {
        using var temp = new TempDir();
        var modsDir = Path.Combine(temp.Path, "Mods");
        Directory.CreateDirectory(modsDir);

        File.WriteAllText(Path.Combine(modsDir, "ActiveMod.dll"), "test");
        File.WriteAllText(Path.Combine(modsDir, "OldMod.dll.disabled"), "test");
        // Sub-Ordner mit DLL wird ignoriert — MelonLoader scannt nicht rekursiv.
        Directory.CreateDirectory(Path.Combine(modsDir, "SubDir"));
        File.WriteAllText(Path.Combine(modsDir, "SubDir", "IgnoredMod.dll"), "test");
        // Nicht-DLL-Files werden ignoriert.
        File.WriteAllText(Path.Combine(modsDir, "readme.txt"), "test");

        var resolver = new ScheduleOnePathResolver();
        var scanner = new MelonLoaderScanner(resolver);
        var game = FakeGame(temp.Path);

        var mods = scanner.ScanAll(game);

        mods.Should().HaveCount(2);
        mods.Should().Contain(m => m.Name == "ActiveMod" && m.IsEnabled && !m.IsDirectory);
        mods.Should().Contain(m => m.Name == "OldMod" && !m.IsEnabled && !m.IsDirectory);
    }

    /// <summary>Marker fuer MelonLoader-Install: <c>version.dll</c> (Proxy-Loader)
    /// im Game-Root ODER <c>MelonLoader/net6/MelonLoader.dll</c> (Managed-DLL
    /// nach erstem Start). Ohne beides ist MelonLoader nicht installiert.</summary>
    [Fact]
    public void LooksLikeMelonLoaderInstall_TrueIfEitherMarkerPresent()
    {
        using var temp = new TempDir();
        var resolver = new ScheduleOnePathResolver();
        var game = FakeGame(temp.Path);

        resolver.LooksLikeMelonLoaderInstall(game).Should().BeFalse();

        // Marker 1: version.dll im Game-Root reicht.
        File.WriteAllText(Path.Combine(temp.Path, "version.dll"), "");
        resolver.LooksLikeMelonLoaderInstall(game).Should().BeTrue();

        // Nach Cleanup + Marker 2 setzen sollte auch klappen.
        File.Delete(Path.Combine(temp.Path, "version.dll"));
        resolver.LooksLikeMelonLoaderInstall(game).Should().BeFalse();
        Directory.CreateDirectory(Path.Combine(temp.Path, "MelonLoader", "net6"));
        File.WriteAllText(Path.Combine(temp.Path, "MelonLoader", "net6", "MelonLoader.dll"), "");
        resolver.LooksLikeMelonLoaderInstall(game).Should().BeTrue();
    }

    private static DetectedGame FakeGame(string installDir) => new(
        Target: new GameTarget("schedule-i", "Schedule I", SteamAppId: 3164500,
            AlternativeExecutableNames: System.Array.Empty<string>(),
            Platforms: Platforms.Both),
        InstallDir: installDir,
        UserDataDir: null,
        ProtonPrefix: null,
        Runtime: RuntimeKind.Native,
        Source: GameSource.Steam);

    private sealed class TempDir : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "scheduleone-scan-" + System.Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }
}
