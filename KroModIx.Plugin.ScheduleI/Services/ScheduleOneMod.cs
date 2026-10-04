using System;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Ein MelonLoader-Mod im <c>Mods/</c>-Ordner. Immer eine
/// einzelne DLL (`SomeMod.dll`) — MelonLoader unterstuetzt kein Ordner-
/// Layout (anders als BepInEx). Toggle via <c>.disabled</c>-Suffix
/// (MelonLoader ignoriert Dateien mit Extension != .dll).
///
/// <para><see cref="IsDirectory"/> bleibt aus API-Kompatibilitaet mit dem
/// gemeinsamen Row/View-Muster erhalten, ist bei MelonLoader-Scans immer
/// <c>false</c>.</para></summary>
/// <param name="ManagedBy">v0.8.0: gesetzt, wenn dieser Mod einem <b>anderen
/// Mod-Manager</b> gehört (r2modman, Gale, Thunderstore, lmm). Erkannt am
/// Verweis, nicht am Namen — siehe
/// <see cref="KroModIx.Plugin.Contracts.ForeignManagerDetection"/>. Wird
/// gelistet, damit der Nutzer sieht was im Spiel liegt, aber nicht
/// verändert.</param>
public sealed record ScheduleOneMod(
    string Path,
    string Name,
    bool IsEnabled,
    bool IsDirectory,
    long SizeBytes,
    DateTime InstalledUtc,
    string? ManagedBy = null)
{
    /// <summary>Ob das Plugin diesen Eintrag verändern darf.</summary>
    public bool CanModify => ManagedBy is null;
}
