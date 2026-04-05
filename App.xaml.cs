using System.Windows;
using CateringEquipmentApp.Services;
using CateringEquipmentApp.Views;

namespace CateringEquipmentApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        ThemeService.ApplyTheme(AppTheme.Light);

        var loginWindow = new LoginWindow();
        MainWindow = loginWindow;
        loginWindow.Show();
    }
}
