using FluentAssertions;
using KroModIx.Plugin.ScheduleI.Services;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

public class NexusFileNameParserTests
{
    // ---- Dash-Format (der reale Nexus-CDN-Standard bei DownloadPrimaryAsync) ----

    [Theory]
    [InlineData("Live Console-15-1-0-1703155833.7z", 15, "1.0", "Live Console")]
    [InlineData("MoreClients-8-2-1-4-1703155833.zip", 8, "2.1.4", "MoreClients")]
    [InlineData("Cheat Menu-42-1-8-14-1703155833.rar", 42, "1.8.14", "Cheat Menu")]
    [InlineData("Better Deals-11-1-0-0-1703155833.zip", 11, "1.0.0", "Better Deals")]
    public void DashFormat_ExtractsAllFields(string fileName, int expectedId, string expectedVer, string expectedName)
    {
        NexusFileNameParser.TryExtractModId(fileName).Should().Be(expectedId);
        NexusFileNameParser.TryExtractVersion(fileName).Should().Be(expectedVer);
        NexusFileNameParser.TryExtractModName(fileName).Should().Be(expectedName);
    }

    // ---- Space-Format (legacy, CDN-URL-Download aus Browser) ----

    [Theory]
    [InlineData("Live Console 15 1.0 2026-05-12T14-30Z abc123def.zip", 15, "1.0", "Live Console")]
    [InlineData("Better Deals 32605 2.1 2026-08-12T16-25Z X32s3EuCx.rar", 32605, "2.1", "Better Deals")]
    public void SpaceFormat_ExtractsAllFields(string fileName, int expectedId, string expectedVer, string expectedName)
    {
        NexusFileNameParser.TryExtractModId(fileName).Should().Be(expectedId);
        NexusFileNameParser.TryExtractVersion(fileName).Should().Be(expectedVer);
        NexusFileNameParser.TryExtractModName(fileName).Should().Be(expectedName);
    }

    // ---- Nicht-Nexus-Filenames: keine ModId ----

    [Theory]
    [InlineData("some_manual_download.zip")]
    [InlineData("Mod.dll")]
    [InlineData("")]
    public void NonNexusFileNames_ReturnNull(string fileName)
    {
        NexusFileNameParser.TryExtractModId(fileName).Should().BeNull();
        NexusFileNameParser.TryExtractVersion(fileName).Should().BeNull();
        NexusFileNameParser.TryExtractModName(fileName).Should().BeNull();
    }
}
