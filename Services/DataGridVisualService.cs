using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace CateringEquipmentApp.Services;

public static class DataGridVisualService
{
    private static readonly StatusBadgeBackgroundConverter BackgroundConverter = new();
    private static readonly StatusBadgeForegroundConverter ForegroundConverter = new();

    public static void AttachStatusBadges(DataGrid grid)
    {
        grid.AutoGeneratingColumn -= Grid_AutoGeneratingColumn;
        grid.AutoGeneratingColumn += Grid_AutoGeneratingColumn;
    }

    private static void Grid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (!ShouldBadge(e.Column.Header?.ToString()))
        {
            return;
        }

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
        borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        borderFactory.SetBinding(Border.BackgroundProperty, new Binding(e.PropertyName) { Converter = BackgroundConverter });

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(e.PropertyName));
        textFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(e.PropertyName) { Converter = ForegroundConverter });

        borderFactory.AppendChild(textFactory);

        e.Column = new DataGridTemplateColumn
        {
            Header = e.Column.Header,
            SortMemberPath = e.PropertyName,
            CellTemplate = new DataTemplate { VisualTree = borderFactory }
        };
    }

    private static bool ShouldBadge(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var text = header.Trim().ToLowerInvariant();
        return text.Contains("статус")
            || text.Contains("приоритет")
            || text.Contains("состояние")
            || text.Contains("отметка")
            || text.Contains("соблюдение");
    }
}
