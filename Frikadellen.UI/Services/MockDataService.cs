using System;
using System.Collections.Generic;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

public class MockDataService
{
    private static readonly Random Rng = new();

    private static readonly (string Name, string Rarity, long BasePrice)[] RichItems =
    {
        ("Hyperion", "LEGENDARY", 180_000_000),
        ("Terminator", "LEGENDARY", 620_000_000),
        ("Necron's Handle", "LEGENDARY", 280_000_000),
        ("Shadow Fury", "LEGENDARY", 90_000_000),
        ("Wither Shield", "LEGENDARY", 110_000_000),
        ("Aspect of the Dragon", "LEGENDARY", 24_000_000),
        ("Livid Dagger", "LEGENDARY", 45_000_000),
        ("Midas Sword", "LEGENDARY", 100_000_000),
        ("Spirit Sceptre", "EPIC", 22_000_000),
        ("Astraea", "LEGENDARY", 160_000_000),
        ("Scylla", "LEGENDARY", 170_000_000),
        ("Valkyrie", "LEGENDARY", 190_000_000),
        ("Stonk", "EPIC", 11_000_000),
        ("Legendary Wolf Pet", "LEGENDARY", 35_000_000),
        ("Ender Dragon Pet", "LEGENDARY", 250_000_000),
        ("Golden Dragon Pet", "LEGENDARY", 550_000_000),
        ("Bal Pet", "LEGENDARY", 75_000_000),
        ("Scatha Pet", "LEGENDARY", 120_000_000),
        ("Florid Zombie Sword", "EPIC", 18_000_000),
        ("Jujubee", "RARE", 8_000_000),
        ("Midas Staff", "LEGENDARY", 220_000_000),
        ("Giant Sword", "LEGENDARY", 310_000_000),
        ("Thick Scorpion Foil", "EPIC", 14_000_000),
        ("Leaping Sword", "RARE", 6_500_000),
        ("Frozen Blaze", "EPIC", 32_000_000),
        ("Lapis Armor Set", "UNCOMMON", 2_000_000),
        ("Glacite Armor Set", "RARE", 9_000_000),
        ("Skeleton Master", "EPIC", 12_000_000),
        ("Enchanted Book (TURBO-CROPS V)", "RARE", 5_500_000),
        ("Enchanted Book (LOOTING V)", "EPIC", 7_800_000),
        ("Strong Dragon Helmet", "EPIC", 15_000_000),
        ("Tarantula Helmet", "LEGENDARY", 48_000_000),
    };

    private static readonly string[] EventTypes = { "purchase", "sold", "bazaar", "listing", "error", "info" };
    private static readonly string[] Finders = { "SNIPER", "STONKS", "COFL", "MEDIAN", "SNIPING" };

    public static EventItem RandomEvent()
    {
        var type = EventTypes[Rng.Next(EventTypes.Length)];
        var item = RichItems[Rng.Next(RichItems.Length)];
        var price = item.BasePrice + (long)(Rng.NextDouble() * item.BasePrice * 0.2 - item.BasePrice * 0.1);
        var target = price + (long)(price * (0.03 + Rng.NextDouble() * 0.12));
        var profit = target - price;

        var avatar = type switch { "purchase" => "🛒", "sold" => "⚡", "bazaar" => "📦", "listing" => "🏷️", "error" => "🔴", _ => "🔵" };
        var typeLabel = type switch { "purchase" => "Purchase", "sold" => "Sale", "bazaar" => "Bazaar", "listing" => "Listing", "error" => "Error", _ => "Info" };
        var message = type switch
        {
            "purchase" => $"Bought {item.Name} for {Fmt.Coins(price)} (target {Fmt.Coins(target)}, +{Fmt.Coins(profit)})",
            "sold"     => $"Sold {item.Name} for {Fmt.Coins(target)} — profit: +{Fmt.Coins(profit)}",
            "bazaar"   => $"[BZ] {(Rng.Next(2) == 0 ? "BUY" : "SELL")}: {item.Name} x{Rng.Next(1, 64)} @ {Fmt.Coins(price / 64)}/unit",
            "listing"  => $"Listed {item.Name} at {Fmt.Coins(target)} (24h)",
            "error"    => "Coflnet WS disconnected — reconnecting…",
            _          => $"Script status OK — queue: {Rng.Next(0, 12)} flips pending",
        };

        return new EventItem { Type = typeLabel, Message = message, Tag = type, Avatar = avatar, Timestamp = DateTimeOffset.Now };
    }

