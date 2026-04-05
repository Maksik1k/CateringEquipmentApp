using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Pages;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp;

public partial class MainWindow : Window
{
    private readonly PermissionService _permissionService = new();
    private readonly UserSession _session;
    private readonly List<SectionDefinition> _sections;
    private readonly Dictionary<string, Page> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly DatabaseService _databaseService = new();
    private string _currentSectionKey = string.Empty;

    public MainWindow(UserSession session)
    {
        InitializeComponent();
        _session = session;
        _sections = BuildSections();
        CurrentUserText.Text = session.FullName;
        CurrentRoleText.Text = $"{session.RoleName} | {session.PositionName}";
        BuildNavigation();
        Loaded += async (_, _) =>
        {
            var firstSection = _sections.FirstOrDefault(CanShowSection);
            if (firstSection is not null)
            {
                await NavigateTo(firstSection.Key);
            }
        };
    }

    private List<SectionDefinition> BuildSections() =>
    [
        new() { Key = "users", Title = "Пользователи", Subtitle = "Управление учетными записями и ролями.", Description = "Список сотрудников системы, их роли, должности и контактные данные." },
        new() { Key = "equipment", Title = "Оборудование", Subtitle = "Карточки оборудования и текущие статусы.", Description = "База оборудования с наименованием, моделью, местом установки, ответственным лицом и текущим состоянием." },
        new() { Key = "history", Title = "История эксплуатации", Subtitle = "Журнал событий по оборудованию.", Description = "История изменений, осмотров и перемещений оборудования." },
        new() { Key = "requests", Title = "Заявки на ремонт", Subtitle = "Регистрация неисправностей и контроль сроков.", Description = "Заявки на ремонт, передача в сервис и контроль исполнения." },
        new() { Key = "repairs", Title = "Ремонты", Subtitle = "Фактически выполненные ремонтные работы.", Description = "Журнал ремонтов с исполнителями, материалами и итоговым состоянием." },
        new() { Key = "replacements", Title = "Замены", Subtitle = "Замены оборудования и комплектующих.", Description = "Причины замены, состав замененных элементов и стоимость." },
        new() { Key = "maintenance", Title = "Плановое обслуживание", Subtitle = "Профилактика и соблюдение графиков.", Description = "Плановое техническое обслуживание с ответственными и статусами." },
        new() { Key = "sanitation", Title = "Санитарная обработка", Subtitle = "Санитарный день и отметки о выполнении.", Description = "Журнал санитарных процедур, средств обработки и соблюдения норм." },
        new() { Key = "appeals", Title = "Связь с администрацией", Subtitle = "Передача обращений и заявок.", Description = "Обращения к администрации, статусы рассмотрения и ответственные лица." },
        new() { Key = "notifications", Title = "Уведомления", Subtitle = "Контроль сроков и просрочек.", Description = "Напоминания о приближающихся сроках ремонтов и обслуживания." },
        new() { Key = "reports", Title = "Отчеты", Subtitle = "Сводные аналитические данные.", Description = "Отчеты по ремонтам, заменам, санитарной обработке, затратам и неисправностям." },
        new() { Key = "logs", Title = "Журнал действий", Subtitle = "История работы пользователей в системе.", Description = "Фиксация входов, изменений, назначений и удалений записей." }
    ];

    private void BuildNavigation()
    {
        foreach (var section in _sections.Where(CanShowSection))
        {
            var button = new Button
            {
                Content = BuildNavigationContent(section),
                Tag = section.Key,
                Style = (Style)FindResource("NavButtonStyle")
            };

            button.Click += NavigationButton_Click;
            NavButtonsPanel.Children.Add(button);
        }
    }

    private FrameworkElement BuildNavigationContent(SectionDefinition section)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        if (TryFindResource(GetSectionIconKey(section.Key)) is ImageSource icon)
        {
            panel.Children.Add(new Image
            {
                Source = icon,
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 10, 0)
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = section.Title,
            VerticalAlignment = VerticalAlignment.Center
        });

        return panel;
    }

    private static string GetSectionIconKey(string sectionKey) => sectionKey switch
    {
        "users" => "KitchenCareIcon",
        "equipment" => "EquipmentIcon",
        "history" => "HistoryIcon",
        "requests" => "RequestsIcon",
        "repairs" => "RepairsIcon",
        "replacements" => "ReplacementsIcon",
        "maintenance" => "MaintenanceIcon",
        "sanitation" => "SanitationIcon",
        "appeals" => "AppealsIcon",
        "notifications" => "NotificationIcon",
        "reports" => "ReportsIcon",
        "logs" => "LogIcon",
        _ => "KitchenCareIcon"
    };

    private bool CanShowSection(SectionDefinition section)
    {
        return _permissionService.GetPermissions(_session.RoleName, section.Key).CanView;
    }

    private async void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sectionKey)
        {
            await NavigateTo(sectionKey);
        }
    }

    private async Task NavigateTo(string sectionKey)
    {
        _currentSectionKey = sectionKey;
        HighlightSelectedSection();

        var section = _sections.First(s => s.Key == sectionKey);
        SectionTitleText.Text = section.Title;
        SectionSubtitleText.Text = section.Subtitle;

        if (!_pages.TryGetValue(sectionKey, out var page))
        {
            page = CreatePage(section);
            _pages[sectionKey] = page;
        }

        ContentFrame.Navigate(page);
        await RefreshCurrentPageAsync();
    }

    private Page CreatePage(SectionDefinition section)
    {
        var permissions = _permissionService.GetPermissions(_session.RoleName, section.Key);
        return section.Key switch
        {
            "users" => new UsersPage(_session),
            "notifications" => new NotificationsPage(_session),
            "reports" => new ReportsPage(_session),
            "logs" => new ActionLogPage(),
            _ => new DataSectionPage(section, permissions, _session)
        };
    }

    private async Task RefreshCurrentPageAsync()
    {
        if (!_pages.TryGetValue(_currentSectionKey, out var page))
        {
            return;
        }

        switch (page)
        {
            case UsersPage usersPage:
                await usersPage.RefreshAsync();
                break;
            case DataSectionPage dataSectionPage:
                await dataSectionPage.RefreshAsync();
                break;
            case NotificationsPage notificationsPage:
                await notificationsPage.RefreshAsync();
                break;
            case ReportsPage reportsPage:
                await reportsPage.RefreshAsync();
                break;
            case ActionLogPage actionLogPage:
                await actionLogPage.RefreshAsync();
                break;
        }
    }

    private void HighlightSelectedSection()
    {
        foreach (var child in NavButtonsPanel.Children.OfType<Button>())
        {
            var isSelected = string.Equals(child.Tag as string, _currentSectionKey, StringComparison.OrdinalIgnoreCase);
            child.Background = (Brush)FindResource(isSelected ? "AppSidebarSelectedBrush" : "AppSidebarButtonBrush");
            child.BorderBrush = (Brush)FindResource(isSelected ? "AppSidebarSelectedBrush" : "AppSidebarBorderBrush");
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshCurrentPageAsync();
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        await _databaseService.LogUserActionAsync(_session, "Авторизация", "Выход", "Пользователь завершил сеанс.");
        var loginWindow = new Views.LoginWindow();
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }

    private void ContentFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {

    }
}
