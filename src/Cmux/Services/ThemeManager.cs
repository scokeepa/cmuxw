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

        // ── Light palette ──────────────────────────────────────────
        // Layer 1  Shell chrome / sidebar / toolbar    #F3F3F3
        // Layer 2  Terminal pane host                  #FFFFFF  (white, like Cursor editor bg)
        // Layer 3  Input fields / cards                #FFFFFF
        // Text     Primary #1E1E1E   Secondary #616161
        // Border   #D5D5D5 (visible on #F3 bg)  Divider #E0E0E0
        //
        // ── Dark palette ─────────────────────────────────────────
        // Layer 1  Shell chrome                        #1E1E1E
        // Layer 2  Sidebar                             #181818
        // Layer 3  Terminal pane host                  #141420
        // Text     Primary #CCCCCC   Secondary #858585
        // Border   #3E3E42           Divider #3E3E42

        ApplyBrushColor("BackgroundBrush", useLight ? "#FFF3F3F3" : "#FF1E1E1E");
        ApplyBrushColor("SidebarBackgroundBrush", useLight ? "#FFF3F3F3" : "#FF181818");
        ApplyBrushColor("SidebarItemHoverBrush", useLight ? "#FFE0E0E0" : "#FF2A2D2E");
        ApplyBrushColor("SidebarItemSelectedBrush", useLight ? "#FFD0D3D8" : "#FF37373D");
        ApplyBrushColor("ForegroundBrush", useLight ? "#FF111111" : "#FFCCCCCC");
        ApplyBrushColor("ForegroundDimBrush", useLight ? "#FF555555" : "#FF858585");
        ApplyBrushColor("BorderBrush", useLight ? "#FFD5D5D5" : "#FF3E3E42");
        ApplyBrushColor("SurfaceTabBackgroundBrush", useLight ? "#FFF3F3F3" : "#FF181818");
        ApplyBrushColor("SurfaceTabSelectedBrush", useLight ? "#FFFFFFFF" : "#FF2A2D2E");
        ApplyBrushColor("DividerBrush", useLight ? "#FFE0E0E0" : "#FF3E3E42");
        ApplyBrushColor("SurfaceBrush", useLight ? "#FFF3F3F3" : "#FF1E1E1E");
        ApplyBrushColor("SurfaceHighBrush", useLight ? "#FFEAEAEA" : "#FF252526");
        ApplyBrushColor("TerminalHostBackgroundBrush", useLight ? "#FFFFFFFF" : "#FF141420");
        ApplyBrushColor("InputBackgroundBrush", useLight ? "#FFFFFFFF" : "#FF3C3C3C");
        ApplyBrushColor("OverlayBackgroundBrush", useLight ? "#E6F3F3F3" : "#E61E1E1E");
        ApplyBrushColor("ChromeHoverWashBrush", useLight ? "#1A000000" : "#26FFFFFF");
        ApplyBrushColor("ChromeActiveWashBrush", useLight ? "#26000000" : "#33FFFFFF");
        ApplyBrushColor("AgentChatBubbleBrush", useLight ? "#FFE8E8E8" : "#33252525");

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
            SetBrush(SystemColors.WindowTextBrushKey, "#FF111111");
            SetBrush(SystemColors.ControlBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.ControlTextBrushKey, "#FF111111");
            SetBrush(SystemColors.HighlightBrushKey, "#FFD0D3D8");
            SetBrush(SystemColors.HighlightTextBrushKey, "#FF111111");
            SetBrush(SystemColors.MenuBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.MenuTextBrushKey, "#FF111111");
            SetBrush(SystemColors.MenuBarBrushKey, "#FFF3F3F3");
            SetBrush(SystemColors.MenuHighlightBrushKey, "#FFD0D3D8");
            SetBrush(SystemColors.ControlLightBrushKey, "#FFF3F3F3");
            SetBrush(SystemColors.ControlLightLightBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.ControlDarkBrushKey, "#FFD5D5D5");
            SetBrush(SystemColors.ControlDarkDarkBrushKey, "#FF808080");
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
