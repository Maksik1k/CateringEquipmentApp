using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CateringEquipmentApp.Services;

public sealed class StatusBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        return text switch
        {
            var t when t.Contains("высок") || t.Contains("просроч") || t.Contains("на ремонте") || t.Contains("требуется повтор") =>
                CreateBrush("#FDEAE7"),
            var t when t.Contains("нов") || t.Contains("в работе") || t.Contains("рассматривается") || t.Contains("средний") || t.Contains("требует осмотра") =>
                CreateBrush("#FFF4DF"),
            var t when t.Contains("выполн") || t.Contains("исправ") || t.Contains("соблюдено") || t.Contains("закрыт") || t.Contains("исполнено") =>
                CreateBrush("#EFF7E8"),
            var t when t.Contains("сервис") || t.Contains("назнач") || t.Contains("после санитарной обработки") =>
                CreateBrush("#F1ECFF"),
            _ => CreateBrush("#E8F3FA")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    private static SolidColorBrush CreateBrush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
}

public sealed class StatusBadgeForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        return text switch
        {
            var t when t.Contains("высок") || t.Contains("просроч") || t.Contains("на ремонте") || t.Contains("требуется повтор") =>
                CreateBrush("#A33B32"),
            var t when t.Contains("нов") || t.Contains("в работе") || t.Contains("рассматривается") || t.Contains("средний") || t.Contains("требует осмотра") =>
                CreateBrush("#8A5A08"),
            var t when t.Contains("выполн") || t.Contains("исправ") || t.Contains("соблюдено") || t.Contains("закрыт") || t.Contains("исполнено") =>
                CreateBrush("#3C6B28"),
            var t when t.Contains("сервис") || t.Contains("назнач") || t.Contains("после санитарной обработки") =>
                CreateBrush("#6D3CB4"),
            _ => CreateBrush("#1C5573")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    private static SolidColorBrush CreateBrush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
}
