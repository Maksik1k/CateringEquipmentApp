using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CateringEquipmentApp.Models;

namespace CateringEquipmentApp.Views;

public partial class RecordEditorWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly FormDefinition _definition;

    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RecordEditorWindow(FormDefinition definition, IDictionary<string, object?>? initialValues = null)
    {
        InitializeComponent();
        _definition = definition;
        WindowTitleText.Text = definition.Title;
        BuildFields(initialValues ?? new Dictionary<string, object?>());
    }

    private void BuildFields(IDictionary<string, object?> initialValues)
    {
        foreach (var field in _definition.Fields)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            var label = new TextBlock { Text = field.Label, Style = (Style)FindResource("EditorLabelStyle") };
            container.Children.Add(label);

            FrameworkElement control = field.InputType switch
            {
                FieldInputType.MultilineText => BuildMultilineTextBox(),
                FieldInputType.Date => BuildDatePicker(),
                FieldInputType.Number => BuildTextBox(),
                FieldInputType.Boolean => BuildBooleanCombo(),
                FieldInputType.Combo => BuildLookupCombo(field.Options),
                _ => BuildTextBox()
            };

            SetInitialValue(control, initialValues.TryGetValue(field.Key, out var value) ? value : null);
            _controls[field.Key] = control;
            container.Children.Add(control);
            FieldsPanel.Children.Add(container);
        }
    }

    private TextBox BuildTextBox()
    {
        var textBox = new TextBox();
        textBox.Style = (Style)FindResource("EditorTextBoxStyle");
        return textBox;
    }

    private TextBox BuildMultilineTextBox()
    {
        var textBox = new TextBox
        {
            MinHeight = 110,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        textBox.Style = (Style)FindResource("EditorTextBoxStyle");
        return textBox;
    }

    private DatePicker BuildDatePicker()
    {
        var datePicker = new DatePicker();
        datePicker.Style = (Style)FindResource("EditorDatePickerStyle");
        return datePicker;
    }

    private ComboBox BuildBooleanCombo()
    {
        var combo = BuildLookupCombo(
        [
            new LookupItem { Value = true, DisplayName = "Да" },
            new LookupItem { Value = false, DisplayName = "Нет" }
        ]);
        return combo;
    }

    private ComboBox BuildLookupCombo(IReadOnlyList<LookupItem>? options)
    {
        var combo = new ComboBox
        {
            DisplayMemberPath = "DisplayName",
            SelectedValuePath = "Value",
            ItemsSource = options
        };
        combo.Style = (Style)FindResource("EditorComboBoxStyle");
        return combo;
    }

    private static void SetInitialValue(FrameworkElement control, object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return;
        }

        switch (control)
        {
            case TextBox textBox:
                textBox.Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                break;
            case DatePicker datePicker when value is DateTime date:
                datePicker.SelectedDate = date;
                break;
            case DatePicker datePicker when DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsedDate):
                datePicker.SelectedDate = parsedDate;
                break;
            case ComboBox comboBox:
                comboBox.SelectedValue = value;
                break;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = string.Empty;
        Values.Clear();

        foreach (var field in _definition.Fields)
        {
            var value = ReadValue(field, _controls[field.Key]);
            if (field.IsRequired && (value is null || string.IsNullOrWhiteSpace(value.ToString())))
            {
                StatusTextBlock.Text = $"Заполните поле «{field.Label}».";
                return;
            }

            Values[field.Key] = value;
        }

        DialogResult = true;
        Close();
    }

    private static object? ReadValue(FormFieldDefinition field, FrameworkElement control)
    {
        return control switch
        {
            TextBox textBox when field.InputType == FieldInputType.Number => decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? number
                : decimal.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out var numberRu)
                    ? numberRu
                    : null,
            TextBox textBox => string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim(),
            DatePicker datePicker => datePicker.SelectedDate,
            ComboBox comboBox => comboBox.SelectedValue,
            _ => null
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