    public static FlipRecord RandomFlip()
    {
        var item = RichItems[Rng.Next(RichItems.Length)];
        var buy = item.BasePrice + (long)(Rng.NextDouble() * item.BasePrice * 0.1 - item.BasePrice * 0.05);
        var sell = buy + (long)(buy * (0.02 + Rng.NextDouble() * 0.15));
        return new FlipRecord
        {
            ItemName = item.Name,
            BuyPrice = buy,
            SellPrice = sell,
            BuySpeedMs = Rng.Next(60, 700),
            Finder = Finders[Rng.Next(Finders.Length)],
        };
    }

    public static List<FlipRecord> GetInitialFlips()
    {
        var list = new List<FlipRecord>();
        for (int i = 0; i < 25; i++)
        {
            var item = RichItems[Rng.Next(RichItems.Length)];
            var buy = item.BasePrice + (long)(Rng.NextDouble() * item.BasePrice * 0.1 - item.BasePrice * 0.05);
            var sell = buy + (long)(buy * (0.01 + Rng.NextDouble() * 0.18));
            list.Add(new FlipRecord
            {
                ItemName = item.Name,
                BuyPrice = buy,
                SellPrice = sell,
                BuySpeedMs = Rng.Next(50, 800),
                Finder = Finders[Rng.Next(Finders.Length)],
                ItemTag = item.Rarity,
            });
        }
        return list;
    }

    public static List<double> GetProfitTimeline()
    {
        var list = new List<double>();
        double val = 0;
        for (int i = 0; i < 200; i++)
        {
            val += (Rng.NextDouble() - 0.35) * 5_000_000;
            if (val < 0) val = 0;
            list.Add(val);
        }
        return list;
    }

    public static List<double> GetHourlyEarnings()
    {
        var list = new List<double>();
        for (int h = 0; h < 24; h++)
        {
            double base_ = h >= 8 && h <= 22 ? 15_000_000 : 5_000_000;
            list.Add(base_ + Rng.NextDouble() * 20_000_000);
        }
        return list;
    }

    public static List<BazaarOrder> GetBazaarOrders()
    {
        var statuses = new[] { "ACTIVE", "FILLED", "CANCELLED" };
        var list = new List<BazaarOrder>();
        for (int i = 0; i < 20; i++)
        {
            var item = RichItems[Rng.Next(RichItems.Length)];
            list.Add(new BazaarOrder
            {
                ItemName = item.Name,
                OrderType = Rng.Next(2) == 0 ? "BUY" : "SELL",
                Price = item.BasePrice + (long)(Rng.NextDouble() * 2_000_000 - 1_000_000),
                Amount = Rng.Next(1, 160),
                Status = statuses[Rng.Next(statuses.Length)],
                PlacedAt = DateTimeOffset.Now.AddMinutes(-Rng.Next(1, 480)),
            });
        }
        return list;
    }

    public static AnalyticsData GetAnalyticsData()
    {
        var flips = GetInitialFlips();
        long total = 0;
        long best = 0;
        string bestItem = "";
        long totalBuy = 0;
        int wins = 0;
        long totalSpeed = 0;
        var itemDict = new Dictionary<string, (int cnt, long tp, long bp)>();

        foreach (var f in flips)
        {
            total += f.Profit; totalBuy += f.BuyPrice; totalSpeed += f.BuySpeedMs ?? 300;
            if (f.Profit > best) { best = f.Profit; bestItem = f.ItemName; }
            if (f.Profit > 0) wins++;
            if (!itemDict.ContainsKey(f.ItemName)) itemDict[f.ItemName] = (0, 0, 0);
            var e = itemDict[f.ItemName]; itemDict[f.ItemName] = (e.cnt + 1, e.tp + f.Profit, Math.Max(e.bp, f.Profit));
        }

        var topItems = new List<(string, int, long, long, long)>();
        foreach (var kv in itemDict) topItems.Add((kv.Key, kv.Value.cnt, kv.Value.tp, kv.Value.cnt > 0 ? kv.Value.tp / kv.Value.cnt : 0, kv.Value.bp));
        topItems.Sort((a, b) => b.Item3.CompareTo(a.Item3));

        return new AnalyticsData
        {
            TotalProfit = total,
            AvgProfitPerFlip = flips.Count > 0 ? total / flips.Count : 0,
            BestFlipItem = bestItem,
            BestFlipProfit = best,
            FlipsPerHour = 8.4 + Rng.NextDouble() * 4,
            AvgBuySpeedMs = totalSpeed / (double)flips.Count,
            WinRate = flips.Count > 0 ? (double)wins / flips.Count : 0.87,
            TopItems = topItems,
        };
    }

    public static string RandomPurse() => Fmt.Coins((long)Rng.Next(50_000_000, 2_000_000_000));
    public static int RandomQueue() => Rng.Next(0, 15);
}
