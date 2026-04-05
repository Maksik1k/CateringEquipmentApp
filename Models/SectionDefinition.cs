namespace CateringEquipmentApp.Models;

public sealed class SectionDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class SectionPermissions
{
    public bool CanView { get; init; }
    public bool CanAdd { get; init; }
    public bool CanEdit { get; init; }
    public bool CanAssign { get; init; }
    public bool CanDelete { get; init; }
}
