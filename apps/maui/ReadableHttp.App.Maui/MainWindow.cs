namespace ReadableHttp.App.Maui;

public sealed class MainWindow : Window
{
    public MainWindow()
        : base(new MainPage())
    {
        Title = "ReadableHttp";
#if WINDOWS || MACCATALYST
        TitleBar = CreateTitleBar();
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged += HandleRequestedThemeChanged;
        }
#endif
#if WINDOWS
        Created += (_, _) =>
        {
            Handler?.MauiContext?.Services.GetService<WindowsTrayIconService>()?.Initialize(this);
        };
        Destroying += (_, _) =>
        {
            Handler?.MauiContext?.Services.GetService<WindowsTrayIconService>()?.Dispose();
        };
#endif
    }

#if WINDOWS || MACCATALYST
    private void HandleRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        if (TitleBar is TitleBar titleBar)
        {
            ApplyTitleBarTheme(titleBar);
        }
    }

    private TitleBar CreateTitleBar()
    {
        var titleBar = new TitleBar
        {
            Title = "ReadableHttp",
            Icon = ImageSource.FromFile("titlebar_icon.png"),
            HeightRequest = 40,
            TrailingContent = CreateTitleBarActions()
        };
        ApplyTitleBarTheme(titleBar);
        return titleBar;
    }

    private static void ApplyTitleBarTheme(TitleBar titleBar)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var background = isDark ? Color.FromArgb("#111827") : Color.FromArgb("#F8FAFC");
        var foreground = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#0F172A");
        var actionIcon = isDark ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#0F172A");

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;

        if (titleBar.TrailingContent is HorizontalStackLayout actions)
        {
            foreach (var button in actions.Children.OfType<Button>())
            {
                button.BackgroundColor = Colors.Transparent;
                button.BorderColor = Colors.Transparent;
                button.BorderWidth = 0;
                button.TextColor = actionIcon;
            }
        }
    }

    private View CreateTitleBarActions()
    {
        return new HorizontalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                CreateTitleBarButton("\uE90C", "折叠/展开左边栏", AppCommand.ToggleExplorer),
                CreateTitleBarButton("\uE90D", "折叠/展开右边栏", AppCommand.ToggleInspector)
            }
        };
    }

    private Button CreateTitleBarButton(string text, string tooltip, AppCommand command)
    {
        var button = new Button
        {
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0,
            CornerRadius = 5,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            Text = text,
            TextColor = Color.FromArgb("#F8FAFC"),
            FontFamily = "SegoeFluentIcons",
            FontSize = 16
        };
        ToolTipProperties.SetText(button, tooltip);
        button.Clicked += async (_, _) => await RequestAppCommandAsync(command);
        return button;
    }

    private async Task RequestAppCommandAsync(AppCommand command)
    {
        if (Handler?.MauiContext?.Services.GetService(typeof(AppCommandService)) is AppCommandService commands)
        {
            await commands.RequestAsync(command);
        }
    }
#endif
}
