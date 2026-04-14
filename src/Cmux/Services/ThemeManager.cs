using System.Windows;
using System.Windows.Media;
using Cmux.Core.Config;
using Microsoft.Win32;

namespace Cmux.Services;

internal static class ThemeManager
{
    public static void ApplyTheme(string? themeName)
    {
        var theme = (themeName ?? "Default Dark").Trim();
        var useLight = IsLightTheme(theme)
            || (IsSystemTheme(theme) && IsSystemLightTheme());

        if (Application.Current == null)
            return;

        // Cursor-style neutrals: chrome #F3F3F3, terminal pane chrome #F8F8F8 (see TerminalHostBackgroundBrush).
        ApplyBrushColor("BackgroundBrush", useLight ? "#FFF3F3F3" : "#FF1E1E1E");
        ApplyBrushColor("SidebarBackgroundBrush", useLight ? "#FFF3F3F3" : "#FF181818");
        ApplyBrushColor("SidebarItemHoverBrush", useLight ? "#FFE8E8E8" : "#FF2A2D2E");
        ApplyBrushColor("SidebarItemSelectedBrush", useLight ? "#FFDFE0E2" : "#FF37373D");
        ApplyBrushColor("ForegroundBrush", useLight ? "#FF383A42" : "#FFCCCCCC");
        ApplyBrushColor("ForegroundDimBrush", useLight ? "#FF6E6E6E" : "#FF858585");
        ApplyBrushColor("BorderBrush", useLight ? "#FFE5E5E5" : "#FF3E3E42");
        ApplyBrushColor("SurfaceTabBackgroundBrush", useLight ? "#FFF3F3F3" : "#FF181818");
        ApplyBrushColor("SurfaceTabSelectedBrush", useLight ? "#FFE8E8E8" : "#FF2A2D2E");
        ApplyBrushColor("DividerBrush", useLight ? "#FFE0E0E0" : "#FF3E3E42");
        ApplyBrushColor("SurfaceBrush", useLight ? "#FFF3F3F3" : "#FF1E1E1E");
        ApplyBrushColor("SurfaceHighBrush", useLight ? "#FFECECEC" : "#FF252526");
        ApplyBrushColor("TerminalHostBackgroundBrush", useLight ? "#FFF8F8F8" : "#FF141420");
        ApplyBrushColor("InputBackgroundBrush", useLight ? "#FFFFFFFF" : "#FF3C3C3C");
        ApplyBrushColor("OverlayBackgroundBrush", useLight ? "#E6FFFFFF" : "#E61E1E1E");
        ApplyBrushColor("ChromeHoverWashBrush", useLight ? "#14000000" : "#26FFFFFF");
        ApplyBrushColor("ChromeActiveWashBrush", useLight ? "#1F000000" : "#33FFFFFF");
        ApplyBrushColor("AgentChatBubbleBrush", useLight ? "#FFF0F0F0" : "#33252525");

        ApplySystemChrome(useLight);

        // Do NOT use Application.ThemeMode — it force-loads the WPF Fluent theme dictionary
        // which overrides every custom brush we set above, breaking both dark and light modes.
    }

    /// <summary>
    /// WPF ComboBox, Menu, and ListBox use <see cref="SystemColors"/> resource keys.
    /// Without updating these, parts of the UI stay dark when switching to a light app palette.
    /// </summary>
    private static void ApplySystemChrome(bool useLight)
    {
        var r = Application.Current!.Resources;

        void SetBrush(object key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            if (r[key] is SolidColorBrush existing && !existing.IsFrozen)
            {
                existing.Color = color;
                return;
            }

            r[key] = new SolidColorBrush(color);
        }

        if (useLight)
        {
            SetBrush(SystemColors.WindowBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.WindowTextBrushKey, "#FF383A42");
            SetBrush(SystemColors.ControlBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.ControlTextBrushKey, "#FF383A42");
            SetBrush(SystemColors.HighlightBrushKey, "#FFDFE0E2");
            SetBrush(SystemColors.HighlightTextBrushKey, "#FF383A42");
            SetBrush(SystemColors.MenuBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.MenuTextBrushKey, "#FF383A42");
            SetBrush(SystemColors.MenuBarBrushKey, "#FFF3F3F3");
            SetBrush(SystemColors.MenuHighlightBrushKey, "#FFDFE0E2");
            SetBrush(SystemColors.ControlLightBrushKey, "#FFF3F3F3");
            SetBrush(SystemColors.ControlLightLightBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.ControlDarkBrushKey, "#FFE5E5E5");
            SetBrush(SystemColors.ControlDarkDarkBrushKey, "#FF9CA3AF");
        }
        else
        {
            SetBrush(SystemColors.WindowBrushKey, "#FF1E1E1E");
            SetBrush(SystemColors.WindowTextBrushKey, "#FFCCCCCC");
            SetBrush(SystemColors.ControlBrushKey, "#FF252526");
            SetBrush(SystemColors.ControlTextBrushKey, "#FFCCCCCC");
            SetBrush(SystemColors.HighlightBrushKey, "#FF37373D");
            SetBrush(SystemColors.HighlightTextBrushKey, "#FFCCCCCC");
            SetBrush(SystemColors.MenuBrushKey, "#FF252526");
            SetBrush(SystemColors.MenuTextBrushKey, "#FFCCCCCC");
            SetBrush(SystemColors.MenuBarBrushKey, "#FF181818");
            SetBrush(SystemColors.MenuHighlightBrushKey, "#FF37373D");
            SetBrush(SystemColors.ControlLightBrushKey, "#FF3E3E42");
            SetBrush(SystemColors.ControlLightLightBrushKey, "#FF3E3E42");
            SetBrush(SystemColors.ControlDarkBrushKey, "#FF3E3E42");
            SetBrush(SystemColors.ControlDarkDarkBrushKey, "#FF3E3E42");
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSystemTheme(string theme)
    {
        return theme.Equals("System", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("System Follow", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Follow System", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLightTheme(string theme)
    {
        return theme.Equals("Default Light", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBrushColor(string key, string hex)
    {
        var app = Application.Current;
        if (app == null)
            return;

        var color = (Color)ColorConverter.ConvertFromString(hex)!;

        // App-level Resources[key] has highest priority in the WPF lookup chain,
        // beating any merged-dictionary entry (DarkTheme.xaml).  DynamicResource
        // bindings throughout the app automatically pick up the new value.
        if (app.Resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
        }
        else
        {
            app.Resources[key] = new SolidColorBrush(color);
        }
    }
}
