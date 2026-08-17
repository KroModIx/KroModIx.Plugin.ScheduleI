using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Detail-Dialog v0.5: Cover + Meta + volle Beschreibung + KI-
/// Zusammenfassung. Kein Custom-Chrome — nutzt Standard-Window-Titelleiste
/// analog Cyberpunk-Muster (der Host uebernimmt den Kroste-Style beim
/// Rendering).</summary>
public sealed class NexusModDetailWindow : Window
{
    public NexusModDetailWindow()
    {
        Title = Strings.T("detail.window_title");
        Width = 780;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 500;
        MinHeight = 400;
        CanResize = true;

        // Cover-Frame links (200x120)
        var coverFrame = new Border
        {
            Width = 200, Height = 120, CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var coverPanel = new Panel();
        var coverFallback = new TextBlock
        {
            Text = "\U0001F310", FontSize = 42,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        coverFallback.Classes.Add("muted");
        coverPanel.Children.Add(coverFallback);
        var coverImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(NexusModDetailViewModel.Cover)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        var title = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 18 };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.Title)));

        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 4, 0, 0) };
        void AddMuted(string p, string prefix = "")
        {
            var t = new TextBlock();
            t.Classes.Add("muted");
            t.Bind(TextBlock.TextProperty, new Binding(p)
            {
                StringFormat = prefix.Length > 0 ? prefix + " {0}" : "{0}",
            });
            metaRow.Children.Add(t);
        }
        AddMuted(nameof(NexusModDetailViewModel.Author), Strings.T("detail.meta.author") + ":");
        AddMuted(nameof(NexusModDetailViewModel.VersionDisplay));
        AddMuted(nameof(NexusModDetailViewModel.UpdatedText));
        AddMuted(nameof(NexusModDetailViewModel.EndorsementsText));

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        summary.Classes.Add("secondary");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.SummaryShort)));

        var textStack = new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, metaRow, summary },
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        header.Children.Add(coverFrame);
        header.Children.Add(textStack);

        // Actions-Row
        var aiBtn = new Button { Content = Strings.T("btn.ai_summary") };
        aiBtn.Classes.Add("accent");
        aiBtn.Bind(Button.CommandProperty, new Binding(nameof(NexusModDetailViewModel.SummarizeCommand)));
        aiBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(NexusModDetailViewModel.AiBusy))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(b => !b),
        });

        var openBtn = new Button { Content = Strings.T("btn.open_nexus") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(NexusModDetailViewModel.OpenOnNexusCommand)));

        var closeBtn = new Button { Content = Strings.T("btn.close") };
        closeBtn.Click += (_, _) => Close();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            Children = { aiBtn, openBtn, closeBtn },
        };

        // AI-Panel (nur sichtbar wenn triggered)
        var aiTitle = new TextBlock
        {
            Text = Strings.T("detail.section.ai_summary"),
            FontWeight = FontWeight.SemiBold,
        };
        aiTitle.Classes.Add("section-label");
        var aiBody = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8),
        };
        aiBody.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.AiSummary)));
        var aiPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8),
            Children = { aiTitle, aiBody },
        };
        aiPanel.Bind(StackPanel.IsVisibleProperty, new Binding(nameof(NexusModDetailViewModel.AiVisible)));

        // Description
        var descTitle = new TextBlock
        {
            Text = Strings.T("detail.section.description"),
            FontWeight = FontWeight.SemiBold,
        };
        descTitle.Classes.Add("section-label");
        var descBody = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        descBody.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.DescriptionText)));

        var body = new StackPanel
        {
            Spacing = 4,
            Children = { aiPanel, descTitle, descBody },
        };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body,
        };

        Content = new DockPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                WithDock(header, Dock.Top),
                WithDock(actions, Dock.Top),
                scroll,
            },
        };
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
