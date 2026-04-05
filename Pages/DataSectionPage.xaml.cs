using System.Data;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;
using CateringEquipmentApp.Views;

namespace CateringEquipmentApp.Pages;

public partial class DataSectionPage : Page
{
    private readonly DatabaseService _databaseService = new();
    private readonly SectionDefinition _section;
    private readonly SectionPermissions _permissions;
    private readonly UserSession _session;
    private DataView? _currentView;

    public DataSectionPage(SectionDefinition section, SectionPermissions permissions, UserSession session)
    {
        InitializeComponent();
        _section = section;
        _permissions = permissions;
        _session = session;
        DescriptionText.Text = section.Description;
        DataGridVisualService.AttachStatusBadges(RecordsGrid);
        ApplyPermissions();
    }

    public async Task RefreshAsync()
    {
        var table = await _databaseService.GetSectionDataAsync(_section.Key);
        _currentView = table.DefaultView;
        RecordsGrid.ItemsSource = _currentView;
        PopulateFilterColumns(table);
        ApplyGridFilter();
    }

    private void UpdateEmptyState()
    {
        var hasRows = _currentView is { Count: > 0 };
        RecordsGrid.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Text = _permissions.CanAdd
            ? "Добавьте первую запись через верхнюю панель действий."
            : "Записи появятся здесь, когда в системе будут доступны данные для просмотра.";
    }

    private void ApplyPermissions()
    {
        ApplyBadge(ViewBadge, _permissions.CanView);
        ApplyBadge(AddBadge, _permissions.CanAdd);
        ApplyBadge(EditBadge, _permissions.CanEdit);
        ApplyBadge(AssignBadge, _permissions.CanAssign);
        ApplyBadge(DeleteBadge, _permissions.CanDelete);

        AddButton.Visibility = _permissions.CanAdd ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = _permissions.CanEdit ? Visibility.Visible : Visibility.Collapsed;
        AssignButton.Visibility = _permissions.CanAssign && SupportsAssign(_section.Key) ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = _permissions.CanDelete ? Visibility.Visible : Visibility.Collapsed;
        CardButton.Visibility = _section.Key == "equipment" ? Visibility.Visible : Visibility.Collapsed;
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

    private static void ApplyBadge(Border badge, bool enabled) => badge.Opacity = enabled ? 1 : 0.35;

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

            await _databaseService.SaveSectionRecordAsync(_section.Key, window.Values);
            await _databaseService.LogUserActionAsync(_session, _section.Title, "Добавление", "Добавлена новая запись.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Запись добавлена", "Новая запись успешно сохранена в базе данных.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось добавить запись", ex.Message);
        }
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetSelectedRecordId(out var recordId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Редактирование записи", "Сначала выберите запись в таблице.");
                return;
            }

