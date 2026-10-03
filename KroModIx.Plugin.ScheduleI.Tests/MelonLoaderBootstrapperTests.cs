using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KroModIx.Plugin.ScheduleI.Services;
using KroModIx.Plugin.TestKit;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

/// <summary>Der MelonLoader-Bootstrap war bis v0.7.0 <b>ungetestet</b> — und
/// trug eine fest hinterlegte Ausweich-URL auf <c>v0.7.3</c>, die mit jeder
/// neuen MelonLoader-Ausgabe weiter veraltet wäre. Ein Nutzer, der beim
/// GitHub-Limit landet, hätte stillschweigend eine alte Fassung bekommen.
///
/// <para>Geprüft wird hier vor allem, <b>welche URL</b> der Bootstrap
/// anfragt — das ist die Entscheidung, die er verantwortet. Der Download
/// selbst läuft über eine Attrappe, die immer dasselbe ZIP ausliefert und
/// die angefragte Adresse mitschreibt.</para></summary>
public sealed class MelonLoaderBootstrapperTests : IDisposable
{
    private const string Repo = "LavaGang/MelonLoader";
    private const string AssetName = "MelonLoader.x64.zip";

    private readonly string _tmp;
    private readonly string _installDir;
    private readonly FakeGitHubService _gh = new();
    private readonly FakeArchiveService _archives = new();
    private readonly AuslieferHandler _handler;
    private readonly MelonLoaderBootstrapper _sut;

    public MelonLoaderBootstrapperTests()
    {
        _tmp = Directory.CreateTempSubdirectory("kromodix-s1-melon").FullName;
        _installDir = Path.Combine(_tmp, "game");
        Directory.CreateDirectory(_installDir);

        _handler = new AuslieferHandler(BaueMelonZip());
        _sut = new MelonLoaderBootstrapper(new HttpClient(_handler), _gh, _archives);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    /// <summary>Ein ZIP mit dem Layout, das MelonLoader.x64.zip wirklich
    /// hat: alles auf Wurzelebene.</summary>
    private static byte[] BaueMelonZip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pfad in new[]
                { "version.dll", "dobby.dll", "MelonLoader/net6/MelonLoader.dll", "NOTICE.txt" })
            {
                using var s = zip.CreateEntry(pfad).Open();
                s.Write([1, 2, 3]);
            }
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Nimmt_die_Datei_aus_der_neuesten_Ausgabe()
    {
        _gh.AddRelease(Repo, "v0.8.1", "MelonLoader.Linux.x64.zip", AssetName,
            "MelonLoader.Installer.exe");

        var r = await _sut.InstallAsync(_installDir, ct: TestContext.Current.CancellationToken);

        r.Success.Should().BeTrue(r.ErrorMessage);
        r.Version.Should().Be("v0.8.1");
        _handler.Angefragt.Should().ContainSingle()
            .Which.Should().Be($"https://github.com/{Repo}/releases/download/v0.8.1/{AssetName}");
        File.Exists(Path.Combine(_installDir, "version.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_installDir, "MelonLoader", "net6", "MelonLoader.dll"))
            .Should().BeTrue();
    }

    /// <summary>Die Linux-Fassung und der Installer dürfen <b>nicht</b>
    /// genommen werden: Schedule I läuft unter Proton mit der
    /// Windows-Fassung. Der Dateiname wird exakt verglichen, nicht per
    /// „enthält".</summary>
    [Fact]
    public async Task Nimmt_nicht_die_Linux_Fassung_und_nicht_den_Installer()
    {
        _gh.AddRelease(Repo, "v0.8.1", "MelonLoader.Linux.x64.zip",
            "MelonLoader.Installer.exe", "MelonLoader.x86.zip");

        var r = await _sut.InstallAsync(_installDir, ct: TestContext.Current.CancellationToken);

        // Keine passende Datei in der Liste → der Bootstrap bildet die URL
        // aus Tag und Namenskonvention. Was er NICHT tut, ist eine der
        // falschen Dateien zu nehmen.
        _handler.Angefragt.Should().ContainSingle()
            .Which.Should().EndWith("/" + AssetName);
        r.Success.Should().BeTrue(r.ErrorMessage);
    }

    /// <summary>Der eigentliche Grund für die Migration. Greift die
    /// Raten-Sperre, kennt der Baukasten nur den Tag — die URL entsteht aus
    /// Tag und Namenskonvention, und sie zeigt auf die <b>aktuelle</b>
    /// Ausgabe. Vorher kam hier eine fest hinterlegte <c>v0.7.3</c>.</summary>
    [Fact]
    public async Task Bei_Raten_Sperre_kommt_der_aktuelle_Tag_und_nicht_eine_feste_Fassung()
    {
        _gh.AddRelease(Repo, "v0.8.1", AssetName);
        _gh.RateLimited = true;

        var r = await _sut.InstallAsync(_installDir, ct: TestContext.Current.CancellationToken);

        r.Success.Should().BeTrue(r.ErrorMessage);
        r.Version.Should().Be("v0.8.1");
        _handler.Angefragt.Should().ContainSingle()
            .Which.Should().Be($"https://github.com/{Repo}/releases/download/v0.8.1/{AssetName}");
        _handler.Angefragt[0].Should().NotContain("v0.7.3",
            "die fest hinterlegte Fassung ist mit v0.7.0 entfallen");
    }

    /// <summary>Ohne Ausgabe wird nicht geraten, sondern gemeldet. Vorher
    /// hätte der Bootstrap hier die fest hinterlegte URL geladen und so
    /// getan, als wäre alles in Ordnung.</summary>
    [Fact]
    public async Task Ohne_Ausgabe_wird_gemeldet_statt_geraten()
    {
        var r = await _sut.InstallAsync(_installDir, ct: TestContext.Current.CancellationToken);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("Kein MelonLoader-Release gefunden");
        _handler.Angefragt.Should().BeEmpty("ohne Ziel wird nichts heruntergeladen");
        Directory.GetFiles(_installDir).Should().BeEmpty();
    }

    /// <summary>Ein Ausbruchsversuch in einem offiziellen Release wäre ein
    /// Alarmzeichen — dann bricht der Bootstrap ab, statt den Rest zu
    /// installieren.</summary>
    [Fact]
    public async Task Ausbruchsversuch_im_Release_bricht_ab()
    {
        _gh.AddRelease(Repo, "v0.8.1", AssetName);
        var opfer = Path.Combine(_tmp, "ausserhalb.txt");
        _handler.Inhalt = BaueZipMit(("version.dll", true), (opfer, true));

        var r = await _sut.InstallAsync(_installDir, ct: TestContext.Current.CancellationToken);

        File.Exists(opfer).Should().BeFalse();
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("sollte bei einem offiziellen Release nicht vorkommen");
    }

    private static byte[] BaueZipMit(params (string Pfad, bool _)[] eintraege)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (pfad, _) in eintraege)
            {
                using var s = zip.CreateEntry(pfad).Open();
                s.Write([1]);
            }
        return ms.ToArray();
    }

    /// <summary>Liefert immer dasselbe ZIP und schreibt mit, welche Adresse
    /// angefragt wurde. Ein Test, der dafür ins Netz greift, würde prüfen,
    /// was GitHub heute als neuestes MelonLoader ausliefert — nicht, was das
    /// Plugin daraus macht.</summary>
    private sealed class AuslieferHandler(byte[] inhalt) : HttpMessageHandler
    {
        public List<string> Angefragt { get; } = [];
        public byte[] Inhalt { get; set; } = inhalt;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Angefragt.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Inhalt),
            });
        }
    }
}
