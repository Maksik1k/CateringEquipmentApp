using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;
using Microsoft.Win32;

namespace CateringEquipmentApp.Pages;

public partial class ReportsPage : Page
{
    private readonly DatabaseService _databaseService = new();
    private readonly UserSession _session;
    private readonly IReadOnlyList<ReportDefinition> _reports;
    private DataView? _currentReportView;

    public ReportsPage(UserSession session)
    {
        InitializeComponent();
        _session = session;
        _reports = _databaseService.GetReports();
        DataGridVisualService.AttachStatusBadges(ReportGrid);
        ReportSelector.ItemsSource = _reports;
        ReportSelector.SelectedIndex = 0;
    }

    public async Task RefreshAsync()
    {
        var selected = ReportSelector.SelectedItem as ReportDefinition ?? _reports.First();
        ReportTitleText.Text = selected.Название;
        ReportDescriptionText.Text = selected.Описание;

        var table = await _databaseService.GetReportDataAsync(selected.Ключ);
        _currentReportView = table.DefaultView;
        ReportGrid.ItemsSource = _currentReportView;
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var hasRows = _currentReportView is { Count: > 0 };
        EmptyStatePanel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
        ReportGrid.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        ExportButton.IsEnabled = hasRows;
        ExportButton.Opacity = hasRows ? 1 : 0.7;
    }

    private async Task ExportCurrentReportAsync()
    {
        if (_currentReportView?.Table is null || _currentReportView.Count == 0)
        {
            DialogService.ShowInfo(Window.GetWindow(this), "Экспорт отчёта", "В выбранном отчёте пока нет данных для экспорта.");
            return;
        }

        var selected = ReportSelector.SelectedItem as ReportDefinition ?? _reports.First();
        var dialog = new SaveFileDialog
        {
            Title = "Сохранение отчёта",
            Filter = "CSV-файл|*.csv",
            DefaultExt = "csv",
            FileName = $"{MakeFileSafe(selected.Название)}_{DateTime.Now:yyyyMMdd_HHmm}"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        var csv = BuildCsv(_currentReportView.Table);
        await File.WriteAllTextAsync(dialog.FileName, csv, new UTF8Encoding(true));
        await _databaseService.LogUserActionAsync(_session, "Отчёты", "Экспорт отчёта", $"Экспортирован отчёт: {selected.Название}.");
        DialogService.ShowSuccess(Window.GetWindow(this), "Отчёт выгружен", $"Файл сохранён:\n{dialog.FileName}");
    }

    private static string BuildCsv(DataTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(column => EscapeCsv(column.ColumnName))));

        foreach (DataRow row in table.Rows)
        {
            var values = table.Columns.Cast<DataColumn>()
                .Select(column => EscapeCsv(row[column] == DBNull.Value ? string.Empty : Convert.ToString(row[column]) ?? string.Empty));
            builder.AppendLine(string.Join(";", values));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        var safe = value.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }

    private static string MakeFileSafe(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private async void ReportSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var selected = ReportSelector.SelectedItem as ReportDefinition;
        if (selected is null)
        {
            return;
        }

        await _databaseService.LogUserActionAsync(_session, "Отчёты", "Просмотр отчёта", $"Открыт отчёт: {selected.Название}.");
        await RefreshAsync();
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ExportCurrentReportAsync();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось экспортировать отчёт", ex.Message);
        }
    }
}
