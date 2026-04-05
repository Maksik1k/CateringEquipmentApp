using System.Windows;
using System.Windows.Media;

namespace CateringEquipmentApp.Views;

public partial class AppDialogWindow : Window
{
    public AppDialogWindow(string title, string message, string subtitle, bool isConfirmation = false, bool isError = false, bool isSuccess = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        SubtitleText.Text = subtitle;

        if (isConfirmation)
        {
            CancelButton.Visibility = Visibility.Visible;
            OkButton.Content = "Подтвердить";
        }

        if (isError)
        {
            AccentBubble.Background = (Brush)new BrushConverter().ConvertFromString("#D85B50")!;
            AccentGlyph.Text = "!";
        }
        else if (isSuccess)
        {
            AccentBubble.Background = (Brush)new BrushConverter().ConvertFromString("#35A46F")!;
            AccentGlyph.Text = "✓";
            OkButton.Content = "Отлично";
        }
        else if (isConfirmation)
        {
            AccentBubble.Background = (Brush)new BrushConverter().ConvertFromString("#D7962C")!;
            AccentGlyph.Text = "?";
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
