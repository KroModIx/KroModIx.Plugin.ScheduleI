using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Cross-Reference: installierte MelonLoader-Mods (mit Install-
/// Manifest, das eine Nexus-ModId enthaelt) vs Nexus-Katalog-latest.
///
/// <para>Nur Mods mit gueltigem Install-Manifest werden gecheckt — wenn
/// ein Plugin manuell reinkopiert wurde (kein Nexus-CDN-Filename beim
/// Install), fehlt die ModId und der Update-Check kann nicht matchen.
/// Analog zum Cyberpunk-Muster.</para>
///
/// <para>Version-Vergleich: <see cref="ScheduleOneInstallManifest.NexusVersion"/>
/// (aus Filename beim Install-Zeitpunkt) vs die aktuelle Katalog-Version.
/// Beide werden via <see cref="Version.TryParse"/> geparst — bei
/// unparseablem Format kein Update-Candidate.</para></summary>
public sealed class ScheduleOneUpdateChecker
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ScheduleOneInstallManifestStore _manifests;
    private readonly ScheduleOneNexusCatalog _catalog;

    private IReadOnlyList<ScheduleOneUpdateCandidate> _pending = Array.Empty<ScheduleOneUpdateCandidate>();
    private DateTime _lastCheckUtc;

    public ScheduleOneUpdateChecker(ScheduleOneInstallManifestStore manifests, ScheduleOneNexusCatalog catalog)
    {
        _manifests = manifests;
        _catalog = catalog;
    }

    public IReadOnlyList<ScheduleOneUpdateCandidate> Pending => _pending;
    public int PendingCount => _pending.Count;
    public DateTime LastCheckUtc => _lastCheckUtc;

    public async Task<int> CheckAsync(CancellationToken ct = default)
    {
        var catalog = _catalog.Cached;
        if (catalog.Count == 0)
        {
            // Katalog leer → einmal laden. Billig weil oeffentliches GraphQL.
            try { await _catalog.LoadFirstPageAsync(NexusSort.LatestUpdate, null, ct); }
            catch (Exception ex) { Log.Debug(ex, "Katalog-Load fuer Update-Check fehlgeschlagen"); }
            catalog = _catalog.Cached;
        }
        if (catalog.Count == 0)
        {
            _pending = Array.Empty<ScheduleOneUpdateCandidate>();
            return 0;
        }

        var installed = _manifests.LoadAll()
            .Where(x => x.Manifest.NexusModId is not null
                && !string.IsNullOrWhiteSpace(x.Manifest.NexusVersion))
            .ToList();
        if (installed.Count == 0)
        {
            _pending = Array.Empty<ScheduleOneUpdateCandidate>();
            _lastCheckUtc = DateTime.UtcNow;
            return 0;
        }

        var byModId = catalog.ToDictionary(e => e.ModId);
        var pending = new List<ScheduleOneUpdateCandidate>();
        foreach (var (key, manifest) in installed)
        {
            if (!byModId.TryGetValue(manifest.NexusModId!.Value, out var entry)) continue;
            if (!TryCompareVersions(manifest.NexusVersion!, entry.Version, out var isNewer)) continue;
            if (!isNewer) continue;
            pending.Add(new ScheduleOneUpdateCandidate(
                InstalledName: key,
                InstalledVersion: manifest.NexusVersion!,
                NexusModId: manifest.NexusModId.Value,
                NexusName: entry.Name,
                NexusVersion: entry.Version));
        }
        _pending = pending;
        _lastCheckUtc = DateTime.UtcNow;
        Log.Info("ScheduleOne-Update-Check: {N} Update(s) fuer {Installed} Plugin(s) (Katalog {Cat})",
            pending.Count, installed.Count, catalog.Count);
        return pending.Count;
    }

    public static bool TryCompareVersions(string installed, string nexus, out bool isNewer)
    {
        isNewer = false;
        if (!TryParse(installed, out var i)) return false;
        if (!TryParse(nexus, out var n)) return false;
        isNewer = n > i;
        return true;

        static bool TryParse(string s, out Version v)
        {
            s = s.Trim();
            if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
            var dash = s.IndexOf('-'); if (dash >= 0) s = s[..dash];
            var plus = s.IndexOf('+'); if (plus >= 0) s = s[..plus];
            return Version.TryParse(s, out v!);
        }
    }
}

public sealed record ScheduleOneUpdateCandidate(
    string InstalledName, string InstalledVersion,
    int NexusModId, string NexusName, string NexusVersion);
