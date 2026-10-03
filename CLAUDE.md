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

## Archive und GitHub kommen aus dem Host (ab v0.7.0)

`ScheduleOneZipInstaller` bekommt `IHostServices.Archives`,
`MelonLoaderBootstrapper` zusätzlich `.GitHub`. SharpCompress ist aus dem
Plugin verschwunden.

**Die fest hinterlegte Ausweich-URL ist weg** — das ist der wichtigste Teil.
Sie zeigte auf `v0.7.3` und wäre mit jeder neuen MelonLoader-Ausgabe weiter
veraltet. Wer beim GitHub-Limit landete, bekam stillschweigend eine alte
Fassung, ohne es zu erfahren. Der Umleitungs-Pfad des Baukastens liefert
stattdessen immer den aktuellen Tag; der Dateiname `MelonLoader.x64.zip` ist
eine Konvention, die wir kennen, also lässt sich die URL daraus bilden —
kein API-Aufruf, kein Limit, nicht veraltend.

**Ohne auffindbare Ausgabe wird gemeldet statt geraten.** Vorher hätte der
Bootstrap die feste URL geladen und so getan, als wäre alles in Ordnung. Ein
fehlendes Release heißt jetzt: Fehlermeldung mit dem Hinweis auf die
Internetverbindung.

**Der Dateiname wird exakt verglichen, nicht per „enthält".** In derselben
Ausgabe liegen `MelonLoader.Linux.x64.zip`, `MelonLoader.x86.zip` und
`MelonLoader.Installer.exe`. Schedule I läuft unter Proton mit der
Windows-x64-Fassung; ein „enthält MelonLoader und endet auf .zip" hätte die
Linux-Fassung genommen. Als Test festgehalten.

**Der Ausbruch-Schutz des Installers prüfte `Contains("..")`.** Das lässt
einen absoluten Eintragsnamen durch, und `Path.Combine` verwirft dann das
Zielverzeichnis. Am Cyberpunk-Installer mit derselben Prüfung am 03.10.2026
nachgewiesen: die Datei landete außerhalb des Spiels, und der Install
meldete Erfolg. Das flache Layout (jede `.dll` nach `Mods/`) entsteht jetzt
über `Filter` + `Flatten` des Baukastens.

**Ein Ausbruchsversuch im MelonLoader-Release selbst bricht ab**, statt den
Rest zu installieren — das ist ein Release eines bekannten Projekts, kein
Nutzer-Archiv; dort wäre so ein Eintrag ein Alarmzeichen.

**Beides war ungetestet.** Jetzt 23 Tests: beide Layouts des Installers,
beide Ausbruch-Fälle, die Inhaltsprüfung, und für den Bootstrap, welche URL
er anfragt — inklusive des Raten-Sperren-Zweigs. Der Download läuft dabei
über eine Attrappe, die immer dasselbe ZIP ausliefert und die angefragte
Adresse mitschreibt; ein Test, der dafür ins Netz greift, würde prüfen, was
GitHub heute als neuestes MelonLoader ausliefert, nicht was das Plugin
daraus macht.
