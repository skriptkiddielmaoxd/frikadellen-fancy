using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Frikadellen.UI.ViewModels;

/// <summary>Converts bool → one of two string values.</summary>
public sealed class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "Yes";
    public string FalseValue { get; set; } = "No";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == TrueValue;
}

/// <summary>Converts bool → accent colour string for running/stopped state.</summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public string TrueColor { get; set; } = "#4ADE80";
    public string FalseColor { get; set; } = "#FB7185";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Media.Color.TryParse((string?)value ?? "", out _);
}

/// <summary>Converts a nullable object → bool (true when not null). Inverts when parameter is "invert".</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var notNull = value is not null;
        return parameter?.ToString() == "invert" ? !notNull : notNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Converts IsExpanded bool → expand arrow character.</summary>
public sealed class ExpandArrowConverter : IValueConverter
{
    public static readonly ExpandArrowConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▲" : "▼";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Converts bool → 👁 / 🙈 for show/hide toggles.</summary>
public sealed class ShowHideConverter : IValueConverter
{
    public static readonly ShowHideConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "👁" : "🙈";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Converts progress (0.0–1.0) → width for the custom progress bar (max 300px).</summary>
public sealed class ProgressToWidthConverter : IValueConverter
{
    public static readonly ProgressToWidthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return Math.Max(0, Math.Min(300, d * 300));
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
