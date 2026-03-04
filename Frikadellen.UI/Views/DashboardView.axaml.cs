using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Frikadellen.UI.ViewModels;

namespace Frikadellen.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        // Auto-scroll each ListBox when new items arrive
        // Flips are inserted at index 0 (newest first), so scroll to top
        WireAutoScroll(this.FindControl<ListBox>("FlipsList"), scrollToTop: true);
        WireAutoScroll(this.FindControl<ListBox>("EventsList"), scrollToTop: false);
        WireAutoScroll(this.FindControl<ListBox>("ChatList"), scrollToTop: false);

        // Enter key sends the chat message
        var chatInput = this.FindControl<TextBox>("ChatInput");
        if (chatInput != null)
        {
            chatInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && DataContext is DashboardViewModel vm)
                {
                    vm.SendChatCommand.Execute(null);
                    e.Handled = true;
                }
            };
        }
    }

    private static void WireAutoScroll(ListBox? list, bool scrollToTop)
    {
        if (list?.Items is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && list.ItemCount > 0)
                {
                    var idx = scrollToTop ? 0 : list.ItemCount - 1;
                    list.ScrollIntoView(list.Items[idx]!);
                }
            };
        }
    }
}
