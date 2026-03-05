using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class EventsViewModel : ViewModelBase
{
    private readonly DispatcherTimer _mockTimer;
    private EventItem? _selectedEvent;
    private bool _isRunning;

    public ObservableCollection<EventItem> Events { get; } = new();

    public EventItem? SelectedEvent
    {
        get => _selectedEvent;
        set => SetField(ref _selectedEvent, value);
    }

    public ICommand SelectEventCommand { get; }
    public ICommand ClearCommand { get; }

    public EventsViewModel()
    {
        SelectEventCommand = new RelayCommand(o =>
        {
            if (o is EventItem e)
            {
                SelectedEvent = e;
                e.IsExpanded = !e.IsExpanded;
            }
        });

        ClearCommand = new RelayCommand(() =>
        {
            Events.Clear();
            SelectedEvent = null;
        });

        _mockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _mockTimer.Tick += (_, _) => AddMockEvent();
    }

    public void SetRunning(bool running)
    {
        _isRunning = running;
        if (running) _mockTimer.Start();
        else          _mockTimer.Stop();
    }

    private void AddMockEvent()
    {
        var evt = MockDataService.RandomEvent();
        Events.Insert(0, evt);
        if (Events.Count > 200) Events.RemoveAt(Events.Count - 1);
    }

    public void AddEvent(EventItem item)
    {
        Events.Insert(0, item);
        if (Events.Count > 200) Events.RemoveAt(Events.Count - 1);
    }
}
