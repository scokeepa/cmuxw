using System.Windows;
using System.Windows.Media;
using Cmux.Services;

namespace Cmux.Views;

public partial class AppDialogWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    private readonly MessageBoxButton _buttons;

    public AppDialogWindow(string title, string message, MessageBoxButton buttons, MessageBoxImage icon)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);

        _buttons = buttons;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        ConfigureIcon(icon);
        ConfigureButtons(buttons);
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        var accent = Application.Current?.TryFindResource("AccentBrush") as Brush
                     ?? Brushes.DodgerBlue;
        var warn = Application.Current?.TryFindResource("WarningBrush") as Brush
                   ?? Brushes.Orange;
        var err = Application.Current?.TryFindResource("ErrorBrush") as Brush
                  ?? Brushes.IndianRed;

        switch (icon)
        {
            case MessageBoxImage.Error:
                IconGlyph.Text = "\uEA39";
                IconGlyph.Foreground = err;
                break;
            case MessageBoxImage.Warning:
                IconGlyph.Text = "\uE7BA";
                IconGlyph.Foreground = warn;
                break;
            case MessageBoxImage.Question:
                IconGlyph.Text = "\uE897";
                IconGlyph.Foreground = accent;
                break;
            default:
                IconGlyph.Text = "\uE946";
                IconGlyph.Foreground = accent;
                break;
        }
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        var ok = L.T("OK");
        var cancel = L.T("Cancel");
        var yes = L.T("Yes");
        var no = L.T("No");

        switch (buttons)
        {
            case MessageBoxButton.OK:
                PrimaryButton.Content = ok;
                break;
            case MessageBoxButton.OKCancel:
                SecondaryButton.Content = cancel;
                SecondaryButton.Visibility = Visibility.Visible;
                PrimaryButton.Content = ok;
                break;
            case MessageBoxButton.YesNo:
                SecondaryButton.Content = no;
                SecondaryButton.Visibility = Visibility.Visible;
                PrimaryButton.Content = yes;
                break;
            case MessageBoxButton.YesNoCancel:
                TertiaryButton.Content = cancel;
                TertiaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = no;
                SecondaryButton.Visibility = Visibility.Visible;
                PrimaryButton.Content = yes;
                break;
            default:
                PrimaryButton.Content = ok;
                break;
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttons switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.Yes,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Yes,
            _ => MessageBoxResult.OK
        };

        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttons switch
        {
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.No,
            _ => MessageBoxResult.None
        };

        DialogResult = false;
        Close();
    }

    private void TertiaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttons is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel
            ? MessageBoxResult.Cancel
            : MessageBoxResult.None;
        DialogResult = false;
        Close();
    }
}
