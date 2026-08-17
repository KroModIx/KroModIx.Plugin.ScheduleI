using System.Text.RegularExpressions;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Parst Nexus-Mod-Beschreibungen. Nexus liefert Descriptions als
/// Mix aus HTML (Web-Editor) und <b>BBCode</b> (Vault-Editor, den viele
/// Mod-Autoren nutzen). Ein reiner HTML-Stripper laesst BBCode roh stehen —
/// im UI erschien dann Muell wie
/// <c>[center][url=…][img height=100]https://…[/img][/url][/center]</c>
/// (real passiert in v0.1, siehe User-Screenshot).
///
/// <para>Dieser Parser strippt beides sauber:</para>
/// <list type="bullet">
/// <item>BBCode-Tags mit Argument: <c>[url=…]TEXT[/url]</c> → <c>TEXT</c>,
///   <c>[img …]URL[/img]</c> → weglassen (Inline-Bilder wuerden das
///   Layout sprengen), <c>[color=…]TEXT[/color]</c> → <c>TEXT</c>, etc.</item>
/// <item>BBCode-Container: <c>[center]…[/center]</c>, <c>[right]…[/right]</c>,
///   <c>[b]…[/b]</c>, <c>[i]…[/i]</c>, <c>[size=…]…[/size]</c>,
///   <c>[font=…]…[/font]</c>, <c>[credit]</c> → nur Inhalt behalten.</item>
/// <item>Standalone-BBCode: <c>[line]</c> → ASCII-Trenner, <c>[br]</c> → Newline.</item>
/// <item>HTML-Tags (<c>&lt;br&gt;</c>, <c>&lt;p&gt;</c>, <c>&lt;strong&gt;</c>) analog.</item>
/// <item>HTML-Entities dekodieren, Mehrfach-Leerzeilen kollabieren.</item>
/// </list>
///
/// <para>Frei stehende statische Klasse (nicht in der VM), damit sie
/// unabhaengig von ObservableObject-Contracts + Avalonia testbar bleibt.</para></summary>
public static class NexusDescriptionParser
{
    /// <summary>Wandelt eine Nexus-Description (HTML+BBCode-Mix) in
    /// lesbaren Plain-Text um. Leerer Input → leerer Output.</summary>
    public static string ToText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var s = html;

        // ---- HTML zuerst (kann BBCode enthalten in <p>-Bloecken) ----
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</p\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);

        // ---- BBCode ----

        // [img …]URL[/img] komplett raus (Inline-Bilder wuerden im
        // scrollbaren TextBlock als roher Text erscheinen). Muss VOR
        // [url=…]…[/url] laufen, sonst wird die Inner-URL doppelt gefressen.
        s = Regex.Replace(s, @"\[img[^\]]*\][^\[]*\[/img\]", "", RegexOptions.IgnoreCase);

        // [url=xxx]Text[/url] → Text  (Link-Ziel weglassen; wir haben ja
        // den Nexus-Button, User klickt sich dort weiter)
        s = Regex.Replace(s, @"\[url=[^\]]*\](.*?)\[/url\]", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        s = Regex.Replace(s, @"\[url\](.*?)\[/url\]", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // [line] → ASCII-Trenner
        s = Regex.Replace(s, @"\[line\]", "\n― ― ― ― ― ― ― ― ― ―\n", RegexOptions.IgnoreCase);
        // [br] → newline
        s = Regex.Replace(s, @"\[br\]", "\n", RegexOptions.IgnoreCase);

        // Tags mit optionalem Argument: [tag=…]…[/tag] → nur Inhalt.
        string[] containerTags = { "center", "right", "left", "b", "i", "u", "s",
            "size", "color", "font", "quote", "spoiler", "code", "sub", "sup",
            "list", "credit", "youtube" };
        foreach (var tag in containerTags)
        {
            s = Regex.Replace(s, $@"\[{tag}(?:=[^\]]*)?\](.*?)\[/{tag}\]", "$1",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        // Restliche Standalone-Tags (z.B. [*] in Listen, [hr]) einfach weg.
        s = Regex.Replace(s, @"\[/?[a-zA-Z][^\]]*\]", "");

        // Whitespace-Cleanup: Leerzeilen kollabieren, trailing-Spaces weg.
        s = Regex.Replace(s, @"[ \t]+\n", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }
}
