using System.Data;
using System.Windows;
using System.Windows.Controls;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp.Pages;

public partial class NotificationsPage : Page
{
    private readonly DatabaseService _databaseService = new();
    private readonly UserSession _session;

    public NotificationsPage(UserSession session)
    {
        InitializeComponent();
        _session = session;
        DataGridVisualService.AttachStatusBadges(NotificationsGrid);
    }

    public async Task RefreshAsync()
    {
        var items = await _databaseService.GetNotificationsAsync();
        CriticalCountText.Text = $"Просроченных: {items.Count(i => i.Приоритет == "Высокий")}";
        UpcomingCountText.Text = $"Ближайших: {items.Count(i => i.Приоритет != "Высокий")}";

        var table = new DataTable();
        table.Columns.Add("Категория");
        table.Columns.Add("Приоритет");
        table.Columns.Add("Оборудование");
        table.Columns.Add("Событие");
        table.Columns.Add("Дата", typeof(DateTime));
        table.Columns.Add("Описание");

        foreach (var item in items)
        {
            table.Rows.Add(item.Категория, item.Приоритет, item.Оборудование, item.Событие, item.Дата, item.Описание);
        }

        NotificationsGrid.ItemsSource = table.DefaultView;
        UpdateEmptyState(table.Rows.Count > 0);
    }

    private void UpdateEmptyState(bool hasRows)
    {
        EmptyStatePanel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
        NotificationsGrid.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
    }
}
