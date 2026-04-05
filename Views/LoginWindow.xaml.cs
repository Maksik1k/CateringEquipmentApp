using System.Windows;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp.Views;

public partial class LoginWindow : Window
{
    private readonly DatabaseService _databaseService = new();

    public LoginWindow()
    {
        InitializeComponent();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = string.Empty;
        LoginButton.IsEnabled = false;

        try
        {
            await _databaseService.EnsureInfrastructureAsync();

            var session = await _databaseService.AuthenticateAsync(LoginTextBox.Text.Trim(), PasswordBox.Password.Trim());
            if (session is null)
            {
                StatusTextBlock.Text = "Неверный логин или пароль.";
                return;
            }

            await _databaseService.LogUserActionAsync(session, "Авторизация", "Вход", "Пользователь вошел в систему.");

            var mainWindow = new MainWindow(session);
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(this, "Ошибка подключения к базе", ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
