using System.Windows;
using Cmux.Views;

namespace Cmux.Services;

public static class DialogService
{
    public static MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        Window? owner = null)
    {
        try
        {
            var dialog = new AppDialogWindow(title, message, buttons, icon);
            dialog.Owner = owner
                ?? GetActiveWindow()
                ?? Application.Current?.MainWindow;
            dialog.WindowStartupLocation = dialog.Owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen;

            dialog.ShowDialog();
            return dialog.Result;
        }
        catch
        {
            return MessageBox.Show(message, title, buttons, icon);
        }
    }

    public static bool Confirm(
        string message,
        string title,
        Window? owner = null,
        MessageBoxImage icon = MessageBoxImage.Question)
    {
        return Show(message, title, MessageBoxButton.YesNo, icon, owner) == MessageBoxResult.Yes;
    }

    private static Window? GetActiveWindow()
    {
        if (Application.Current == null)
            return null;

        foreach (Window window in Application.Current.Windows)
        {
            if (window.IsActive)
                return window;
        }

        return null;
    }
}
