using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

public class ToastService
{
    private static ToastService? _instance;
    public static ToastService Instance => _instance ??= new ToastService();

    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public void Show(string message, ToastType type = ToastType.Info)
    {
        var toast = new ToastItem { Message = message, Type = type };
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Toasts.Add(toast);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, _) =>
            {
                (s as DispatcherTimer)?.Stop();
                Toasts.Remove(toast);
            };
            timer.Start();
        });
    }

    public void Dismiss(ToastItem toast)
    {
        Dispatcher.UIThread.InvokeAsync(() => Toasts.Remove(toast));
    }
}
