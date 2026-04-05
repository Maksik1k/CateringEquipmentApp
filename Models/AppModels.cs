using System.Data;

namespace CateringEquipmentApp.Models;

public sealed class UserSession
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string PositionName { get; init; } = string.Empty;
}

public sealed class DashboardStats
{
    public int EquipmentCount { get; init; }
    public int OpenRequestsCount { get; init; }
    public int RepairsCount { get; init; }
    public int ReplacementsCount { get; init; }
    public int SanitationCount { get; init; }
    public int AppealsCount { get; init; }
}

public sealed class NotificationItem
{
    public string Категория { get; init; } = string.Empty;
    public string Приоритет { get; init; } = string.Empty;
    public string Оборудование { get; init; } = string.Empty;
    public string Событие { get; init; } = string.Empty;
    public DateTime Дата { get; init; }
    public string Описание { get; init; } = string.Empty;
}

public sealed class EquipmentCardData
{
    public Dictionary<string, object?> ОсновнаяИнформация { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DataTable История { get; init; } = new();
    public DataTable Заявки { get; init; } = new();
    public DataTable Ремонты { get; init; } = new();
    public DataTable Замены { get; init; } = new();
    public DataTable Обслуживание { get; init; } = new();
    public DataTable СанитарнаяОбработка { get; init; } = new();
}

public sealed class ReportDefinition
{
    public string Ключ { get; init; } = string.Empty;
    public string Название { get; init; } = string.Empty;
    public string Описание { get; init; } = string.Empty;

    public override string ToString() => Название;
}
