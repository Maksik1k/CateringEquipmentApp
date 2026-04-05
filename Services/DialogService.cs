using System.Windows;
using CateringEquipmentApp.Views;

namespace CateringEquipmentApp.Services;

public static class DialogService
{
    public static void ShowSuccess(Window? owner, string title, string message, string subtitle = "Операция успешно завершена.")
    {
        var dialog = new AppDialogWindow(title, message, subtitle, isSuccess: true)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    public static void ShowInfo(Window? owner, string title, string message, string subtitle = "Информационное сообщение")
    {
        var dialog = new AppDialogWindow(title, message, subtitle)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    public static void ShowError(Window? owner, string title, string message)
    {
        var dialog = new AppDialogWindow(title, message, "Возникла ошибка при выполнении операции.", isError: true)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message)
    {
        var dialog = new AppDialogWindow(title, message, "Подтвердите действие перед продолжением.", isConfirmation: true)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }
}
