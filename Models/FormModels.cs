namespace CateringEquipmentApp.Models;

public enum FieldInputType
{
    Text,
    MultilineText,
    Date,
    Number,
    Boolean,
    Combo
}

public sealed class LookupItem
{
    public int? Id { get; init; }
    public object? Value { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}

public sealed class FormFieldDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public FieldInputType InputType { get; init; }
    public bool IsRequired { get; init; } = true;
    public IReadOnlyList<LookupItem>? Options { get; init; }
}

public sealed class FormDefinition
{
    public string Title { get; init; } = string.Empty;
    public string SectionKey { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public IReadOnlyList<FormFieldDefinition> Fields { get; init; } = Array.Empty<FormFieldDefinition>();
}
