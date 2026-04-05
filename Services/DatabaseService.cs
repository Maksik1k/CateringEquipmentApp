using System.Data;
using System.IO;
using System.Text.Json;
using CateringEquipmentApp.Models;
using Microsoft.Data.SqlClient;

namespace CateringEquipmentApp.Services;

public sealed partial class DatabaseService
{
    private static readonly Lazy<string> ConnectionStringValue = new(LoadConnectionString);

    private static string ConnectionString => ConnectionStringValue.Value;

    private static string LoadConnectionString()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException(
                $"Не найден файл конфигурации appsettings.json рядом с программой: {configPath}");
        }

        using var stream = File.OpenRead(configPath);
        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
            connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection) &&
            !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
        {
            return defaultConnection.GetString()!;
        }

        throw new InvalidOperationException(
            "В appsettings.json не найдена строка подключения ConnectionStrings:DefaultConnection.");
    }

    public async Task EnsureInfrastructureAsync()
    {
        const string sql = """
            IF OBJECT_ID(N'[ЖурналДействийПользователей]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ЖурналДействийПользователей]
                (
                    [КодДействия] INT IDENTITY(1,1) PRIMARY KEY,
                    [ДатаДействия] DATETIME2 NOT NULL CONSTRAINT [DF_ЖурналДействий_Дата] DEFAULT SYSDATETIME(),
                    [КодПользователя] INT NULL,
                    [ФИОПользователя] NVARCHAR(120) NOT NULL,
                    [РольПользователя] NVARCHAR(100) NOT NULL,
                    [Раздел] NVARCHAR(120) NOT NULL,
                    [Операция] NVARCHAR(120) NOT NULL,
                    [Описание] NVARCHAR(400) NOT NULL,
                    CONSTRAINT [FK_ЖурналДействий_Пользователи] FOREIGN KEY ([КодПользователя]) REFERENCES [Пользователи]([КодПользователя])
                );
            END;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<UserSession?> AuthenticateAsync(string login, string password)
    {
        const string sql = """
            SELECT TOP 1
                p.[КодПользователя],
                p.[ФИО],
                p.[Должность],
                r.[НазваниеРоли]
            FROM [Пользователи] p
            INNER JOIN [Роли] r ON r.[КодРоли] = p.[КодРоли]
            WHERE p.[Логин] = @login AND p.[Пароль] = @password;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@login", login);
        command.Parameters.AddWithValue("@password", password);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new UserSession
        {
            UserId = reader.GetInt32(0),
            FullName = reader.GetString(1),
            PositionName = reader.GetString(2),
            RoleName = reader.GetString(3)
        };
    }

    public async Task LogUserActionAsync(UserSession session, string section, string operation, string description)
    {
        const string sql = """
            INSERT INTO [ЖурналДействийПользователей]
            ([КодПользователя], [ФИОПользователя], [РольПользователя], [Раздел], [Операция], [Описание])
            VALUES (@userId, @fullName, @roleName, @section, @operation, @description);
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", session.UserId);
        command.Parameters.AddWithValue("@fullName", session.FullName);
        command.Parameters.AddWithValue("@roleName", session.RoleName);
        command.Parameters.AddWithValue("@section", section);
        command.Parameters.AddWithValue("@operation", operation);
        command.Parameters.AddWithValue("@description", description);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<DataTable> GetSectionDataAsync(string sectionKey)
    {
        var sql = sectionKey switch
        {
            "users" => """
                SELECT
                    p.[КодПользователя] AS [Код],
                    p.[ФИО] AS [ФИО],
                    p.[Должность] AS [Должность],
                    r.[НазваниеРоли] AS [Роль],
                    p.[Логин] AS [Логин],
                    p.[Телефон] AS [Телефон],
                    p.[ЭлектроннаяПочта] AS [Электронная почта]
                FROM [Пользователи] p
                INNER JOIN [Роли] r ON r.[КодРоли] = p.[КодРоли]
                ORDER BY p.[ФИО];
                """,
            "equipment" => """
                SELECT
                    o.[КодОборудования] AS [Код],
                    o.[Наименование] AS [Наименование],
                    o.[Модель] AS [Модель],
                    o.[Тип] AS [Тип],
                    o.[ИнвентарныйНомер] AS [Инвентарный номер],
                    o.[ДатаВводаВЭксплуатацию] AS [Дата ввода],
                    o.[МестоУстановки] AS [Место установки],
                    p.[ФИО] AS [Ответственное лицо],
                    o.[ТекущееСостояние] AS [Текущее состояние],
                    o.[СрокСлужбыЛет] AS [Срок службы, лет]
                FROM [Оборудование] o
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = o.[ОтветственноеЛицо]
                ORDER BY o.[КодОборудования];
                """,
            "history" => """
                SELECT
                    h.[КодЗаписиИстории] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    h.[ДатаСобытия] AS [Дата события],
                    h.[Событие] AS [Событие],
                    h.[Описание] AS [Описание],
                    p.[ФИО] AS [Ответственный]
                FROM [ИсторияЭксплуатации] h
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = h.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = h.[Ответственный]
                ORDER BY h.[ДатаСобытия] DESC, h.[КодЗаписиИстории] DESC;
                """,
            "requests" => """
                SELECT
                    z.[КодЗаявки] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    z.[ДатаВыявленияНеисправности] AS [Дата выявления],
                    z.[ОписаниеПроблемы] AS [Описание проблемы],
                    z.[СтатусЗаявки] AS [Статус],
                    z.[ПлановаяДатаЗавершения] AS [Плановая дата],
                    z.[ФактическаяДатаЗавершения] AS [Фактическая дата],
                    CASE WHEN z.[ПереданоВСервиснуюОрганизацию] = 1 THEN N'Да' ELSE N'Нет' END AS [Передано в сервис],
                    ISNULL(z.[СервиснаяОрганизация], N'Не указана') AS [Сервисная организация],
                    p.[ФИО] AS [Инициатор]
                FROM [ЗаявкиНаРемонт] z
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = z.[Инициатор]
                ORDER BY z.[КодЗаявки] DESC;
                """,
            "repairs" => """
                SELECT
                    r.[КодРемонта] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    z.[ДатаВыявленияНеисправности] AS [Дата выявления неисправности],
                    r.[ВидРемонта] AS [Вид ремонта],
                    p.[ФИО] AS [Исполнитель],
                    r.[ИспользованныеМатериалыИЗапчасти] AS [Материалы и запчасти],
                    r.[ИтоговоеСостояниеОборудования] AS [Итоговое состояние],
                    r.[Стоимость] AS [Стоимость]
                FROM [Ремонты] r
                INNER JOIN [ЗаявкиНаРемонт] z ON z.[КодЗаявки] = r.[КодЗаявки]
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = r.[Исполнитель]
                ORDER BY r.[КодРемонта] DESC;
                """,
            "replacements" => """
                SELECT
                    z.[КодЗамены] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    z.[ПричинаЗамены] AS [Причина замены],
                    z.[ПереченьЗамененныхЭлементов] AS [Замененные элементы],
                    z.[ДатаЗамены] AS [Дата замены],
                    z.[Стоимость] AS [Стоимость],
                    p.[ФИО] AS [Ответственный сотрудник]
                FROM [Замены] z
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = z.[ОтветственныйСотрудник]
                ORDER BY z.[КодЗамены] DESC;
                """,
            "maintenance" => """
                SELECT
                    p.[КодПлана] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    p.[ВидОбслуживания] AS [Вид обслуживания],
                    p.[Периодичность] AS [Периодичность],
                    p.[ПлановаяДата] AS [Плановая дата],
                    p.[ДатаВыполнения] AS [Дата выполнения],
                    p.[Статус] AS [Статус],
                    u.[ФИО] AS [Ответственный]
                FROM [ПлановоеОбслуживание] p
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = p.[КодОборудования]
                INNER JOIN [Пользователи] u ON u.[КодПользователя] = p.[Ответственный]
                ORDER BY p.[ПлановаяДата];
                """,
            "sanitation" => """
                SELECT
                    s.[КодОбработки] AS [Код],
                    o.[Наименование] AS [Оборудование],
                    s.[ДатаПроведения] AS [Дата проведения],
                    s.[ВидОбработки] AS [Вид обработки],
                    s.[ИспользуемоеСредство] AS [Используемое средство],
                    p.[ФИО] AS [Исполнитель],
                    s.[ОтметкаОВыполнении] AS [Отметка о выполнении],
                    s.[СоблюдениеНорм] AS [Соблюдение норм]
                FROM [СанитарныеОбработки] s
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = s.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = s.[Исполнитель]
                ORDER BY s.[ДатаПроведения] DESC;
                """,
            "appeals" => """
                SELECT
                    a.[КодОбращения] AS [Код],
                    a.[ДатаОбращения] AS [Дата обращения],
                    a.[Тема] AS [Тема],
                    a.[ТекстСообщения] AS [Сообщение],
                    a.[Статус] AS [Статус],
                    sender.[ФИО] AS [Отправитель],
                    admin.[ФИО] AS [Ответственный администратор]
                FROM [ОбращенияКАдминистрации] a
                INNER JOIN [Пользователи] sender ON sender.[КодПользователя] = a.[Отправитель]
                INNER JOIN [Пользователи] admin ON admin.[КодПользователя] = a.[ОтветственныйАдминистратор]
                ORDER BY a.[КодОбращения] DESC;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(sectionKey), sectionKey, "Неизвестный раздел.")
        };

        return await ExecuteDataTableAsync(sql);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM [Оборудование]) AS EquipmentCount,
                (SELECT COUNT(*) FROM [ЗаявкиНаРемонт] WHERE [СтатусЗаявки] <> N'Закрыта') AS OpenRequestsCount,
                (SELECT COUNT(*) FROM [Ремонты]) AS RepairsCount,
                (SELECT COUNT(*) FROM [Замены]) AS ReplacementsCount,
                (SELECT COUNT(*) FROM [СанитарныеОбработки]) AS SanitationCount,
                (SELECT COUNT(*) FROM [ОбращенияКАдминистрации]) AS AppealsCount;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return new DashboardStats
        {
            EquipmentCount = reader.GetInt32(0),
            OpenRequestsCount = reader.GetInt32(1),
            RepairsCount = reader.GetInt32(2),
            ReplacementsCount = reader.GetInt32(3),
            SanitationCount = reader.GetInt32(4),
            AppealsCount = reader.GetInt32(5)
        };
    }

    public async Task<List<LookupItem>> GetUsersAsync() =>
        await ReadLookupAsync("""
            SELECT [КодПользователя], [ФИО]
            FROM [Пользователи]
            ORDER BY [ФИО];
            """);

    public async Task<List<LookupItem>> GetRolesAsync() =>
        await ReadLookupAsync("""
            SELECT [КодРоли], [НазваниеРоли]
            FROM [Роли]
            ORDER BY [НазваниеРоли];
            """);

    public async Task<List<LookupItem>> GetEquipmentAsync() =>
        await ReadLookupAsync("""
            SELECT [КодОборудования], [Наименование]
            FROM [Оборудование]
            ORDER BY [Наименование];
            """);

    public async Task<List<LookupItem>> GetRequestsAsync() =>
        await ReadLookupAsync("""
            SELECT z.[КодЗаявки], o.[Наименование] + N' / заявка ' + CAST(z.[КодЗаявки] AS nvarchar(20))
            FROM [ЗаявкиНаРемонт] z
            INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
            ORDER BY z.[КодЗаявки] DESC;
            """);

    public async Task<Dictionary<string, object?>> GetRecordValuesAsync(string sectionKey, int recordId)
    {
        var sql = sectionKey switch
        {
            "equipment" => "SELECT * FROM [Оборудование] WHERE [КодОборудования] = @id",
            "requests" => "SELECT * FROM [ЗаявкиНаРемонт] WHERE [КодЗаявки] = @id",
            "repairs" => "SELECT * FROM [Ремонты] WHERE [КодРемонта] = @id",
            "replacements" => "SELECT * FROM [Замены] WHERE [КодЗамены] = @id",
            "maintenance" => "SELECT * FROM [ПлановоеОбслуживание] WHERE [КодПлана] = @id",
            "sanitation" => "SELECT * FROM [СанитарныеОбработки] WHERE [КодОбработки] = @id",
            "appeals" => "SELECT * FROM [ОбращенияКАдминистрации] WHERE [КодОбращения] = @id",
            _ => throw new ArgumentOutOfRangeException(nameof(sectionKey))
        };

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", recordId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Запись не найдена.");
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return result;
    }

    public async Task SaveSectionRecordAsync(string sectionKey, Dictionary<string, object?> values, int? recordId = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        switch (sectionKey)
        {
            case "equipment":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [Оборудование]
                        ([Наименование],[Модель],[Тип],[ИнвентарныйНомер],[ДатаВводаВЭксплуатацию],[МестоУстановки],[ОтветственноеЛицо],[ТекущееСостояние],[СрокСлужбыЛет])
                        VALUES (@name,@model,@type,@inventory,@startDate,@location,@responsible,@state,@serviceYears);
                        """
                    : """
                        UPDATE [Оборудование]
                        SET [Наименование]=@name,[Модель]=@model,[Тип]=@type,[ИнвентарныйНомер]=@inventory,[ДатаВводаВЭксплуатацию]=@startDate,
                            [МестоУстановки]=@location,[ОтветственноеЛицо]=@responsible,[ТекущееСостояние]=@state,[СрокСлужбыЛет]=@serviceYears
                        WHERE [КодОборудования]=@id;
                        """;
                Add(command, "@name", values["Наименование"]);
                Add(command, "@model", values["Модель"]);
                Add(command, "@type", values["Тип"]);
                Add(command, "@inventory", values["ИнвентарныйНомер"]);
                Add(command, "@startDate", values["ДатаВводаВЭксплуатацию"]);
                Add(command, "@location", values["МестоУстановки"]);
                Add(command, "@responsible", values["ОтветственноеЛицо"]);
                Add(command, "@state", values["ТекущееСостояние"]);
                Add(command, "@serviceYears", values["СрокСлужбыЛет"]);
                break;

            case "requests":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [ЗаявкиНаРемонт]
                        ([КодОборудования],[ДатаВыявленияНеисправности],[ОписаниеПроблемы],[СтатусЗаявки],[ПлановаяДатаЗавершения],[ФактическаяДатаЗавершения],[ПереданоВСервиснуюОрганизацию],[СервиснаяОрганизация],[Инициатор])
                        VALUES (@equipmentId,@foundDate,@problem,@status,@plannedDate,@actualDate,@sentToService,@serviceOrg,@initiator);
                        """
                    : """
                        UPDATE [ЗаявкиНаРемонт]
                        SET [КодОборудования]=@equipmentId,[ДатаВыявленияНеисправности]=@foundDate,[ОписаниеПроблемы]=@problem,[СтатусЗаявки]=@status,
                            [ПлановаяДатаЗавершения]=@plannedDate,[ФактическаяДатаЗавершения]=@actualDate,[ПереданоВСервиснуюОрганизацию]=@sentToService,
                            [СервиснаяОрганизация]=@serviceOrg,[Инициатор]=@initiator
                        WHERE [КодЗаявки]=@id;
                        """;
                Add(command, "@equipmentId", values["КодОборудования"]);
                Add(command, "@foundDate", values["ДатаВыявленияНеисправности"]);
                Add(command, "@problem", values["ОписаниеПроблемы"]);
                Add(command, "@status", values["СтатусЗаявки"]);
                Add(command, "@plannedDate", values["ПлановаяДатаЗавершения"]);
                Add(command, "@actualDate", values["ФактическаяДатаЗавершения"]);
                Add(command, "@sentToService", values["ПереданоВСервиснуюОрганизацию"]);
                Add(command, "@serviceOrg", values["СервиснаяОрганизация"]);
                Add(command, "@initiator", values["Инициатор"]);
                break;

            case "repairs":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [Ремонты]
                        ([КодЗаявки],[ВидРемонта],[Исполнитель],[ИспользованныеМатериалыИЗапчасти],[ИтоговоеСостояниеОборудования],[Стоимость])
                        VALUES (@requestId,@repairType,@executor,@materials,@resultState,@cost);
                        """
                    : """
                        UPDATE [Ремонты]
                        SET [КодЗаявки]=@requestId,[ВидРемонта]=@repairType,[Исполнитель]=@executor,
                            [ИспользованныеМатериалыИЗапчасти]=@materials,[ИтоговоеСостояниеОборудования]=@resultState,[Стоимость]=@cost
                        WHERE [КодРемонта]=@id;
                        """;
                Add(command, "@requestId", values["КодЗаявки"]);
                Add(command, "@repairType", values["ВидРемонта"]);
                Add(command, "@executor", values["Исполнитель"]);
                Add(command, "@materials", values["ИспользованныеМатериалыИЗапчасти"]);
                Add(command, "@resultState", values["ИтоговоеСостояниеОборудования"]);
                Add(command, "@cost", values["Стоимость"]);
                break;

            case "replacements":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [Замены]
                        ([КодОборудования],[ПричинаЗамены],[ПереченьЗамененныхЭлементов],[ДатаЗамены],[Стоимость],[ОтветственныйСотрудник])
                        VALUES (@equipmentId,@reason,@elements,@replaceDate,@cost,@employeeId);
                        """
                    : """
                        UPDATE [Замены]
                        SET [КодОборудования]=@equipmentId,[ПричинаЗамены]=@reason,[ПереченьЗамененныхЭлементов]=@elements,
                            [ДатаЗамены]=@replaceDate,[Стоимость]=@cost,[ОтветственныйСотрудник]=@employeeId
                        WHERE [КодЗамены]=@id;
                        """;
                Add(command, "@equipmentId", values["КодОборудования"]);
                Add(command, "@reason", values["ПричинаЗамены"]);
                Add(command, "@elements", values["ПереченьЗамененныхЭлементов"]);
                Add(command, "@replaceDate", values["ДатаЗамены"]);
                Add(command, "@cost", values["Стоимость"]);
                Add(command, "@employeeId", values["ОтветственныйСотрудник"]);
                break;

            case "maintenance":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [ПлановоеОбслуживание]
                        ([КодОборудования],[ВидОбслуживания],[Периодичность],[ПлановаяДата],[ДатаВыполнения],[Статус],[Ответственный])
                        VALUES (@equipmentId,@maintenanceType,@frequency,@plannedDate,@doneDate,@status,@responsible);
                        """
                    : """
                        UPDATE [ПлановоеОбслуживание]
                        SET [КодОборудования]=@equipmentId,[ВидОбслуживания]=@maintenanceType,[Периодичность]=@frequency,
                            [ПлановаяДата]=@plannedDate,[ДатаВыполнения]=@doneDate,[Статус]=@status,[Ответственный]=@responsible
                        WHERE [КодПлана]=@id;
                        """;
                Add(command, "@equipmentId", values["КодОборудования"]);
                Add(command, "@maintenanceType", values["ВидОбслуживания"]);
                Add(command, "@frequency", values["Периодичность"]);
                Add(command, "@plannedDate", values["ПлановаяДата"]);
                Add(command, "@doneDate", values["ДатаВыполнения"]);
                Add(command, "@status", values["Статус"]);
                Add(command, "@responsible", values["Ответственный"]);
                break;

            case "sanitation":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [СанитарныеОбработки]
                        ([КодОборудования],[ДатаПроведения],[ВидОбработки],[ИспользуемоеСредство],[Исполнитель],[ОтметкаОВыполнении],[СоблюдениеНорм])
                        VALUES (@equipmentId,@date,@type,@product,@executor,@mark,@norms);
                        """
                    : """
                        UPDATE [СанитарныеОбработки]
                        SET [КодОборудования]=@equipmentId,[ДатаПроведения]=@date,[ВидОбработки]=@type,[ИспользуемоеСредство]=@product,
                            [Исполнитель]=@executor,[ОтметкаОВыполнении]=@mark,[СоблюдениеНорм]=@norms
                        WHERE [КодОбработки]=@id;
                        """;
                Add(command, "@equipmentId", values["КодОборудования"]);
                Add(command, "@date", values["ДатаПроведения"]);
                Add(command, "@type", values["ВидОбработки"]);
                Add(command, "@product", values["ИспользуемоеСредство"]);
                Add(command, "@executor", values["Исполнитель"]);
                Add(command, "@mark", values["ОтметкаОВыполнении"]);
                Add(command, "@norms", values["СоблюдениеНорм"]);
                break;

            case "appeals":
                command.CommandText = recordId is null
                    ? """
                        INSERT INTO [ОбращенияКАдминистрации]
                        ([ДатаОбращения],[Тема],[ТекстСообщения],[Статус],[Отправитель],[ОтветственныйАдминистратор])
                        VALUES (@date,@topic,@message,@status,@sender,@admin);
                        """
                    : """
                        UPDATE [ОбращенияКАдминистрации]
                        SET [ДатаОбращения]=@date,[Тема]=@topic,[ТекстСообщения]=@message,[Статус]=@status,[Отправитель]=@sender,[ОтветственныйАдминистратор]=@admin
                        WHERE [КодОбращения]=@id;
                        """;
                Add(command, "@date", values["ДатаОбращения"]);
                Add(command, "@topic", values["Тема"]);
                Add(command, "@message", values["ТекстСообщения"]);
                Add(command, "@status", values["Статус"]);
                Add(command, "@sender", values["Отправитель"]);
                Add(command, "@admin", values["ОтветственныйАдминистратор"]);
                break;

            default:
                throw new InvalidOperationException("Для этого раздела сохранение не поддерживается.");
        }

        if (recordId is not null)
        {
            command.Parameters.AddWithValue("@id", recordId.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task AssignSectionRecordAsync(string sectionKey, int recordId, Dictionary<string, object?> values)
    {
        var sql = sectionKey switch
        {
            "equipment" => "UPDATE [Оборудование] SET [ОтветственноеЛицо] = @value1 WHERE [КодОборудования] = @id;",
            "requests" => """
                UPDATE [ЗаявкиНаРемонт]
                SET [СтатусЗаявки] = @value1,
                    [ПереданоВСервиснуюОрганизацию] = @value2,
                    [СервиснаяОрганизация] = @value3
                WHERE [КодЗаявки] = @id;
                """,
            "maintenance" => """
                UPDATE [ПлановоеОбслуживание]
                SET [Ответственный] = @value1,
                    [Статус] = @value2
                WHERE [КодПлана] = @id;
                """,
            "appeals" => """
                UPDATE [ОбращенияКАдминистрации]
                SET [ОтветственныйАдминистратор] = @value1,
                    [Статус] = @value2
                WHERE [КодОбращения] = @id;
                """,
            _ => throw new InvalidOperationException("Для этого раздела назначение не поддерживается.")
        };

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", recordId);

        switch (sectionKey)
        {
            case "equipment":
                Add(command, "@value1", values["ОтветственноеЛицо"]);
                break;
            case "requests":
                Add(command, "@value1", values["СтатусЗаявки"]);
                Add(command, "@value2", values["ПереданоВСервиснуюОрганизацию"]);
                Add(command, "@value3", values["СервиснаяОрганизация"]);
                break;
            case "maintenance":
                Add(command, "@value1", values["Ответственный"]);
                Add(command, "@value2", values["Статус"]);
                break;
            case "appeals":
                Add(command, "@value1", values["ОтветственныйАдминистратор"]);
                Add(command, "@value2", values["Статус"]);
                break;
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteSectionRecordAsync(string sectionKey, int recordId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            switch (sectionKey)
            {
                case "equipment":
                    command.CommandText = """
                        DELETE r
                        FROM [Ремонты] r
                        INNER JOIN [ЗаявкиНаРемонт] z ON z.[КодЗаявки] = r.[КодЗаявки]
                        WHERE z.[КодОборудования] = @id;

                        DELETE FROM [ЗаявкиНаРемонт] WHERE [КодОборудования] = @id;
                        DELETE FROM [ИсторияЭксплуатации] WHERE [КодОборудования] = @id;
                        DELETE FROM [Замены] WHERE [КодОборудования] = @id;
                        DELETE FROM [ПлановоеОбслуживание] WHERE [КодОборудования] = @id;
                        DELETE FROM [СанитарныеОбработки] WHERE [КодОборудования] = @id;
                        DELETE FROM [Оборудование] WHERE [КодОборудования] = @id;
                        """;
                    break;
                case "requests":
                    command.CommandText = """
                        DELETE FROM [Ремонты] WHERE [КодЗаявки] = @id;
                        DELETE FROM [ЗаявкиНаРемонт] WHERE [КодЗаявки] = @id;
                        """;
                    break;
                case "repairs":
                    command.CommandText = "DELETE FROM [Ремонты] WHERE [КодРемонта] = @id;";
                    break;
                case "replacements":
                    command.CommandText = "DELETE FROM [Замены] WHERE [КодЗамены] = @id;";
                    break;
                case "maintenance":
                    command.CommandText = "DELETE FROM [ПлановоеОбслуживание] WHERE [КодПлана] = @id;";
                    break;
                case "sanitation":
                    command.CommandText = "DELETE FROM [СанитарныеОбработки] WHERE [КодОбработки] = @id;";
                    break;
                case "appeals":
                    command.CommandText = "DELETE FROM [ОбращенияКАдминистрации] WHERE [КодОбращения] = @id;";
                    break;
                default:
                    throw new InvalidOperationException("Для этого раздела удаление не поддерживается.");
            }

            command.Parameters.AddWithValue("@id", recordId);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<EquipmentCardData> GetEquipmentCardAsync(int equipmentId)
    {
        var info = await ReadSingleRecordAsync("""
            SELECT
                o.[КодОборудования],
                o.[Наименование],
                o.[Модель],
                o.[Тип],
                o.[ИнвентарныйНомер],
                o.[ДатаВводаВЭксплуатацию],
                o.[МестоУстановки],
                p.[ФИО] AS [ОтветственноеЛицоФИО],
                o.[ТекущееСостояние],
                o.[СрокСлужбыЛет]
            FROM [Оборудование] o
            INNER JOIN [Пользователи] p ON p.[КодПользователя] = o.[ОтветственноеЛицо]
            WHERE o.[КодОборудования] = @id;
            """, equipmentId);

        return new EquipmentCardData
        {
            ОсновнаяИнформация = info,
            История = await ExecuteDataTableAsync("""
                SELECT [ДатаСобытия] AS [Дата], [Событие], [Описание]
                FROM [ИсторияЭксплуатации]
                WHERE [КодОборудования] = @id
                ORDER BY [ДатаСобытия] DESC;
                """, equipmentId),
            Заявки = await ExecuteDataTableAsync("""
                SELECT [КодЗаявки] AS [Заявка], [ДатаВыявленияНеисправности] AS [Дата], [ОписаниеПроблемы] AS [Проблема], [СтатусЗаявки] AS [Статус]
                FROM [ЗаявкиНаРемонт]
                WHERE [КодОборудования] = @id
                ORDER BY [КодЗаявки] DESC;
                """, equipmentId),
            Ремонты = await ExecuteDataTableAsync("""
                SELECT r.[КодРемонта] AS [Ремонт], r.[ВидРемонта] AS [Вид], r.[ИтоговоеСостояниеОборудования] AS [Итог], r.[Стоимость] AS [Стоимость]
                FROM [Ремонты] r
                INNER JOIN [ЗаявкиНаРемонт] z ON z.[КодЗаявки] = r.[КодЗаявки]
                WHERE z.[КодОборудования] = @id
                ORDER BY r.[КодРемонта] DESC;
                """, equipmentId),
            Замены = await ExecuteDataTableAsync("""
                SELECT [ДатаЗамены] AS [Дата], [ПричинаЗамены] AS [Причина], [ПереченьЗамененныхЭлементов] AS [Элементы], [Стоимость]
                FROM [Замены]
                WHERE [КодОборудования] = @id
                ORDER BY [ДатаЗамены] DESC;
                """, equipmentId),
            Обслуживание = await ExecuteDataTableAsync("""
                SELECT [ПлановаяДата] AS [Плановая дата], [ДатаВыполнения] AS [Дата выполнения], [ВидОбслуживания] AS [Вид], [Статус]
                FROM [ПлановоеОбслуживание]
                WHERE [КодОборудования] = @id
                ORDER BY [ПлановаяДата] DESC;
                """, equipmentId),
            СанитарнаяОбработка = await ExecuteDataTableAsync("""
                SELECT [ДатаПроведения] AS [Дата], [ВидОбработки] AS [Вид обработки], [ИспользуемоеСредство] AS [Средство], [ОтметкаОВыполнении] AS [Отметка]
                FROM [СанитарныеОбработки]
                WHERE [КодОборудования] = @id
                ORDER BY [ДатаПроведения] DESC;
                """, equipmentId)
        };
    }

    public async Task<List<NotificationItem>> GetNotificationsAsync()
    {
        const string sql = """
            SELECT
                N'Ремонт' AS [Категория],
                CASE WHEN [ПлановаяДатаЗавершения] < CAST(GETDATE() AS DATE) THEN N'Высокий' ELSE N'Средний' END AS [Приоритет],
                o.[Наименование] AS [Оборудование],
                N'Заявка на ремонт' AS [Событие],
                z.[ПлановаяДатаЗавершения] AS [Дата],
                z.[ОписаниеПроблемы] AS [Описание]
            FROM [ЗаявкиНаРемонт] z
            INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
            WHERE z.[СтатусЗаявки] <> N'Закрыта'
              AND z.[ПлановаяДатаЗавершения] <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))

            UNION ALL

            SELECT
                N'Обслуживание' AS [Категория],
                CASE WHEN [ПлановаяДата] < CAST(GETDATE() AS DATE) THEN N'Высокий' ELSE N'Средний' END AS [Приоритет],
                o.[Наименование] AS [Оборудование],
                N'Плановое обслуживание' AS [Событие],
                p.[ПлановаяДата] AS [Дата],
                p.[ВидОбслуживания] AS [Описание]
            FROM [ПлановоеОбслуживание] p
            INNER JOIN [Оборудование] o ON o.[КодОборудования] = p.[КодОборудования]
            WHERE p.[Статус] <> N'Выполнено'
              AND p.[ПлановаяДата] <= DATEADD(DAY, 10, CAST(GETDATE() AS DATE))

            ORDER BY [Дата];
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var items = new List<NotificationItem>();
        while (await reader.ReadAsync())
        {
            items.Add(new NotificationItem
            {
                Категория = reader.GetString(0),
                Приоритет = reader.GetString(1),
                Оборудование = reader.GetString(2),
                Событие = reader.GetString(3),
                Дата = reader.GetDateTime(4),
                Описание = reader.GetString(5)
            });
        }

        return items;
    }

    public async Task<DataTable> GetActionLogAsync()
    {
        return await ExecuteDataTableAsync("""
            SELECT
                [КодДействия] AS [Код],
                [ДатаДействия] AS [Дата действия],
                [ФИОПользователя] AS [Пользователь],
                [РольПользователя] AS [Роль],
                [Раздел] AS [Раздел],
                [Операция] AS [Операция],
                [Описание] AS [Описание]
            FROM [ЖурналДействийПользователей]
            ORDER BY [КодДействия] DESC;
            """);
    }

    public IReadOnlyList<ReportDefinition> GetReports() =>
    [
        new() { Ключ = "repairs", Название = "Отчет по ремонтам", Описание = "Все выполненные ремонты с исполнителем и стоимостью." },
        new() { Ключ = "replacements", Название = "Отчет по заменам", Описание = "Замены оборудования и комплектующих по датам и причинам." },
        new() { Ключ = "sanitation", Название = "Отчет по санитарной обработке", Описание = "Проведенные санитарные процедуры и отметки о выполнении." },
        new() { Ключ = "costs", Название = "Отчет по затратам", Описание = "Суммарные затраты на ремонты и замены по оборудованию." },
        new() { Ключ = "faults", Название = "Статистика неисправностей", Описание = "Частота заявок и проблемное оборудование." }
    ];

    public async Task<DataTable> GetReportDataAsync(string reportKey)
    {
        var sql = reportKey switch
        {
            "repairs" => """
                SELECT o.[Наименование] AS [Оборудование], r.[ВидРемонта] AS [Вид ремонта], p.[ФИО] AS [Исполнитель], r.[Стоимость] AS [Стоимость], r.[ИтоговоеСостояниеОборудования] AS [Итоговое состояние]
                FROM [Ремонты] r
                INNER JOIN [ЗаявкиНаРемонт] z ON z.[КодЗаявки] = r.[КодЗаявки]
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
                INNER JOIN [Пользователи] p ON p.[КодПользователя] = r.[Исполнитель]
                ORDER BY o.[Наименование], r.[КодРемонта] DESC;
                """,
            "replacements" => """
                SELECT o.[Наименование] AS [Оборудование], z.[ДатаЗамены] AS [Дата замены], z.[ПричинаЗамены] AS [Причина], z.[ПереченьЗамененныхЭлементов] AS [Элементы], z.[Стоимость] AS [Стоимость]
                FROM [Замены] z
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = z.[КодОборудования]
                ORDER BY z.[ДатаЗамены] DESC;
                """,
            "sanitation" => """
                SELECT o.[Наименование] AS [Оборудование], s.[ДатаПроведения] AS [Дата], s.[ВидОбработки] AS [Вид обработки], s.[ИспользуемоеСредство] AS [Средство], s.[ОтметкаОВыполнении] AS [Отметка], s.[СоблюдениеНорм] AS [Соблюдение норм]
                FROM [СанитарныеОбработки] s
                INNER JOIN [Оборудование] o ON o.[КодОборудования] = s.[КодОборудования]
                ORDER BY s.[ДатаПроведения] DESC;
                """,
            "costs" => """
                SELECT
                    o.[Наименование] AS [Оборудование],
                    ISNULL(rep.[СуммаРемонтов], 0) AS [Ремонты],
                    ISNULL(repl.[СуммаЗамен], 0) AS [Замены],
                    ISNULL(rep.[СуммаРемонтов], 0) + ISNULL(repl.[СуммаЗамен], 0) AS [Итого]
                FROM [Оборудование] o
                LEFT JOIN (
                    SELECT z.[КодОборудования], SUM(r.[Стоимость]) AS [СуммаРемонтов]
                    FROM [Ремонты] r
                    INNER JOIN [ЗаявкиНаРемонт] z ON z.[КодЗаявки] = r.[КодЗаявки]
                    GROUP BY z.[КодОборудования]
                ) rep ON rep.[КодОборудования] = o.[КодОборудования]
                LEFT JOIN (
                    SELECT [КодОборудования], SUM([Стоимость]) AS [СуммаЗамен]
                    FROM [Замены]
                    GROUP BY [КодОборудования]
                ) repl ON repl.[КодОборудования] = o.[КодОборудования]
                ORDER BY [Итого] DESC, o.[Наименование];
                """,
            "faults" => """
                SELECT
                    o.[Наименование] AS [Оборудование],
                    COUNT(z.[КодЗаявки]) AS [Количество заявок],
                    SUM(CASE WHEN z.[СтатусЗаявки] <> N'Закрыта' THEN 1 ELSE 0 END) AS [Открытые заявки],
                    MAX(z.[ДатаВыявленияНеисправности]) AS [Последняя неисправность]
                FROM [Оборудование] o
                LEFT JOIN [ЗаявкиНаРемонт] z ON z.[КодОборудования] = o.[КодОборудования]
                GROUP BY o.[Наименование]
                ORDER BY [Количество заявок] DESC, o.[Наименование];
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(reportKey), reportKey, "Неизвестный отчет.")
        };

        return await ExecuteDataTableAsync(sql);
    }

    private async Task<Dictionary<string, object?>> ReadSingleRecordAsync(string sql, int id)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Запись не найдена.");
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return result;
    }

    private async Task<DataTable> ExecuteDataTableAsync(string sql, int? id = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        if (id is not null)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private async Task<List<LookupItem>> ReadLookupAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var items = new List<LookupItem>();
        while (await reader.ReadAsync())
        {
            items.Add(new LookupItem
            {
                Id = reader.GetInt32(0),
                Value = reader.GetInt32(0),
                DisplayName = reader.GetString(1)
            });
        }

        return items;
    }

    public async Task<DataTable> GetUsersManagementDataAsync()
    {
        const string sql = """
            SELECT
                p.[КодПользователя] AS [Код],
                p.[ФИО] AS [ФИО],
                p.[Должность] AS [Должность],
                r.[НазваниеРоли] AS [Роль],
                p.[Логин] AS [Логин],
                p.[Телефон] AS [Телефон],
                p.[ЭлектроннаяПочта] AS [Электронная почта]
            FROM [Пользователи] p
            INNER JOIN [Роли] r ON r.[КодРоли] = p.[КодРоли]
            ORDER BY p.[ФИО];
            """;

        return await ExecuteDataTableAsync(sql);
    }

    public async Task<Dictionary<string, object?>> GetUserRecordAsync(int userId)
    {
        const string sql = "SELECT * FROM [Пользователи] WHERE [КодПользователя] = @id;";
        return await ReadSingleRecordAsync(sql, userId);
    }

    public async Task SaveUserAsync(Dictionary<string, object?> values, int? userId = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = userId is null
            ? """
                INSERT INTO [Пользователи]
                ([Логин],[Пароль],[ФИО],[Должность],[Телефон],[ЭлектроннаяПочта],[КодРоли])
                VALUES (@login,@password,@fullName,@position,@phone,@email,@roleId);
                """
            : """
                UPDATE [Пользователи]
                SET [Логин]=@login,
                    [Пароль]=@password,
                    [ФИО]=@fullName,
                    [Должность]=@position,
                    [Телефон]=@phone,
                    [ЭлектроннаяПочта]=@email,
                    [КодРоли]=@roleId
                WHERE [КодПользователя]=@id;
                """;

        Add(command, "@login", values["Логин"]);
        Add(command, "@password", values["Пароль"]);
        Add(command, "@fullName", values["ФИО"]);
        Add(command, "@position", values["Должность"]);
        Add(command, "@phone", values["Телефон"]);
        Add(command, "@email", values["ЭлектроннаяПочта"]);
        Add(command, "@roleId", values["КодРоли"]);

        if (userId is not null)
        {
            command.Parameters.AddWithValue("@id", userId.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteUserAsync(int userId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        const string checkSql = """
            SELECT
                (SELECT COUNT(*) FROM [Оборудование] WHERE [ОтветственноеЛицо] = @id) +
                (SELECT COUNT(*) FROM [ИсторияЭксплуатации] WHERE [Ответственный] = @id) +
                (SELECT COUNT(*) FROM [ЗаявкиНаРемонт] WHERE [Инициатор] = @id) +
                (SELECT COUNT(*) FROM [Ремонты] WHERE [Исполнитель] = @id) +
                (SELECT COUNT(*) FROM [Замены] WHERE [ОтветственныйСотрудник] = @id) +
                (SELECT COUNT(*) FROM [ПлановоеОбслуживание] WHERE [Ответственный] = @id) +
                (SELECT COUNT(*) FROM [СанитарныеОбработки] WHERE [Исполнитель] = @id) +
                (SELECT COUNT(*) FROM [ОбращенияКАдминистрации] WHERE [Отправитель] = @id OR [ОтветственныйАдминистратор] = @id) AS [LinksCount];
            """;

        await using (var checkCommand = new SqlCommand(checkSql, connection))
        {
            checkCommand.Parameters.AddWithValue("@id", userId);
            var linksCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync() ?? 0);
            if (linksCount > 0)
            {
                throw new InvalidOperationException("Нельзя удалить пользователя, потому что он уже используется в записях системы.");
            }
        }

        const string deleteLogSql = "DELETE FROM [ЖурналДействийПользователей] WHERE [КодПользователя] = @id;";
        await using (var deleteLogCommand = new SqlCommand(deleteLogSql, connection))
        {
            deleteLogCommand.Parameters.AddWithValue("@id", userId);
            await deleteLogCommand.ExecuteNonQueryAsync();
        }

        const string deleteUserSql = "DELETE FROM [Пользователи] WHERE [КодПользователя] = @id;";
        await using var deleteUserCommand = new SqlCommand(deleteUserSql, connection);
        deleteUserCommand.Parameters.AddWithValue("@id", userId);
        await deleteUserCommand.ExecuteNonQueryAsync();
    }

    private static void Add(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
