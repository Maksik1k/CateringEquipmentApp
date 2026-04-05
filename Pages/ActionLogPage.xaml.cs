using System.Windows.Controls;
using System.Windows;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp.Pages;

public partial class ActionLogPage : Page
{
    private readonly DatabaseService _databaseService = new();

    public ActionLogPage()
    {
        InitializeComponent();
        DataGridVisualService.AttachStatusBadges(LogGrid);
    }

    public async Task RefreshAsync()
    {
        var table = await _databaseService.GetActionLogAsync();
        LogGrid.ItemsSource = table.DefaultView;
        var hasRows = table.Rows.Count > 0;
        LogGrid.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
    }
}
