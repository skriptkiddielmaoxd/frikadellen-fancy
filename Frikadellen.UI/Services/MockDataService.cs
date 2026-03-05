using System;
using Frikadellen.UI.Models;

namespace Frikadellen.UI.Services;

/// <summary>
/// Generates realistic-looking mock events and flip records
/// so the UI can be demonstrated without the real Rust backend.
/// </summary>
public static class MockDataService
{
    private static readonly Random Rng = new();

    private static readonly string[] Items =
    {
        "Hyperion", "Terminator", "Jujubee", "Necron's Handle",
        "Shadow Fury", "Wither Shield", "Aspect of the Dragon",
        "Florid Zombie Sword", "Livid Dagger", "Midas Sword",
        "Spirit Sceptre", "Astraea", "Scylla", "Valkyrie",
        "Stonk", "Legendary Wolf Pet", "Ender Dragon Pet",
        "Golden Dragon Pet", "Bal Pet", "Scatha Pet",
    };

    private static readonly string[] EventTypes =
    {
        "purchase", "sold", "bazaar", "listing", "error", "info",
    };

    public static EventItem RandomEvent()
    {
        var type = EventTypes[Rng.Next(EventTypes.Length)];
        var item = Items[Rng.Next(Items.Length)];
        var price = (long)Rng.Next(1_000_000, 500_000_000);
        var target = price + (long)Rng.Next(100_000, 20_000_000);
        var profit = target - price;

        var avatar = type switch
        {
            "purchase" => "🛒",
            "sold"     => "⚡",
            "bazaar"   => "📦",
            "listing"  => "🏷️",
            "error"    => "🔴",
            _          => "🔵",
        };

        var typeLabel = type switch
        {
            "purchase" => "Purchase",
            "sold"     => "Sale",
            "bazaar"   => "Bazaar",
            "listing"  => "Listing",
            "error"    => "Error",
            _          => "Info",
        };

        var message = type switch
        {
            "purchase" => $"Bought {item} for {Fmt.Coins(price)} (target {Fmt.Coins(target)}, +{Fmt.Coins(profit)})",
            "sold"     => $"Sold {item} for {Fmt.Coins(target)} — profit: +{Fmt.Coins(profit)}",
            "bazaar"   => $"[BZ] {(Rng.Next(2) == 0 ? "BUY" : "SELL")}: {item} x{Rng.Next(1, 64)} @ {Fmt.Coins(price / 64)}/unit",
            "listing"  => $"Listed {item} at {Fmt.Coins(target)} (24h)",
            "error"    => "Coflnet WS disconnected — reconnecting…",
            _          => $"Script status OK — queue: {Rng.Next(0, 12)} flips pending",
        };

        return new EventItem
        {
            Type = typeLabel,
            Message = message,
            Tag = type,
            Avatar = avatar,
            Timestamp = DateTimeOffset.Now,
        };
    }

    public static FlipRecord RandomFlip()
    {
        var item = Items[Rng.Next(Items.Length)];
        var buy = (long)Rng.Next(5_000_000, 300_000_000);
        var sell = buy + (long)Rng.Next(500_000, 30_000_000);
        return new FlipRecord
        {
            ItemName = item,
            BuyPrice = buy,
            SellPrice = sell,
            BuySpeedMs = Rng.Next(60, 700),
            Finder = Rng.Next(2) == 0 ? "SNIPER" : "STONKS",
        };
    }

    public static string RandomPurse() =>
        Fmt.Coins((long)Rng.Next(50_000_000, 2_000_000_000));

    public static int RandomQueue() => Rng.Next(0, 15);
}
