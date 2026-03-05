using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class BazaarViewModel : ViewModelBase
{
    public ObservableCollection<BazaarOrder> ActiveOrders { get; } = new();
    public ObservableCollection<FlipRecord>  BazaarFlips  { get; } = new();
    public List<double> AhVsBazaarData { get; } = new();
    public int CurrentHour { get; } = DateTime.Now.Hour;

    public BazaarViewModel()
    {
        foreach (var o in MockDataService.GetBazaarOrders())
            ActiveOrders.Add(o);

        foreach (var f in MockDataService.GetInitialFlips())
            BazaarFlips.Add(f);

        var rng = new Random();
        for (int i = 0; i < 24; i++)
            AhVsBazaarData.Add(5_000_000 + rng.NextDouble() * 20_000_000);
    }
}
