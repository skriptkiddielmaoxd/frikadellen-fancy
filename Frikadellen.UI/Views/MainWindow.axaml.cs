using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Frikadellen.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wire drag, minimize, maximize, close
        var drag = this.FindControl<Border>("DragRegion");
        var titleBar = this.FindControl<Border>("TitleBar");
        if (drag != null)
            drag.PointerPressed += OnDragRegionPointerPressed;
        if (titleBar != null)
            titleBar.DoubleTapped += OnTitleBarDoubleTapped;

        var min = this.FindControl<Button>("MinimizeButton");
        var max = this.FindControl<Button>("MaximizeButton");
        var close = this.FindControl<Button>("CloseButton");

        if (min != null) min.Click += (_, _) => WindowState = WindowState.Minimized;
        if (max != null) max.Click += (_, _) => ToggleMaximize();
        if (close != null) close.Click += (_, _) => Close();

        // Keyboard shortcuts
        KeyDown += OnMainWindowKeyDown;
    }

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.S)
            {
                // Ctrl+S => Start
                if (DataContext is ViewModels.MainWindowViewModel vm)
                    vm.StartScript();
                e.Handled = true;
            }
            else if (e.Key == Key.T)
            {
                // Ctrl+T => Stop
                if (DataContext is ViewModels.MainWindowViewModel vm)
                    vm.StopScript();
                e.Handled = true;
            }
        }
    }
}
