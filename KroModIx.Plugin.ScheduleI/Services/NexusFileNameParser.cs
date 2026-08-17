using System.Text.RegularExpressions;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Extrahiert Nexus-Mod-Id + Version + Name aus einem Nexus-CDN-
/// Filename. Schedule-I-Nexus (analog anderer Nexus-Games) vergibt zwei Formate — beide werden hier gematcht:
///
/// <list type="number">
/// <item><b>Dash-Format (Unix-Timestamp)</b> — Standard, was tatsaechlich in
/// den Downloads landet und in der Nexus-API als <c>NexusFileEntry.FileName</c>
/// steht:
/// <c>&lt;Name&gt;-&lt;modId&gt;-&lt;version.mit.dashes&gt;-&lt;unix-ts&gt;.&lt;ext&gt;</c>
/// Beispiele: <c>Locale-15-1-0-1703155833.7z</c>,
/// <c>Live Console-12-2-1-4-1703155833.zip</c>,
/// <c>Nebula Multiplayer-Client-42-1-8-14-1703155833.zip</c>.</item>
/// <item><b>Space-Format (ISO-Timestamp)</b> — legacy, kommt aus manchen
/// Nexus-CDN-URLs (User speichert manuell aus dem Browser):
/// <c>&lt;Name&gt; &lt;modId&gt; &lt;version&gt; &lt;yyyy-MM-ddTHH-mmZ&gt; &lt;hash&gt;.&lt;ext&gt;</c>
/// Beispiel: <c>Live Console 12 2.1.4 2026-05-12T14-30Z abc123def.zip</c>.</item>
/// </list>
///
/// <para><b>Beide Formate</b> unterstuetzen <c>.zip</c>, <c>.rar</c>, <c>.7z</c>.</para>
///
/// <para>Version wird beim Dash-Format normalisiert (Dashes → Punkte), d.h.
/// aus <c>1-8-14</c> wird <c>1.8.14</c>.</para>
///
/// <para>Der Dash-Anker ist der 10-stellige Unix-Timestamp am Ende (typische
/// Werte in 2020-2030er). Der Space-Anker ist das ISO-Zeitstempel-Muster.
/// Non-greedy <c>.+?</c> im Name-Segment kombiniert mit dem konkreten Rest
/// laesst den Regex-Engine per Backtracking das Richtige finden — auch bei
/// Namen die selbst Bindestriche enthalten (z.B. <c>Nebula-Multiplayer</c>).</para></summary>
public static class NexusFileNameParser
{
    // Dash-Format: <name>-<modId>-<v-parts>-<unix10>.<ext>
    private static readonly Regex DashPattern = new(
        @"^(?<name>.+?)-(?<modId>\d+)(?<version>(?:-\d+)+)-(?<ts>\d{10})\.(?:zip|rar|7z)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Space-Format: <name> <modId> <version> <iso-ts> <hash>.<ext>
    private static readonly Regex SpacePattern = new(
        @"^(?<name>.*?)\s+(?<modId>\d+)\s+(?<version>\S+)\s+(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}-\d{2}Z)\s+[A-Za-z0-9]+\.(?:zip|rar|7z)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static int? TryExtractModId(string fileName)
    {
        var m = DashPattern.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["modId"].Value, out var id1)) return id1;
        m = SpacePattern.Match(fileName);
        if (m.Success && int.TryParse(m.Groups["modId"].Value, out var id2)) return id2;
        return null;
    }

    public static string? TryExtractVersion(string fileName)
    {
        var m = DashPattern.Match(fileName);
        if (m.Success)
        {
            // "-1-0" oder "-2-1-4" → "1.0" bzw "2.1.4"
            var raw = m.Groups["version"].Value.TrimStart('-');
            return raw.Replace('-', '.');
        }
        m = SpacePattern.Match(fileName);
        return m.Success ? m.Groups["version"].Value.Trim() : null;
    }

    public static string? TryExtractModName(string fileName)
    {
        var m = DashPattern.Match(fileName);
        if (m.Success) return m.Groups["name"].Value.Trim();
        m = SpacePattern.Match(fileName);
        return m.Success ? m.Groups["name"].Value.Trim() : null;
    }
}
