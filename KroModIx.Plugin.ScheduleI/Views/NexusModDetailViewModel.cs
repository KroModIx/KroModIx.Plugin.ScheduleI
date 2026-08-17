using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Nexus-Detail-Dialog v0.5: volle Mod-Beschreibung + Cover +
/// KI-Zusammenfassung via <see cref="IAiService"/>. Kein Download-Button
/// hier (der bleibt in der Katalog-Row) — dieser Dialog ist eher
/// Inspektion vor dem Klick.
///
/// <para>v0.6: zwei Ctors — <see cref="NexusModDetailViewModel(NexusRow, INexusService, CoverCache, IHostServices)"/>
/// aus dem Katalog-Tab und <see cref="NexusModDetailViewModel(int, string?, INexusService, CoverCache, IHostServices)"/>
/// aus Downloads/Installed. Der ModId-only-Ctor laedt Katalog-Werte
/// direkt via <see cref="INexusService.GetModDetailAsync"/> — Titel/Autor/
/// Cover werden nachbestueckt.</para></summary>
public sealed partial class NexusModDetailViewModel : ObservableObject
{
    private readonly int _modId;
    private readonly INexusService _nexus;
    private readonly CoverCache _covers;
    private readonly IHostServices _host;
    private readonly Bitmap? _prefilledCover;

    [ObservableProperty] private Bitmap? _cover;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _versionDisplay = "";
    [ObservableProperty] private string _updatedText = "";
    [ObservableProperty] private string _endorsementsText = "";
    [ObservableProperty] private string _summaryShort = "";
    [ObservableProperty] private string _descriptionText = "";
    [ObservableProperty] private bool _descriptionBusy;

    [ObservableProperty] private string _aiSummary = "";
    [ObservableProperty] private bool _aiBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiSummary))]
    private bool _aiVisible;
    public bool HasAiSummary => !string.IsNullOrWhiteSpace(AiSummary);

    /// <summary>Katalog-Ctor: Katalog-Row liefert Basis-Metadaten sofort,
    /// Detail-Fetch reichert an.</summary>
    public NexusModDetailViewModel(NexusRow row, INexusService nexus,
        CoverCache covers, IHostServices host)
        : this(row.Source.ModId, row.Cover, nexus, covers, host)
    {
        Title = row.Source.Name;
        Author = row.Source.Author;
        VersionDisplay = row.VersionDisplay;
        UpdatedText = row.UpdatedText;
        EndorsementsText = row.EndorsementsText;
        SummaryShort = row.Source.Summary;
    }

    /// <summary>ModId-only Ctor: fuer Downloads/Installed, wo nur die
    /// ModId (aus Manifest oder Filename-Parser) und optional der
    /// bereits enrichte Cover verfuegbar sind. Titel/Autor werden aus
    /// dem Nexus-Detail-Fetch gefuellt.</summary>
    public NexusModDetailViewModel(int modId, Bitmap? prefilledCover,
        INexusService nexus, CoverCache covers, IHostServices host)
    {
        _modId = modId; _nexus = nexus; _covers = covers; _host = host;
        _prefilledCover = prefilledCover;
        Cover = prefilledCover;

        DescriptionText = Strings.T("detail.desc_loading");
        _ = LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        try
        {
            DescriptionBusy = true;
            var detail = await _nexus.GetModDetailAsync(ScheduleOneNexusCatalog.GameSlug, _modId);
            if (detail is null)
            {
                DescriptionText = Strings.T("detail.desc_load_error");
                return;
            }

            // Katalog-Metadaten nachreichen wenn nicht schon vom Katalog-Ctor gesetzt.
            if (string.IsNullOrWhiteSpace(Title)) Title = detail.Name;
            if (string.IsNullOrWhiteSpace(Author)) Author = detail.Author;
            if (string.IsNullOrWhiteSpace(VersionDisplay))
            {
                var v = detail.Version?.Trim() ?? "";
                VersionDisplay = v.Length == 0 ? "" : (char.IsDigit(v[0]) ? "v" + v : v);
            }
            if (string.IsNullOrWhiteSpace(UpdatedText))
            {
                var delta = DateTime.UtcNow - detail.UpdatedUtc;
                UpdatedText = delta.TotalDays < 1 ? "heute"
                    : delta.TotalDays < 2 ? "gestern"
                    : delta.TotalDays < 30 ? $"vor {(int)delta.TotalDays} Tagen"
                    : delta.TotalDays < 365 ? $"vor {(int)(delta.TotalDays / 30)} Monaten"
                    : detail.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd");
            }
            if (string.IsNullOrWhiteSpace(EndorsementsText) && detail.EndorsementCount > 0)
                EndorsementsText = $"👍 {detail.EndorsementCount}";
            if (string.IsNullOrWhiteSpace(SummaryShort)) SummaryShort = detail.Summary;

            var html = detail.DescriptionHtml ?? "";
            DescriptionText = string.IsNullOrWhiteSpace(html)
                ? Strings.T("detail.desc_empty")
                : _host.Descriptions.ToPlainText(html);

            // Falls Katalog-Row keinen Cover hatte, Detail liefert oft doch einen.
            if (Cover is null && !string.IsNullOrEmpty(detail.PictureUrl))
            {
                var bytes = await _covers.GetOrDownloadBytesAsync(detail.PictureUrl);
                if (bytes is not null)
                {
                    var bmp = await _host.Images.DecodeAsync(bytes);
                    if (bmp is not null)
                        await Dispatcher.UIThread.InvokeAsync(() => Cover = bmp);
                }
            }
        }
        catch (Exception ex)
        {
            _host.Logger.Debug(ex, "Detail-Fetch fehlgeschlagen mod_id={Id}", _modId);
            DescriptionText = Strings.T("detail.desc_load_error") + " " + ex.Message;
        }
        finally { DescriptionBusy = false; }
    }

    [RelayCommand]
    private async Task SummarizeAsync()
    {
        if (AiBusy) return;
        if (!await _host.Ai.IsAvailableAsync())
        {
            _host.Notifications.Notify(Strings.T("notify.ai_unavailable"),
                NotificationLevel.Warning);
            return;
        }
        try
        {
            AiBusy = true;
            AiVisible = true;
            AiSummary = string.Format(Strings.T("detail.ai_running_prefix"), _host.Ai.ProviderInfo);
            var systemPrompt = Strings.T("ai.prompt.summary_system");
            var userPrompt = $"Titel: {Title}\nAutor: {Author}\n\nBeschreibung:\n{DescriptionText}";
            var answer = await _host.Ai.CompleteAsync(systemPrompt, userPrompt);
            AiSummary = string.IsNullOrWhiteSpace(answer)
                ? Strings.T("detail.ai_no_answer")
                : answer;
        }
        catch (Exception ex)
        {
            _host.Logger.Warn(ex, "AI-Summary fehlgeschlagen mod_id={Id}", _modId);
            AiSummary = Strings.T("detail.ai_error") + " " + ex.Message;
        }
        finally { AiBusy = false; }
    }

    [RelayCommand]
    private void OpenOnNexus() =>
        _host.Shell.OpenExternalUrl(
            $"https://www.nexusmods.com/{ScheduleOneNexusCatalog.GameSlug}/mods/{_modId}");

    // HTML+BBCode-Parsing der Nexus-Description liegt seit Host v1.20.0 im
    // zentralen Baukasten `_host.Descriptions` (Contracts v1.19+). Der frueher
    // hier gepflegte NexusDescriptionParser wurde 1:1 vom Host uebernommen.
}
