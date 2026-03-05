using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Frikadellen.UI.ViewModels;
using Frikadellen.UI.Views;

namespace Frikadellen.UI;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ToggleTheme()
    {
        if (Current is App app)
        {
            app.RequestedThemeVariant =
                app.RequestedThemeVariant == ThemeVariant.Dark
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
        }
    }
}
