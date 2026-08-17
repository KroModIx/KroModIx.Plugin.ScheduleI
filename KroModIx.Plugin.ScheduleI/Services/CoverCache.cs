using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.ScheduleI.Services;

/// <summary>Cover-Download-Cache mit SHA1(URL)-Key.
/// <para>v0.3.0: Reines HTTP-Download + Cache-Persistierung. Format-Convert
/// (WebP → PNG, DDS → PNG, …) und Bitmap-Instantiation macht ab jetzt der
/// zentrale Host-Baukasten <see cref="IImageDecoder"/> (Contracts v1.18+).
/// Plugin kippt die geladenen Bytes einfach rein und bekommt eine
/// Avalonia-Bitmap zurueck.</para></summary>
public sealed class CoverCache
{
    private readonly HttpClient _http;
    private readonly IHostServices _host;
    private readonly string _dir;

    public CoverCache(HttpClient http, IHostServices host)
    {
        _http = http;
        _host = host;
        _dir = Path.Combine(host.PluginCacheDir, "nexus-covers");
        Directory.CreateDirectory(_dir);
    }

    /// <summary>Laed URL herunter (oder liest aus Cache) und liefert die
    /// Rohbytes. Beim Cache-Miss: HTTP-GET, Magic-Byte-Check via
    /// <see cref="IImageDecoder.LooksLikeImage"/> (verhindert dass eine
    /// HTML-Login-Wall im Cache landet), atomarer tmp+move ins Cache-File.
    /// Rueckgabe: Bytes oder null.</summary>
    public async Task<byte[]?> GetOrDownloadBytesAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var path = Path.Combine(_dir, Sha1(url) + ".img");
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            try { return await File.ReadAllBytesAsync(path); }
            catch (Exception ex)
            {
                _host.Logger.Debug(ex, "Cover-Cache-Read fehlgeschlagen: {Path}", path);
            }
        }

        try
        {
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0) return null;
            // Sanity: nicht cachen wenn's kein Bild ist (Login-Wall/HTML/JSON)
            if (!_host.Images.LooksLikeImage(bytes))
            {
                _host.Logger.Debug("URL liefert kein Bild — wird nicht gecached: {Url}", url);
                return null;
            }
            var tmp = path + $".tmp.{Guid.NewGuid():N}";
            await File.WriteAllBytesAsync(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
            return bytes;
        }
        catch (Exception ex)
        {
            _host.Logger.Debug(ex, "Cover-Download fehlgeschlagen: {Url}", url);
            return null;
        }
    }

    private static string Sha1(string s)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }
}