            var definition = await BuildFormDefinitionAsync("edit");
            var values = await _databaseService.GetRecordValuesAsync(_section.Key, recordId);
            var window = new RecordEditorWindow(definition, values) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true)
            {
                return;
            }

            await _databaseService.SaveSectionRecordAsync(_section.Key, window.Values, recordId);
            await _databaseService.LogUserActionAsync(_session, _section.Title, "Редактирование", $"Изменена запись #{recordId}.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Изменения сохранены", $"Запись #{recordId} успешно обновлена.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось изменить запись", ex.Message);
        }
    }

    private async void AssignButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetSelectedRecordId(out var recordId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Назначение", "Сначала выберите запись в таблице.");
                return;
            }

            var definition = await BuildAssignDefinitionAsync();
            var values = await _databaseService.GetRecordValuesAsync(_section.Key, recordId);
            var window = new RecordEditorWindow(definition, values) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true)
            {
                return;
            }

            await _databaseService.AssignSectionRecordAsync(_section.Key, recordId, window.Values);
            await _databaseService.LogUserActionAsync(_session, _section.Title, "Назначение", $"Назначение выполнено для записи #{recordId}.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Назначение выполнено", $"Изменения для записи #{recordId} успешно применены.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось выполнить назначение", ex.Message);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryGetSelectedRecordId(out var recordId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Удаление", "Сначала выберите запись в таблице.");
                return;
            }

            if (!DialogService.Confirm(Window.GetWindow(this),
                "Подтверждение удаления",
                "Удалить выбранную запись? Связанные записи для оборудования тоже будут удалены."))
            {
                return;
            }

            await _databaseService.DeleteSectionRecordAsync(_section.Key, recordId);
            await _databaseService.LogUserActionAsync(_session, _section.Title, "Удаление", $"Удалена запись #{recordId}.");
            await RefreshAsync();
            DialogService.ShowSuccess(Window.GetWindow(this), "Запись удалена", $"Запись #{recordId} была удалена из системы.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось удалить запись", ex.Message);
        }
    }

    private async void CardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_section.Key != "equipment")
        {
            return;
        }

        try
        {
            if (!TryGetSelectedRecordId(out var recordId))
            {
                DialogService.ShowInfo(Window.GetWindow(this), "Карточка оборудования", "Сначала выберите оборудование в таблице.");
                return;
            }

            var window = new EquipmentCardWindow(recordId) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
            await _databaseService.LogUserActionAsync(_session, _section.Title, "Просмотр карточки", $"Открыта карточка оборудования #{recordId}.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(Window.GetWindow(this), "Не удалось открыть карточку оборудования", ex.Message);
        }
    }

    private bool TryGetSelectedRecordId(out int recordId)
    {
        recordId = 0;
        if (RecordsGrid.SelectedItem is not DataRowView rowView)
        {
            return false;
        }

        var firstColumn = rowView.Row.Table.Columns[0].ColumnName;
        recordId = Convert.ToInt32(rowView.Row[firstColumn], CultureInfo.InvariantCulture);
        return true;
    }

    private async Task<FormDefinition> BuildFormDefinitionAsync(string mode)
    {
        var users = await _databaseService.GetUsersAsync();
        var equipment = await _databaseService.GetEquipmentAsync();
        var requests = await _databaseService.GetRequestsAsync();

        return _section.Key switch
        {
            "equipment" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление оборудования" : "Редактирование оборудования",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Text("Наименование", "Наименование"),
                    Text("Модель", "Модель"),
                    Combo("Тип", "Тип", StaticOptions("Тепловое", "Холодильное", "Механическое", "Моечное", "Вентиляционное")),
                    Text("ИнвентарныйНомер", "Инвентарный номер"),
                    Date("ДатаВводаВЭксплуатацию", "Дата ввода в эксплуатацию"),
                    Combo("МестоУстановки", "Место установки", StaticOptions("Горячий цех", "Холодный цех", "Мясной цех", "Овощной цех", "Моечное отделение", "Линия раздачи")),
                    Combo("ОтветственноеЛицо", "Ответственное лицо", users),
                    Combo("ТекущееСостояние", "Текущее состояние", StaticOptions("Исправно", "Требует осмотра", "На ремонте", "После санитарной обработки")),
                    Number("СрокСлужбыЛет", "Срок службы, лет")
                ]
            },
            "requests" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление заявки на ремонт" : "Редактирование заявки на ремонт",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Combo("КодОборудования", "Оборудование", equipment),
                    Date("ДатаВыявленияНеисправности", "Дата выявления неисправности"),
                    Multiline("ОписаниеПроблемы", "Описание проблемы"),
                    Combo("СтатусЗаявки", "Статус заявки", StaticOptions("Новая", "В работе", "Передана в сервис", "Закрыта")),
                    Date("ПлановаяДатаЗавершения", "Плановая дата завершения"),
                    OptionalDate("ФактическаяДатаЗавершения", "Фактическая дата завершения"),
                    Boolean("ПереданоВСервиснуюОрганизацию", "Передано в сервисную организацию"),
                    OptionalText("СервиснаяОрганизация", "Сервисная организация"),
                    Combo("Инициатор", "Инициатор", users)
                ]
            },
            "repairs" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление ремонта" : "Редактирование ремонта",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Combo("КодЗаявки", "Заявка на ремонт", requests),
                    Combo("ВидРемонта", "Вид ремонта", StaticOptions("Плановый", "Аварийный", "Капитальный", "Профилактический")),
                    Combo("Исполнитель", "Исполнитель", users),
                    Multiline("ИспользованныеМатериалыИЗапчасти", "Использованные материалы и запчасти"),
                    Text("ИтоговоеСостояниеОборудования", "Итоговое состояние оборудования"),
                    Number("Стоимость", "Стоимость")
                ]
            },
            "replacements" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление замены" : "Редактирование замены",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Combo("КодОборудования", "Оборудование", equipment),
                    Text("ПричинаЗамены", "Причина замены"),
                    Multiline("ПереченьЗамененныхЭлементов", "Перечень замененных элементов"),
                    Date("ДатаЗамены", "Дата замены"),
                    Number("Стоимость", "Стоимость"),
                    Combo("ОтветственныйСотрудник", "Ответственный сотрудник", users)
                ]
            },
            "maintenance" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление планового обслуживания" : "Редактирование планового обслуживания",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Combo("КодОборудования", "Оборудование", equipment),
                    Text("ВидОбслуживания", "Вид обслуживания"),
                    Combo("Периодичность", "Периодичность", StaticOptions("Еженедельно", "Ежемесячно", "Ежеквартально")),
                    Date("ПлановаяДата", "Плановая дата"),
                    OptionalDate("ДатаВыполнения", "Дата выполнения"),
                    Combo("Статус", "Статус", StaticOptions("Запланировано", "Выполнено", "Просрочено")),
                    Combo("Ответственный", "Ответственный", users)
                ]
            },
            "sanitation" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление санитарной обработки" : "Редактирование санитарной обработки",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Combo("КодОборудования", "Оборудование", equipment),
                    Date("ДатаПроведения", "Дата проведения"),
                    Combo("ВидОбработки", "Вид обработки", StaticOptions("Мойка", "Дезинфекция", "Обезжиривание", "Комплексная обработка")),
                    Text("ИспользуемоеСредство", "Используемое средство"),
                    Combo("Исполнитель", "Исполнитель", users),
                    Combo("ОтметкаОВыполнении", "Отметка о выполнении", StaticOptions("Выполнено", "Выполнено с замечанием", "Требуется повтор")),
                    Combo("СоблюдениеНорм", "Соблюдение норм", StaticOptions("Соблюдено", "Частично соблюдено", "Требует проверки"))
                ]
            },
            "appeals" => new FormDefinition
            {
                Title = mode == "add" ? "Добавление обращения" : "Редактирование обращения",
                SectionKey = _section.Key,
                Mode = mode,
                Fields =
                [
                    Date("ДатаОбращения", "Дата обращения"),
                    Text("Тема", "Тема"),
                    Multiline("ТекстСообщения", "Текст сообщения"),
                    Combo("Статус", "Статус", StaticOptions("Новое", "Рассматривается", "Исполнено", "Закрыто")),
                    Combo("Отправитель", "Отправитель", users),
                    Combo("ОтветственныйАдминистратор", "Ответственный администратор", users)
                ]
            },
            _ => throw new InvalidOperationException("Для этого раздела создание и редактирование не поддерживается.")
        };
    }

    private async Task<FormDefinition> BuildAssignDefinitionAsync()
    {
        var users = await _databaseService.GetUsersAsync();
        return _section.Key switch
        {
            "equipment" => new FormDefinition
            {
                Title = "Назначение ответственного лица",
                SectionKey = _section.Key,
                Mode = "assign",
                Fields = [Combo("ОтветственноеЛицо", "Ответственное лицо", users)]
            },
            "requests" => new FormDefinition
            {
                Title = "Назначение заявки",
                SectionKey = _section.Key,
                Mode = "assign",
                Fields =
                [
                    Combo("СтатусЗаявки", "Статус заявки", StaticOptions("Новая", "В работе", "Передана в сервис", "Закрыта")),
                    Boolean("ПереданоВСервиснуюОрганизацию", "Передано в сервисную организацию"),
                    OptionalText("СервиснаяОрганизация", "Сервисная организация")
                ]
            },
            "maintenance" => new FormDefinition
            {
                Title = "Назначение ответственного по обслуживанию",
                SectionKey = _section.Key,
                Mode = "assign",
                Fields =
                [
                    Combo("Ответственный", "Ответственный", users),
                    Combo("Статус", "Статус", StaticOptions("Запланировано", "Выполнено", "Просрочено"))
                ]
            },
            "appeals" => new FormDefinition
            {
                Title = "Назначение обращения",
                SectionKey = _section.Key,
                Mode = "assign",
                Fields =
                [
                    Combo("ОтветственныйАдминистратор", "Ответственный администратор", users),
                    Combo("Статус", "Статус", StaticOptions("Новое", "Рассматривается", "Исполнено", "Закрыто"))
                ]
            },
            _ => throw new InvalidOperationException("Для этого раздела назначение не поддерживается.")
        };
    }

    private static bool SupportsAssign(string sectionKey) => sectionKey is "equipment" or "requests" or "maintenance" or "appeals";

    private static FormFieldDefinition Text(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Text };
    private static FormFieldDefinition OptionalText(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Text, IsRequired = false };
    private static FormFieldDefinition Multiline(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.MultilineText };
    private static FormFieldDefinition Number(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Number };
    private static FormFieldDefinition Date(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Date };
    private static FormFieldDefinition OptionalDate(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Date, IsRequired = false };
    private static FormFieldDefinition Boolean(string key, string label) => new() { Key = key, Label = label, InputType = FieldInputType.Boolean };
    private static FormFieldDefinition Combo(string key, string label, IReadOnlyList<LookupItem> options) => new() { Key = key, Label = label, InputType = FieldInputType.Combo, Options = options };

    private static IReadOnlyList<LookupItem> StaticOptions(params string[] values) =>
        values.Select(value => new LookupItem { Value = value, DisplayName = value }).ToArray();
}
