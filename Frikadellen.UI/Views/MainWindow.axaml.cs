using Avalonia.Controls;
using Avalonia.Input;

namespace Frikadellen.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var drag  = this.FindControl<Border>("DragRegion");
        var bar   = this.FindControl<Border>("TitleBar");
        var min   = this.FindControl<Button>("MinimizeButton");
        var max   = this.FindControl<Button>("MaximizeButton");
        var close = this.FindControl<Button>("CloseButton");

        if (drag != null)
            drag.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

        if (bar != null)
            bar.DoubleTapped += (_, _) => ToggleMaximize();

        if (min   != null) min.Click   += (_, _) => WindowState = WindowState.Minimized;
        if (max   != null) max.Click   += (_, _) => ToggleMaximize();
        if (close != null) close.Click += (_, _) => Close();

        KeyDown += OnKeyDown;
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (DataContext is not ViewModels.MainWindowViewModel vm) return;

        if (e.Key == Key.S)      { vm.StartScript(); e.Handled = true; }
        else if (e.Key == Key.T) { vm.StopScript();  e.Handled = true; }
    }
}
