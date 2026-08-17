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
public sealed record ScheduleOneMod(
    string Path,
    string Name,
    bool IsEnabled,
    bool IsDirectory,
    long SizeBytes,
    DateTime InstalledUtc);
