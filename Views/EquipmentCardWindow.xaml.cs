using System.Data;
using System.Reflection;
using System.Windows;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp.Views;

public partial class EquipmentCardWindow : Window
{
    private readonly DatabaseService _databaseService = new();
    private readonly int _equipmentId;

    public EquipmentCardWindow(int equipmentId)
    {
        InitializeComponent();
        _equipmentId = equipmentId;

        DataGridVisualService.AttachStatusBadges(HistoryGrid);
        DataGridVisualService.AttachStatusBadges(RequestsGrid);
        DataGridVisualService.AttachStatusBadges(RepairsGrid);
        DataGridVisualService.AttachStatusBadges(ReplacementsGrid);
        DataGridVisualService.AttachStatusBadges(MaintenanceGrid);
        DataGridVisualService.AttachStatusBadges(SanitationGrid);

        Loaded += EquipmentCardWindow_Loaded;
    }

    private async void EquipmentCardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var card = await _databaseService.GetEquipmentCardAsync(_equipmentId);

        var info = ReadMainInfo(card);
        var tables = ReadTables(card);

        TitleText.Text = GetText(info, 1, "Карточка оборудования");
        ModelText.Text = GetText(info, 2);
        TypeText.Text = GetText(info, 3);
        InventoryText.Text = GetText(info, 4);
        LocationText.Text = GetText(info, 6);
        ResponsibleText.Text = GetText(info, 7);
        StateText.Text = GetText(info, 8);

        var history = tables.ElementAtOrDefault(0) ?? new DataTable();
        var requests = tables.ElementAtOrDefault(1) ?? new DataTable();
        var repairs = tables.ElementAtOrDefault(2) ?? new DataTable();
        var replacements = tables.ElementAtOrDefault(3) ?? new DataTable();
        var maintenance = tables.ElementAtOrDefault(4) ?? new DataTable();
        var sanitation = tables.ElementAtOrDefault(5) ?? new DataTable();

        OpenRequestsText.Text = CountOpenRequests(requests).ToString();
        ServiceCostText.Text = $"{SumLastNumericColumn(repairs) + SumLastNumericColumn(replacements):0.##} ₽";

        HistoryGrid.ItemsSource = history.DefaultView;
        RequestsGrid.ItemsSource = requests.DefaultView;
        RepairsGrid.ItemsSource = repairs.DefaultView;
        ReplacementsGrid.ItemsSource = replacements.DefaultView;
        MaintenanceGrid.ItemsSource = maintenance.DefaultView;
        SanitationGrid.ItemsSource = sanitation.DefaultView;
    }

    private static List<object?> ReadMainInfo(EquipmentCardData card)
    {
        var dictionaryProperty = typeof(EquipmentCardData)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property => typeof(IDictionary<string, object?>).IsAssignableFrom(property.PropertyType));

        var dictionary = dictionaryProperty?.GetValue(card) as IDictionary<string, object?>;
        return dictionary?.Values.ToList() ?? new List<object?>();
    }

    private static List<DataTable> ReadTables(EquipmentCardData card)
    {
        return typeof(EquipmentCardData)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(DataTable))
            .Select(property => property.GetValue(card) as DataTable ?? new DataTable())
            .ToList();
    }

    private static string GetText(IReadOnlyList<object?> values, int index, string fallback = "Нет данных")
    {
        if (index < 0 || index >= values.Count)
        {
            return fallback;
        }

        var value = Convert.ToString(values[index]);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int CountOpenRequests(DataTable table)
    {
        if (table.Columns.Count == 0)
        {
            return 0;
        }

        var statusColumnIndex = Math.Max(0, table.Columns.Count - 1);

        return table.AsEnumerable()
            .Count(row =>
            {
                var status = Convert.ToString(row[statusColumnIndex]);
                return !string.Equals(status, "Закрыта", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static decimal SumLastNumericColumn(DataTable table)
    {
        if (table.Columns.Count == 0)
        {
            return 0m;
        }

        var lastColumnIndex = table.Columns.Count - 1;
        decimal total = 0m;

        foreach (var row in table.AsEnumerable())
        {
            total += Convert.ToDecimal(row[lastColumnIndex] is DBNull ? 0m : row[lastColumnIndex]);
        }

        return total;
    }
}
