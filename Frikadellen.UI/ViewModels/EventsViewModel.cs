using System.Collections.ObjectModel;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.ViewModels;

public sealed class EventsViewModel : ViewModelBase
{
    private EventItem? _selectedEvent;

    public EventsViewModel(ObservableCollection<EventItem> events)
    {
        Events = events;
    }

    public ObservableCollection<EventItem> Events { get; }

    public EventItem? SelectedEvent
    {
        get => _selectedEvent;
        set => SetField(ref _selectedEvent, value);
    }
}
