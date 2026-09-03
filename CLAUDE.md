# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Grundlagen

- **Was:** Mod-Manager für Schedule I (TVGS, Unity IL2CPP) als KroModIx-Plugin. Steam-AppId **3164500**.
- **Stack:** .NET 10, `KroModIx.Plugin.Contracts` als PackageReference, `minHostVersion` 1.27.0.
- **Repo:** `github.com/KroModIx/KroModIx.Plugin.ScheduleI`.
- **Deploy-Ziel:** `~/.config/KroModIx/plugins/kroste.scheduleone/`.
- **Kroste-Standards:** `~/.claude/skills/KroModIx-Plugin/`. Hier steht nur, was Schedule-I-spezifisch ist.

## Drei Schreibweisen für denselben Namen — nicht vereinheitlichen

Das ist die häufigste Stolperfalle in diesem Repo. Alle drei Formen sind in Gebrauch und keine davon ist ein Tippfehler:

| Kontext | Wert |
|---|---|
| Repo, Assembly, Namespace | `KroModIx.Plugin.ScheduleI` |
| Plugin-Id und Deploy-Ordner | `kroste.scheduleone` |
| Entry-Type und Klassen-Präfix | `ScheduleOnePlugin`, `ScheduleOne*` |
| `gameId` im Manifest | `schedule-i` |
| **Nexus-Game-Slug** | **`schedule1`** |

Der Nexus-Slug ist der gefährlichste: weder `scheduleone` noch `schedulei`, sondern `schedule1` mit Ziffer (`ScheduleOneNexusCatalog.GameSlug`). Ein falscher Slug liefert eine leere Katalogliste ohne Fehler.

## Loader und Pfade

Schedule I lädt über **MelonLoader** (IL2CPP):

- Mod-Verzeichnis: `<InstallDir>/Mods`
- Zusätzlich: `<InstallDir>/UserLibs` für Bibliotheken
- Loader erkannt an `<InstallDir>/version.dll` **oder** `<InstallDir>/MelonLoader/net6/MelonLoader.dll` — beide Prüfungen sind nötig, je nach MelonLoader-Version und Installationsweg liegt nur eine davon vor.
- Fehlt der Loader, übernimmt `MelonLoaderBootstrapper` die Installation.

## Architektur

- **Services/**: `ScheduleOnePathResolver` / `ScheduleOnePaths`, `MelonLoaderBootstrapper` + `MelonLoaderScanner`, `ScheduleOneNexusCatalog` (GraphQL-Vollkatalog, Auto-Load-All seit v0.2.0), `ScheduleOneNexusRowEnricher`, `NexusFileNameParser`, `ScheduleOneInstallManifestStore`, `ScheduleOneInstallService` / `ScheduleOneZipInstaller`, `ScheduleOneDownloader`, `ScheduleOneUpdateChecker`, `CoverCache`, `DownloadEventBus`.
- **Views/**: Nexus (Katalog), Downloads, Installiert, `NexusModDetailWindow` + VM, `ScheduleOneNexusDetailLauncher`.

## Historie, die den Code erklärt

- **v0.3.0** hat den plugin-eigenen BBCode-Parser rausgeworfen — Beschreibungen laufen seither über `_host.Descriptions`. Nicht wieder lokal nachbauen; die rohen `[center][url=..]`-Reste im UI waren genau der Grund für den zentralen Baukasten.
- **v0.4.1** hat den Manifest-GC aus DSP v0.6.4 portiert (Phantom-Update-Badges nach manuell gelöschten Mod-DLLs).

## Bekannte Grenzen

- **Kein Enable/Disable pro Mod** — MelonLoader lädt, was in `Mods/` liegt.
- **Kein Dependency-Resolver.**
