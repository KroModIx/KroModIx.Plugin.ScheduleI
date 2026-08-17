using System.Collections.Generic;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.ScheduleI.Services;

public static class Strings
{
    private static ILocalization? _loc;
    public static void Init(ILocalization loc) => _loc = loc;

    public static string T(string key)
    {
        var iso = _loc?.CurrentIso ?? "de";
        if (iso.StartsWith("en") && En.TryGetValue(key, out var en)) return en;
        if (De.TryGetValue(key, out var de)) return de;
        return key;
    }

    private static readonly Dictionary<string, string> De = new()
    {
        // Tabs
        ["tab.installed"] = "Installiert",
        ["tab.nexus"] = "Nexus",
        ["tab.downloads"] = "Downloads",

        // Common
        ["btn.refresh"] = "🔄  Aktualisieren",
        ["btn.open_folder"] = "📂  Mods/ öffnen",
        ["btn.open_downloads_folder"] = "📂  Downloads öffnen",
        ["btn.enable"] = "▶  Aktivieren",
        ["btn.disable"] = "⏸  Deaktivieren",
        ["btn.uninstall"] = "🗑  Deinstallieren",
        ["btn.enable_all"] = "▶▶  Alle aktivieren",
        ["btn.disable_all"] = "⏸⏸  Alle deaktivieren",
        ["btn.install"] = "📥  Installieren",
        ["btn.install_all"] = "📥  Alle installieren",
        ["btn.delete_file"] = "🗑  Löschen",
        ["btn.download"] = "⬇  Download",
        ["btn.open_nexus"] = "↗  Nexus öffnen",
        ["btn.search"] = "🔎  Suchen",
        ["btn.load_more"] = "📚  Mehr laden",
        ["btn.install_melonloader"] = "⬇  MelonLoader jetzt installieren",
        ["btn.details"] = "🔍  Details",
        ["btn.ai_summary"] = "🤖  KI-Zusammenfassung",
        ["btn.close"] = "Schließen",
        ["detail.window_title"] = "Nexus-Mod-Detail",
        ["detail.meta.author"] = "Autor",
        ["detail.section.description"] = "Beschreibung",
        ["detail.section.ai_summary"] = "🤖 KI-Zusammenfassung",
        ["detail.desc_loading"] = "Beschreibung wird geladen …",
        ["detail.desc_load_error"] = "Fehler beim Laden der Beschreibung.",
        ["detail.desc_empty"] = "Keine Beschreibung im Detail-Endpoint.",
        ["detail.ai_running_prefix"] = "🤖 KI ({0}) analysiert …",
        ["detail.ai_no_answer"] = "Keine Antwort vom KI-Provider.",
        ["detail.ai_error"] = "KI-Fehler:",
        ["notify.ai_unavailable"] = "KI-Provider nicht erreichbar — bitte in den KroModIx-Einstellungen konfigurieren.",
        ["ai.prompt.summary_system"] = "Du bist ein deutschsprachiger MelonLoader-Mod-Reviewer fuer Schedule I. Fasse die Mod-Beschreibung in 3-5 Saetzen zusammen: Was macht der Mod? Welche Features/Balance-Aenderungen/QoL? Ist er kompatibel zum aktuellen Spielstand? Sachlich, kein Werbe-Sprech. Antworte auf Deutsch.",

        // Placeholders + tooltips
        ["placeholder.search"] = "🔍 Filter nach Name …",
        ["placeholder.search_nexus"] = "🔍 Nexus durchsuchen — Enter",
        ["tooltip.premium_download"] = "Direct-Download in den Downloads-Ordner (Nexus-Premium nötig)",
        ["filter.all_categories"] = "Alle Kategorien",

        // Sort
        ["sort.latest_update"] = "Neueste Updates",
        ["sort.latest_add"] = "Neu hinzugefügt",
        ["sort.most_endorsed"] = "Meistgeliked",
        ["sort.most_downloaded"] = "Meistgeladen",

        // Status
        ["status.no_melonloader"] = "MelonLoader nicht installiert (version.dll im Game-Root fehlt). Nutze den Button unten für Auto-Install.",
        ["status.no_mods"] = "Keine Mods in Mods/.",
        ["status.mods_count"] = "{0} Mod(s) — {1} aktiv, {2} deaktiviert.",
        ["status.loading_catalog"] = "Lade Nexus-Katalog …",
        ["status.error_prefix"] = "Fehler: ",
        ["status.mods_of"] = "{0} von {1} Mods geladen",
        ["status.mods_count_catalog"] = "{0} Mods",
        ["status.downloads_dir_missing"] = "Downloads-Ordner existiert nicht: {0}",
        ["status.no_zips_hint"] = "Keine Archive unter {0} — Nexus-Downloads (ZIP/RAR/7z) landen hier.",
        ["status.zips_ready"] = "{0} Archiv(e) bereit zum Install.",

        // Row
        ["row.status_active"] = "aktiv",
        ["row.status_inactive"] = "deaktiviert",

        // Notify
        ["notify.uninstalled_prefix"] = "Deinstalliert: ",
        ["notify.no_enabled_mods"] = "Keine aktiven Mods.",
        ["notify.no_disabled_mods"] = "Keine deaktivierten Mods.",
        ["notify.bulk_disable_result"] = "{0} deaktiviert, {1} Fehler.",
        ["notify.bulk_enable_result"] = "{0} aktiviert, {1} Fehler.",
        ["notify.bulk_install_result"] = "{0} installiert, {1} Fehler.",
        ["notify.premium_required"] = "Direct-Download braucht Nexus-Premium. Klick 'Nexus öffnen' für den Browser-Weg.",
        ["notify.download_fail"] = "Download fehlgeschlagen — Log prüfen (Premium? Rate-Limit?).",
        ["notify.download_ok_prefix"] = "Heruntergeladen: ",
        ["notify.melonloader_installing"] = "MelonLoader wird installiert …",
        ["notify.melonloader_ok"] = "✓ MelonLoader {0} installiert. Starte Schedule I einmal, damit sich MelonLoader initialisiert.",
        ["notify.melonloader_fail"] = "MelonLoader-Install fehlgeschlagen: {0}",

        // Dialogs
        ["dialog.uninstall_title"] = "Deinstallieren?",
        ["dialog.uninstall_msg"] = "{0} wirklich löschen?\n\nPfad: {1}",
        ["dialog.uninstall_ok"] = "Löschen",
        ["dialog.disable_all_title"] = "Alle deaktivieren?",
        ["dialog.disable_all_msg"] = "{0} Mod(s) werden per .disabled-Suffix deaktiviert. Reversibel.",
        ["dialog.disable_all_ok"] = "Deaktivieren",
        ["dialog.install_all_title"] = "Alle installieren?",
        ["dialog.install_all_msg"] = "{0} Archiv(e) werden nacheinander nach Mods/ entpackt. Fortfahren?",
        ["dialog.install_all_ok"] = "Installieren",
        ["dialog.delete_zip_title"] = "Archiv löschen?",
        ["dialog.delete_zip_msg"] = "{0} wirklich löschen? (Nur das Archiv im Downloads-Ordner, schon installierte Dateien in Mods/ bleiben.)",
        ["dialog.delete_zip_ok"] = "Löschen",
        ["dialog.melonloader_install_title"] = "MelonLoader installieren?",
        ["dialog.melonloader_install_msg"] = "Das Plugin lädt MelonLoader x64 (~20 MB) vom offiziellen LavaGang/MelonLoader-GitHub-Release und entpackt es ins Game-Root ({0}). Nach dem Install einmal Schedule I starten damit MelonLoader sich initialisiert. Fortfahren?",
        ["dialog.melonloader_install_ok"] = "Installieren",

        // Progress
        ["progress.disable_bulk"] = "Deaktiviere {0} Mod(s) …",
        ["progress.enable_bulk"] = "Aktiviere {0} Mod(s) …",
        ["progress.install_zips"] = "Installiere {0} Archiv(e) …",
        ["progress.melonloader_install"] = "MelonLoader-Install …",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.installed"] = "Installed",
        ["tab.nexus"] = "Nexus",
        ["tab.downloads"] = "Downloads",

        ["btn.refresh"] = "🔄  Refresh",
        ["btn.open_folder"] = "📂  Open Mods/",
        ["btn.open_downloads_folder"] = "📂  Open downloads",
        ["btn.enable"] = "▶  Enable",
        ["btn.disable"] = "⏸  Disable",
        ["btn.uninstall"] = "🗑  Uninstall",
        ["btn.enable_all"] = "▶▶  Enable all",
        ["btn.disable_all"] = "⏸⏸  Disable all",
        ["btn.install"] = "📥  Install",
        ["btn.install_all"] = "📥  Install all",
        ["btn.delete_file"] = "🗑  Delete",
        ["btn.download"] = "⬇  Download",
        ["btn.open_nexus"] = "↗  Open on Nexus",
        ["btn.search"] = "🔎  Search",
        ["btn.load_more"] = "📚  Load more",
        ["btn.install_melonloader"] = "⬇  Install MelonLoader now",
        ["btn.details"] = "🔍  Details",
        ["btn.ai_summary"] = "🤖  AI summary",
        ["btn.close"] = "Close",
        ["detail.window_title"] = "Nexus mod details",
        ["detail.meta.author"] = "Author",
        ["detail.section.description"] = "Description",
        ["detail.section.ai_summary"] = "🤖 AI summary",
        ["detail.desc_loading"] = "Loading description …",
        ["detail.desc_load_error"] = "Failed to load description.",
        ["detail.desc_empty"] = "No description in the detail endpoint.",
        ["detail.ai_running_prefix"] = "🤖 AI ({0}) analyzing …",
        ["detail.ai_no_answer"] = "No answer from AI provider.",
        ["detail.ai_error"] = "AI error:",
        ["notify.ai_unavailable"] = "AI provider not reachable — configure it in KroModIx settings.",
        ["ai.prompt.summary_system"] = "You are an English-language MelonLoader mod reviewer for Schedule I. Summarize the mod description in 3-5 sentences: What does the mod do? Which features/balance changes/QoL? Is it save-game compatible? Factual, no marketing language. Respond in English.",

        ["placeholder.search"] = "🔍 Filter by name …",
        ["placeholder.search_nexus"] = "🔍 Search Nexus — press Enter",
        ["tooltip.premium_download"] = "Direct download to downloads folder (Nexus Premium required)",
        ["filter.all_categories"] = "All categories",

        ["sort.latest_update"] = "Recently updated",
        ["sort.latest_add"] = "Recently added",
        ["sort.most_endorsed"] = "Most endorsed",
        ["sort.most_downloaded"] = "Most downloaded",

        ["status.no_melonloader"] = "MelonLoader not installed (version.dll in game root missing). Use the button below to auto-install.",
        ["status.no_mods"] = "No mods in Mods/.",
        ["status.mods_count"] = "{0} mod(s) — {1} active, {2} disabled.",
        ["status.loading_catalog"] = "Loading Nexus catalog …",
        ["status.error_prefix"] = "Error: ",
        ["status.mods_of"] = "{0} of {1} mods loaded",
        ["status.mods_count_catalog"] = "{0} mods",
        ["status.downloads_dir_missing"] = "Downloads folder does not exist: {0}",
        ["status.no_zips_hint"] = "No archives in {0} — Nexus downloads (ZIP/RAR/7z) land here.",
        ["status.zips_ready"] = "{0} archive(s) ready to install.",

        ["row.status_active"] = "active",
        ["row.status_inactive"] = "disabled",

        ["notify.uninstalled_prefix"] = "Uninstalled: ",
        ["notify.no_enabled_mods"] = "No active mods.",
        ["notify.no_disabled_mods"] = "No disabled mods.",
        ["notify.bulk_disable_result"] = "{0} disabled, {1} error(s).",
        ["notify.bulk_enable_result"] = "{0} enabled, {1} error(s).",
        ["notify.bulk_install_result"] = "{0} installed, {1} error(s).",
        ["notify.premium_required"] = "Direct download requires Nexus Premium. Click 'Open on Nexus' for the browser flow.",
        ["notify.download_fail"] = "Download failed — check log (Premium? Rate limit?).",
        ["notify.download_ok_prefix"] = "Downloaded: ",
        ["notify.melonloader_installing"] = "Installing MelonLoader …",
        ["notify.melonloader_ok"] = "✓ MelonLoader {0} installed. Start Schedule I once so MelonLoader initializes.",
        ["notify.melonloader_fail"] = "MelonLoader install failed: {0}",

        ["dialog.uninstall_title"] = "Uninstall?",
        ["dialog.uninstall_msg"] = "Really delete {0}?\n\nPath: {1}",
        ["dialog.uninstall_ok"] = "Delete",
        ["dialog.disable_all_title"] = "Disable all?",
        ["dialog.disable_all_msg"] = "{0} mod(s) will be disabled via .disabled suffix. Reversible.",
        ["dialog.disable_all_ok"] = "Disable",
        ["dialog.install_all_title"] = "Install all?",
        ["dialog.install_all_msg"] = "{0} archive(s) will be extracted into Mods/ sequentially. Continue?",
        ["dialog.install_all_ok"] = "Install",
        ["dialog.delete_zip_title"] = "Delete archive?",
        ["dialog.delete_zip_msg"] = "Really delete {0}? (Only the archive in the downloads folder — installed files in Mods/ stay.)",
        ["dialog.delete_zip_ok"] = "Delete",
        ["dialog.melonloader_install_title"] = "Install MelonLoader?",
        ["dialog.melonloader_install_msg"] = "The plugin will download MelonLoader x64 (~20 MB) from the official LavaGang/MelonLoader GitHub release and extract it into the game root ({0}). After install, start Schedule I once so MelonLoader initializes. Continue?",
        ["dialog.melonloader_install_ok"] = "Install",

        ["progress.disable_bulk"] = "Disabling {0} mod(s) …",
        ["progress.enable_bulk"] = "Enabling {0} mod(s) …",
        ["progress.install_zips"] = "Installing {0} archive(s) …",
        ["progress.melonloader_install"] = "MelonLoader install …",
    };
}
