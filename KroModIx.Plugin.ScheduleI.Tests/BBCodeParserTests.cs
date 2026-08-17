using FluentAssertions;
using KroModIx.Plugin.ScheduleI.Services;
using Xunit;

namespace KroModIx.Plugin.ScheduleI.Tests;

/// <summary>Regression-Guard fuer <see cref="NexusDescriptionParser"/>.
/// Nexus-Descriptions mixen HTML und BBCode — v0.1 hatte nur HTML-Strip,
/// BBCode blieb roh im UI stehen. Fuer jedes reale Muster ein Fixture
/// damit dieser Bug nicht wiederkommt.</summary>
public class BBCodeParserTests
{
    [Theory]
    [InlineData("[center]hello[/center]", "hello")]
    [InlineData("[b]bold[/b] text", "bold text")]
    [InlineData("[i]italic[/i]", "italic")]
    [InlineData("[color=#ff0000]red[/color]", "red")]
    [InlineData("[size=1][i][font=Verdana]tiny[/font][/i][/size]", "tiny")]
    [InlineData("[right]right-aligned[/right]", "right-aligned")]
    public void SimpleContainerTags_StripsAndKeepsContent(string input, string expected)
    {
        NexusDescriptionParser.ToText(input).Should().Be(expected);
    }

    [Fact]
    public void UrlTag_KeepsTextOnly()
    {
        var input = "See [url=https://buymeacoffee.com/foo]buy me a coffee[/url] please";
        NexusDescriptionParser.ToText(input).Should().Be("See buy me a coffee please");
    }

    [Fact]
    public void ImgTag_IsDroppedCompletely()
    {
        var input = "before [img height=100]https://media.giphy.com/foo.gif[/img] after";
        NexusDescriptionParser.ToText(input).Should().Be("before  after");
    }

    [Fact]
    public void LineTag_BecomesAsciiDivider()
    {
        var input = "top[line]bottom";
        var result = NexusDescriptionParser.ToText(input);
        result.Should().Contain("―");
        result.Should().StartWith("top");
        result.Should().EndWith("bottom");
    }

    [Fact]
    public void RealNexusDescription_StripsAllBBCode()
    {
        // Aus dem User-Screenshot (Drones - Forked - Tuning - Colors - Stats - Fullscreen - NV).
        var input = @"[center][line]
[/center]

[center][url=https://buymeacoffee.com/virtunerd][img height=100]https://media3.giphy.com/media/foo/giphy.gif[/img][/url][url=https://ko-fi.com/virtunerd][img height=90]https://media.giphy.com/media/bar/giphy.gif[/img][/url][/center]

[center][url=https://buymeacoffee.com/virtunerd]Send me a beer [/url]if you would [url=https://ko-fi.com/virtunerd]drink one with me[/url][/center]

[right][size=1][i][font=Verdana]Based on the legendary [url=https://www.nexusmods.com/schedule1/mods/907]Drones [/url]mod by [b][color=#00ff00]ThrustGoblin [/color][/b](credit) v0.9.9[/font][/i][/size][/right]";

        var result = NexusDescriptionParser.ToText(input);

        // Kein einziger BBCode-Klammer-Rest darf uebrig sein.
        result.Should().NotContain("[");
        result.Should().NotContain("]");
        // Keine URLs mehr (durch img-Drop + url-Text-Only).
        result.Should().NotContain("giphy.com");
        result.Should().NotContain("buymeacoffee.com");
        result.Should().NotContain("nexusmods.com");
        // Text-Content bleibt lesbar.
        result.Should().Contain("Send me a beer");
        result.Should().Contain("drink one with me");
        result.Should().Contain("ThrustGoblin");
    }
}
