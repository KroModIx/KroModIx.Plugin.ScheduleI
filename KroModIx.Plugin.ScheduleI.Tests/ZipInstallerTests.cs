using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;
using KroModIx.Plugin.TestKit;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

/// <summary>Der Installer hatte bis v0.7.0 (2026-10-03) <b>keine</b> Tests —
/// und einen eigenen Ausbruch-Schutz, der nur auf <c>..</c> prüfte.</summary>
public sealed class ZipInstallerTests : IDisposable
{
    private readonly string _tmp;
    private readonly string _installRoot;
    private readonly DetectedGame _game;
    private readonly FakeArchiveService _archives = new();
    private readonly ScheduleOneZipInstaller _installer;

    public ZipInstallerTests()
    {
        _installer = new ScheduleOneZipInstaller(_archives);
        _tmp = Directory.CreateTempSubdirectory("kromodix-s1-zip").FullName;
        _installRoot = Path.Combine(_tmp, "game");
        Directory.CreateDirectory(_installRoot);

        _game = new DetectedGame(
            Target: new GameTarget("schedule-i", "Schedule I", 3164500,
                Array.Empty<string>(), Platforms.Both),
            InstallDir: _installRoot,
            UserDataDir: null,
            ProtonPrefix: null,
            Runtime: RuntimeKind.Native,
            Source: GameSource.Steam);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    private string BuildZip(string name, params (string Path, string Content)[] entries)
    {
        var zipPath = Path.Combine(_tmp, name);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            using var s = archive.CreateEntry(path).Open();
            using var sw = new StreamWriter(s);
            sw.Write(content);
        }
        return zipPath;
    }

    [Fact]
    public void Direkt_Layout_behaelt_die_Struktur()
    {
        var zip = BuildZip("bundle.zip",
            ("Mods/MeinMod.dll", "dll"),
            ("UserLibs/Hilfe.dll", "dll"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeTrue(r.Message);
        File.Exists(Path.Combine(_installRoot, "Mods", "MeinMod.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_installRoot, "UserLibs", "Hilfe.dll")).Should().BeTrue();
    }

    /// <summary>Der häufigste Nexus-Fall bei MelonLoader: eine DLL, manchmal
    /// in einem Ordner mit dem Mod-Namen. Beides landet flach in
    /// <c>Mods/</c> — seit v0.7.0 über <c>Flatten</c> des Host-Baukastens.</summary>
    [Fact]
    public void Flaches_Layout_legt_jede_Dll_nach_Mods()
    {
        var zip = BuildZip("mod.zip",
            ("MeinMod/MeinMod.dll", "dll"),
            ("MeinMod/liesmich.txt", "Anleitung"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeTrue(r.Message);
        File.Exists(Path.Combine(_installRoot, "Mods", "MeinMod.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_installRoot, "Mods", "liesmich.txt"))
            .Should().BeFalse("nur DLLs gehören in den Mods-Ordner");
        r.InstalledPaths.Should().ContainSingle();
    }

    [Fact]
    public void Ohne_Dll_schlaegt_es_fehl()
    {
        var zip = BuildZip("fremd.zip", ("liesmich.txt", "nur Text"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeFalse();
        r.Message.Should().Contain("kein MelonLoader-Mod erkennbar");
    }

    /// <summary>Der Fall, an dem die alte Prüfung vorbeiging: ein
    /// <b>absoluter</b> Eintragsname enthält kein <c>..</c>, kam also durch,
    /// und <c>Path.Combine</c> verwarf dann das Zielverzeichnis. Am
    /// Cyberpunk-Installer mit derselben Prüfung nachgewiesen.</summary>
    [Fact]
    public void Absoluter_Eintragsname_bricht_nicht_aus()
    {
        var opfer = Path.Combine(_tmp, "ausserhalb.txt");
        var zip = BuildZip("boese.zip",
            ("Mods/MeinMod.dll", "dll"),
            (opfer, "UEBERNOMMEN"));

        var r = _installer.Install(zip, _game);

        File.Exists(opfer).Should().BeFalse();
        r.Success.Should().BeFalse("ein Ausbruchsversuch bricht den Install ab");
        r.Message.Should().Contain("herausschreiben");
    }

    [Fact]
    public void Punkt_Punkt_Pfad_wird_abgelehnt()
    {
        var zip = BuildZip("boese2.zip",
            ("Mods/MeinMod.dll", "dll"),
            ("Mods/../../evil.dll", "boom"));

        var r = _installer.Install(zip, _game);

        File.Exists(Path.Combine(_tmp, "evil.dll")).Should().BeFalse();
        r.Success.Should().BeFalse();
        r.Message.Should().Contain("herausschreiben");
    }

    [Fact]
    public void Kein_Archiv_wird_am_Inhalt_erkannt()
    {
        var kaputt = Path.Combine(_tmp, "abgebrochen.zip");
        File.WriteAllText(kaputt, "das ist kein ZIP");

        var r = _installer.Install(kaputt, _game);

        r.Success.Should().BeFalse();
        r.Message.Should().Contain("kein lesbares Archiv");
    }

    [Fact]
    public void Endungs_Vorfilter_kommt_aus_dem_Baukasten()
    {
        _installer.SupportedExtensions.Should().BeEquivalentTo([".zip", ".rar", ".7z"]);
        _installer.HasSupportedExtension("mod.RAR").Should().BeTrue();
        _installer.HasSupportedExtension("liesmich.txt").Should().BeFalse();
    }
}
