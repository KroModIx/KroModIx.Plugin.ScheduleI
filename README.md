# KroModIx.Plugin.ScheduleI

[![CI](https://github.com/KroModIx/KroModIx.Plugin.ScheduleI/actions/workflows/ci.yml/badge.svg)](https://github.com/KroModIx/KroModIx.Plugin.ScheduleI/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/KroModIx/KroModIx.Plugin.ScheduleI)](https://github.com/KroModIx/KroModIx.Plugin.ScheduleI/releases)

**Schedule I Mod-Manager** — Plugin für den
[KroModIx](https://github.com/KroModIx/KroModIx).

Verwaltet MelonLoader-Mods für Schedule I (TVGS, Steam AppId 3164500,
IL2CPP-basiert). Auto-Install von MelonLoader direkt aus dem Plugin,
Nexus-Katalog-Integration, Downloads-Ordner mit Bulk-Install und
Details-Dialog + KI-Zusammenfassung. DE+EN-Übersetzung.

## Features (v0.1.0)

### Installiert-Tab

- **MelonLoader-Bootstrap-Panel** wenn MelonLoader noch nicht installiert
  ist — Direct-Download der offiziellen `MelonLoader.x64.zip` vom
  LavaGang-GitHub-Release + Entpacken ins Game-Root. Kein Installer-EXE-
  Umweg, funktioniert unter Proton.
- **Discovery** aller flat DLLs unter `<InstallDir>/Mods/`:

  | Zustand | Datei | Toggle |
  |---|---|---|
  | Aktiv | `Mods/MyMod.dll` | `MyMod.dll` → `MyMod.dll.disabled` |
  | Deaktiviert | `Mods/MyMod.dll.disabled` | Rename zurück |

- **Kroste-Card-Row** mit Cover (140×90, aus Nexus-Katalog via
  InstallManifest-ModId), Titel, Autor · Version · Datum, Summary,
  Status-Label, Actions (Toggle, 🔍 Details, 🗑 Deinstallieren).
- **Doppelklick auf Row** öffnet den gleichen Nexus-Detail-Dialog wie
  im Katalog-Tab (falls ModId im InstallManifest hinterlegt).
- **Bulk-Aktionen** mit Progress-Scope: „▶▶ Alle aktivieren" / „⏸⏸ Alle
  deaktivieren" mit Confirm-Dialog.
- **Filter-Textbox** live nach Namen.

### Nexus-Tab

- **Voll-Katalog** via `INexusService.SearchModsAsync` (Contracts v1.15+,
  öffentliches GraphQL, kein Personal-Key nötig für Read).
- Pagination (40 pro Seite), Sort (Update / Add / Endorsed / Downloaded),
  Server-Search, Kategorie-Filter clientseitig.
- **Direct-Download** in den Plugin-Downloads-Ordner (Premium-Only, analog
  Cyberpunk/DSP).
- **Detail-Dialog** (780×640) mit Cover, Meta-Row, KI-Zusammenfassung
  (via `IHostServices.Ai`), HTML-Beschreibung. Sprachabhängiger AI-Prompt.

### Downloads-Tab

- Listet `.zip` / `.rar` / `.7z` im Plugin-Downloads-Ordner.
- **Auto-Layout-Install** via SharpCompress: entweder direktes Extract ins
  Game-Root (wenn Archiv `Mods/`, `UserLibs/` oder `Plugins/` auf einer
  Ebene enthält) oder Flat-Extract aller `.dll` nach `Mods/`.
- **Bulk-Install** aller Downloads mit Progress-Scope.
- **Row-Enrichment**: `NexusFileNameParser` extrahiert ModId aus dem
  Nexus-CDN-Filename (beide Formate: Dash + Space), Enricher zieht
  Cover/Autor/Summary aus dem Katalog. Doppelklick + 🔍 Details.

### Update-Discovery (IUpdateNotifier)

- `ScheduleOneInstallManifestStore` persistiert pro installierter DLL
  ein Manifest mit Nexus-ModId + Version + Original-Filename.
- `ScheduleOneUpdateChecker` matcht Manifests gegen Nexus-Katalog,
  meldet echte Versions-Deltas als grünen ↑-Badge auf der Schedule-I-
  Sidebar-Kachel.

### Sprachumschaltung

DE + EN. Nach Sprachwechsel im Host: Kachel neu selektieren, dann sind
die frischen Übersetzungen aktiv (Host-Tab-Cache-Invalidate seit v1.14.7).

## Build

```bash
dotnet build -c Release
dotnet test
```

## Lizenz

MIT — siehe [LICENSE](LICENSE).
