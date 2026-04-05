using System.Data;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;
using CateringEquipmentApp.Views;

namespace CateringEquipmentApp.Pages;

public partial class UsersPage : Page
{
    private readonly DatabaseService _databaseService = new();
    private readonly UserSession _session;
    private DataView? _currentView;

    public UsersPage(UserSession session)
    {
        InitializeComponent();
        _session = session;
    }

    public async Task RefreshAsync()
    {
        var table = await _databaseService.GetUsersManagementDataAsync();
        _currentView = table.DefaultView;
        UsersGrid.ItemsSource = _currentView;
        PopulateFilterColumns(table);
        ApplyGridFilter();
    }

    private void PopulateFilterColumns(DataTable table)
    {
        var selected = ColumnFilterComboBox.SelectedItem as string;
        var items = new List<string> { "Все столбцы" };
        items.AddRange(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        ColumnFilterComboBox.ItemsSource = items;
        ColumnFilterComboBox.SelectedItem = selected is not null && items.Contains(selected) ? selected : items[0];
    }

    private void ApplyGridFilter()
    {
        if (_currentView is null)
        {
            return;
        }

        var term = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            _currentView.RowFilter = string.Empty;
            UpdateEmptyState();
            return;
        }

        var escaped = EscapeRowFilterValue(term);
        var selectedColumn = ColumnFilterComboBox.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(selectedColumn) && selectedColumn != "Все столбцы")
        {
            _currentView.RowFilter = BuildContainsExpression(selectedColumn, escaped);
            UpdateEmptyState();
            return;
        }

        if (_currentView.Table is null)
        {
            _currentView.RowFilter = string.Empty;
            UpdateEmptyState();
            return;
        }

        var expressions = _currentView.Table.Columns.Cast<DataColumn>()
            .Select(column => BuildContainsExpression(column.ColumnName, escaped));
        _currentView.RowFilter = string.Join(" OR ", expressions);
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var hasRows = _currentView is { Count: > 0 };
        UsersGrid.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string BuildContainsExpression(string columnName, string escapedValue) =>
        $"CONVERT([{columnName}], 'System.String') LIKE '%{escapedValue}%'";

    private static string EscapeRowFilterValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\'' => "''",
                '[' => "[[]",
                ']' => "[]]",
                '%' => "[%]",
                '*' => "[*]",
                _ => character
            });
        }

        return builder.ToString();
    }

    private bool TryGetSelectedUserId(out int userId)
    {
        userId = 0;
        if (UsersGrid.SelectedItem is not DataRowView rowView)
        {
            return false;
        }

        var firstColumn = rowView.Row.Table.Columns[0].ColumnName;
        userId = Convert.ToInt32(rowView.Row[firstColumn], CultureInfo.InvariantCulture);
        return true;
    }

    private async Task<FormDefinition> BuildFormDefinitionAsync(string mode)
    {
        var roles = await _databaseService.GetRolesAsync();
        return new FormDefinition
        {
            Title = mode == "add" ? "Добавление пользователя" : "Редактирование пользователя",
            SectionKey = "users",
            Mode = mode,
            Fields =
            [
                Text("Логин", "Логин"),
                Text("Пароль", "Пароль"),
                Text("ФИО", "ФИО"),
                Text("Должность", "Должность"),
                Text("Телефон", "Телефон"),
                Text("ЭлектроннаяПочта", "Электронная почта"),
                Combo("КодРоли", "Роль", roles)
            ]
        };
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var definition = await BuildFormDefinitionAsync("add");
            var window = new RecordEditorWindow(definition, null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true)
            {
                return;
            }

            await _databaseService.SaveUserAsync(window.Values);
            await _databaseService.LogUserActionAsync(_session, "Пользователи", "Добавление", "Добавлен новый пользователь системы.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Пользователь добавлен", "Новая учетная запись успешно создана.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось добавить пользователя", ex.Message);
        }
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetSelectedUserId(out var userId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Редактирование пользователя", "Сначала выберите пользователя в таблице.");
                return;
            }

            var definition = await BuildFormDefinitionAsync("edit");
            var values = await _databaseService.GetUserRecordAsync(userId);
            var window = new RecordEditorWindow(definition, values) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true)
            {
                return;
            }

            await _databaseService.SaveUserAsync(window.Values, userId);
            await _databaseService.LogUserActionAsync(_session, "Пользователи", "Редактирование", $"Обновлены данные пользователя #{userId}.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Изменения сохранены", $"Пользователь #{userId} успешно обновлен.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось изменить пользователя", ex.Message);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetSelectedUserId(out var userId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Удаление пользователя", "Сначала выберите пользователя в таблице.");
                return;
            }

            if (userId == _session.UserId)
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Удаление пользователя", "Нельзя удалить пользователя, под которым вы сейчас вошли в систему.");
                return;
            }

            if (!DialogService.Confirm(Window.GetWindow(this), "Подтверждение удаления", "Удалить выбранного пользователя из системы?"))
            {
                return;
            }

            await _databaseService.DeleteUserAsync(userId);
            await _databaseService.LogUserActionAsync(_session, "Пользователи", "Удаление", $"Удален пользователь #{userId}.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Пользователь удален", $"Учетная запись #{userId} была удалена.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось удалить пользователя", ex.Message);
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyGridFilter();

    private void ColumnFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyGridFilter();

    private void ResetFilterButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        if (ColumnFilterComboBox.Items.Count > 0)
        {
            ColumnFilterComboBox.SelectedIndex = 0;
        }
    }

    private static FormFieldDefinition Text(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Text };
    private static FormFieldDefinition Combo(string key, string label, IReadOnlyList<LookupItem> options) => new() { Key = key, Label = label, InputType = FieldInputType.Combo, Options = options };
}
