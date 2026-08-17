using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using KroModIx.Plugin.ScheduleI.Services;

namespace KroModIx.Plugin.ScheduleI.Views;

/// <summary>Detail-Dialog v0.2: groesserer Frame (920x760), Cover 240×135
/// (16:9), Sektions-Cards fuer AI/Beschreibung mit klarer visueller
/// Trennung, Loading-Placeholder waehrend Detail-Fetch, Kroste-Card-Look
/// via Host-Styles. Nutzt Standard-Window-Titelleiste (Kroste-Chrome
/// kommt vom Host via App.axaml).</summary>
public sealed class NexusModDetailWindow : Window
{
    public NexusModDetailWindow()
    {
        Title = Strings.T("detail.window_title");
        Width = 920;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 620;
        MinHeight = 480;
        CanResize = true;

        Content = BuildRoot();
    }

    private static Control BuildRoot() => new DockPanel
    {
        Margin = new Thickness(20),
        LastChildFill = true,
        Children =
        {
            WithDock(BuildHeaderCard(), Dock.Top),
            WithDock(BuildActionsRow(), Dock.Top),
            BuildScrollableBody(),
        },
    };

    // ---- Header-Card mit Cover + Titel + Meta + Summary ----

    private static Control BuildHeaderCard()
    {
        var coverFrame = new Border
        {
            Width = 240, Height = 135, CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        var coverPanel = new Panel();
        var coverFallback = new TextBlock
        {
            Text = "\U0001F310", FontSize = 48,
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

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold, FontSize = 20,
            TextWrapping = TextWrapping.Wrap,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.Title)));

        // Meta-Zeile: Autor · Version · Alter · 👍 Endorsements
        var metaRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            Margin = new Thickness(0, 6, 0, 0),
        };
        AddMuted(metaRow, nameof(NexusModDetailViewModel.Author),
            Strings.T("detail.meta.author") + ":");
        AddSeparator(metaRow);
        AddMuted(metaRow, nameof(NexusModDetailViewModel.VersionDisplay));
        AddSeparator(metaRow);
        AddMuted(metaRow, nameof(NexusModDetailViewModel.UpdatedText));
        AddSeparator(metaRow);
        AddMuted(metaRow, nameof(NexusModDetailViewModel.EndorsementsText));

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
        };
        summary.Classes.Add("secondary");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.SummaryShort)));

        var textStack = new StackPanel
        {
            Spacing = 0, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18, 0, 0, 0),
            Children = { title, metaRow, summary },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(coverFrame);
        grid.Children.Add(textStack);

        var card = new Border
        {
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid,
        };
        card.Classes.Add("card");
        return card;
    }

    private static void AddMuted(StackPanel row, string bindingPath, string prefix = "")
    {
        var t = new TextBlock();
        t.Classes.Add("muted");
        t.Bind(TextBlock.TextProperty, new Binding(bindingPath)
        {
            StringFormat = prefix.Length > 0 ? prefix + " {0}" : "{0}",
        });
        row.Children.Add(t);
    }

    private static void AddSeparator(StackPanel row)
    {
        var sep = new TextBlock { Text = "·" };
        sep.Classes.Add("muted");
        row.Children.Add(sep);
    }

    // ---- Actions-Zeile ----

    private static Control BuildActionsRow()
    {
        var aiBtn = new Button { Content = Strings.T("btn.ai_summary") };
        aiBtn.Classes.Add("accent");
        aiBtn.Bind(Button.CommandProperty, new Binding(nameof(NexusModDetailViewModel.SummarizeCommand)));
        aiBtn.Bind(Button.IsEnabledProperty, new Binding(nameof(NexusModDetailViewModel.AiBusy))
        {
            Converter = new FuncValueConverter<bool, bool>(b => !b),
        });

        var openBtn = new Button { Content = Strings.T("btn.open_nexus") };
        openBtn.Classes.Add("ghost");
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(NexusModDetailViewModel.OpenOnNexusCommand)));

        var closeBtn = new Button { Content = Strings.T("btn.close") };
        closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
        closeBtn.Click += (sender, _) =>
        {
            if (sender is Control c && TopLevel.GetTopLevel(c) is Window w) w.Close();
        };

        var leftGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { aiBtn, openBtn },
        };

        var grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 12),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumn(leftGroup, 0);
        Grid.SetColumn(closeBtn, 1);
        grid.Children.Add(leftGroup);
        grid.Children.Add(closeBtn);
        return grid;
    }

    // ---- Scrollable Body: AI-Panel + Description ----

    private static Control BuildScrollableBody()
    {
        var aiPanel = BuildAiPanel();
        var descPanel = BuildDescriptionPanel();

        var body = new StackPanel
        {
            Spacing = 12,
            Children = { aiPanel, descPanel },
        };
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body,
            Padding = new Thickness(0, 0, 8, 0), // Platz fuer Scrollbar
        };
    }

    private static Control BuildAiPanel()
    {
        var title = new TextBlock
        {
            Text = Strings.T("detail.section.ai_summary"),
            FontWeight = FontWeight.SemiBold,
        };
        title.Classes.Add("section-label");

        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            LineHeight = 20,
        };
        body.Bind(TextBlock.TextProperty, new Binding(nameof(NexusModDetailViewModel.AiSummary)));

        var stack = new StackPanel { Children = { title, body } };
        var card = new Border { Padding = new Thickness(16), Child = stack };
        card.Classes.Add("card");
        card.Bind(Border.IsVisibleProperty, new Binding(nameof(NexusModDetailViewModel.AiVisible)));
        return card;
    }

    private static Control BuildDescriptionPanel()
    {
        var title = new TextBlock
        {
            Text = Strings.T("detail.section.description"),
            FontWeight = FontWeight.SemiBold,
        };
        title.Classes.Add("section-label");

        // v0.4.0: Rich-HTML-Rendering via _host.Descriptions.CreateRichView
        // (HtmlPanel) statt Plain-Text-TextBlock. Fallback wenn noch nicht
        // fertig geladen: kurzer Loading-TextBlock (DescriptionText enthaelt
        // dann noch "wird geladen …").
        var richHost = new ContentControl
        {
            Margin = new Thickness(0, 6, 0, 0),
        };
        richHost.Bind(ContentControl.ContentProperty,
            new Binding(nameof(NexusModDetailViewModel.DescriptionView)));

        var loadingFallback = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        loadingFallback.Classes.Add("muted");
        loadingFallback.Bind(TextBlock.TextProperty,
            new Binding(nameof(NexusModDetailViewModel.DescriptionText)));
        loadingFallback.Bind(TextBlock.IsVisibleProperty,
            new Binding(nameof(NexusModDetailViewModel.DescriptionView))
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<Control?, bool>(
                    c => c is null),
            });

        var stack = new StackPanel { Children = { title, richHost, loadingFallback } };
        var card = new Border { Padding = new Thickness(16), Child = stack };
        card.Classes.Add("card");
        return card;
    }

    private static Control WithDock(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }
}
