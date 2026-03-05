using System.Collections.Generic;
using System.Collections.ObjectModel;
using Frikadellen.UI.Models;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class AnalyticsViewModel : ViewModelBase
{
    private readonly AnalyticsData _data;

    public string TotalProfit      => Fmt.Coins(_data.TotalProfit);
    public string AvgProfitPerFlip => Fmt.Coins(_data.AvgProfitPerFlip);
    public string BestFlipItem     => _data.BestFlipItem;
    public string BestFlipProfit   => Fmt.Coins(_data.BestFlipProfit);
    public string FlipsPerHour     => $"{_data.FlipsPerHour:0.#}/hr";
    public string AvgBuySpeed      => $"{_data.AvgBuySpeedMs:0}ms";
    public string WinRate          => $"{_data.WinRate * 100:0.#}%";

    public List<double> ProfitTimeline { get; } = MockDataService.GetProfitTimeline();
    public List<double> HourlyEarnings { get; } = MockDataService.GetHourlyEarnings();

    public ObservableCollection<TopItemEntry> TopItems { get; } = new();

    public AnalyticsViewModel()
    {
        _data = MockDataService.GetAnalyticsData();

        int rank = 1;
        foreach (var (item, cnt, tp, avg, bp) in _data.TopItems)
        {
            TopItems.Add(new TopItemEntry
            {
                Rank = rank++,
                ItemName = item,
                TimesFlipped = cnt,
                TotalProfitLabel = Fmt.Coins(tp),
                AvgProfitLabel   = Fmt.Coins(avg),
                BestProfitLabel  = Fmt.Coins(bp),
            });
            if (rank > 10) break;
        }
    }
}
