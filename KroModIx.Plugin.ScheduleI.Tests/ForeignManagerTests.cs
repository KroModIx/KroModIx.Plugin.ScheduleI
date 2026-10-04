using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

/// <summary>Mods, die ein anderer Mod-Manager ausgeliefert hat. r2modman und
/// Gale bedienen auch MelonLoader-Spiele und legen ihre Profile als Verweise
/// in <c>Mods/</c> ab.
///
/// <para>Der Anlass ist bezahlt, nur in einem anderen Plugin: am 04.10.2026
/// hat ein Deinstallieren-Klick im Icarus-Plugin lmms zusammengeführtes Pak
/// entfernt und damit lautlos eine Mod aus dem Spiel genommen — die Quelle
/// lag unversehrt in lmms Zwischenspeicher.</para></summary>
public sealed class ForeignManagerTests : IDisposable
{
    private readonly string _tmp = Directory.CreateTempSubdirectory("s1-fremd").FullName;
    private readonly string _mods;

    public ForeignManagerTests()
    {
        _mods = Path.Combine(_tmp, "Mods");
        Directory.CreateDirectory(_mods);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    private string EigeneDll(string name)
    {
        var p = Path.Combine(_mods, name);
        File.WriteAllText(p, "dll");
        return p;
    }

    private string FremdeDll(string name)
    {
        var profil = Path.Combine(_tmp, "config", "r2modman", "profiles", "Default");
        Directory.CreateDirectory(profil);
        var ziel = Path.Combine(profil, name);
        File.WriteAllText(ziel, "dll");
        var link = Path.Combine(_mods, name);
        File.CreateSymbolicLink(link, ziel);
        return link;
    }

    private ScheduleOneMod Scan(string pfad)
    {
        var game = new DetectedGame(
            Target: new GameTarget("schedule-i", "Schedule I", 3164500,
                Array.Empty<string>(), Platforms.Both),
            InstallDir: _tmp, UserDataDir: null, ProtonPrefix: null,
            Runtime: RuntimeKind.Native, Source: GameSource.Steam);
        return new MelonLoaderScanner(new ScheduleOnePathResolver()).ScanAll(game)
            .Single(m => string.Equals(Path.GetFullPath(m.Path), Path.GetFullPath(pfad),
                StringComparison.Ordinal));
    }

    [Fact]
    public void Eine_eigene_Dll_bleibt_veraenderbar()
    {
        var mod = Scan(EigeneDll("MeinMod.dll"));
        mod.CanModify.Should().BeTrue();
        mod.ManagedBy.Should().BeNull();
    }

    [Fact]
    public void Eine_fremd_ausgelieferte_Dll_wird_erkannt_und_benannt()
    {
        var mod = Scan(FremdeDll("FremdMod.dll"));
        mod.CanModify.Should().BeFalse();
        mod.ManagedBy.Should().Be("r2modman");
    }

    [Fact]
    public void Deinstallieren_wird_verweigert_und_die_Datei_bleibt()
    {
        var p = FremdeDll("FremdMod.dll");
        var mod = Scan(p);

        ((Action)(() => new ScheduleOneInstallService().Uninstall(mod))).Should()
            .Throw<InvalidOperationException>().WithMessage("*r2modman*");

        File.Exists(p).Should().BeTrue("genau das ist am 04.10.2026 schiefgegangen");
    }

    [Fact]
    public void Umschalten_wird_verweigert_und_nichts_umbenannt()
    {
        var p = FremdeDll("FremdMod.dll");
        var mod = Scan(p);

        ((Action)(() => new ScheduleOneInstallService().SetEnabled(mod, false))).Should()
            .Throw<InvalidOperationException>().WithMessage("*r2modman*");

        File.Exists(p).Should().BeTrue();
        File.Exists(p + ".disabled").Should().BeFalse();
    }

    /// <summary>Gegenprobe, und die ist die wichtigere Hälfte: eine Sperre,
    /// die den Normalfall mitnimmt, wäre schlimmer als das Problem.</summary>
    [Fact]
    public void Eine_eigene_Mod_laesst_sich_weiter_deinstallieren()
    {
        var p = EigeneDll("MeinMod.dll");

        new ScheduleOneInstallService().Uninstall(Scan(p));

        File.Exists(p).Should().BeFalse();
    }
}
