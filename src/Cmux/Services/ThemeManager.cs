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
        var useLight = theme.Equals("Default Light", StringComparison.OrdinalIgnoreCase)
            || (theme.Equals("System", StringComparison.OrdinalIgnoreCase) && IsSystemLightTheme());

        if (Application.Current == null)
            return;

        ApplyBrushColor("BackgroundBrush", useLight ? "#FFF5F7FA" : "#FF0F0F0F");
        ApplyBrushColor("SidebarBackgroundBrush", useLight ? "#FFEFF2F8" : "#FF0A0A0A");
        ApplyBrushColor("SidebarItemHoverBrush", useLight ? "#FFDDE7FF" : "#FF1A1A2E");
        ApplyBrushColor("SidebarItemSelectedBrush", useLight ? "#FFC9D8FF" : "#FF252547");
        ApplyBrushColor("ForegroundBrush", useLight ? "#FF1F2937" : "#FFE2E2E9");
        ApplyBrushColor("ForegroundDimBrush", useLight ? "#FF4B5563" : "#FF6B6B80");
        ApplyBrushColor("BorderBrush", useLight ? "#FFD3DBE8" : "#FF2A2A3C");
        ApplyBrushColor("SurfaceTabBackgroundBrush", useLight ? "#FFF1F5FB" : "#FF161622");
        ApplyBrushColor("SurfaceTabSelectedBrush", useLight ? "#FFDDE7FF" : "#FF1E1E32");
        ApplyBrushColor("DividerBrush", useLight ? "#FFC6D0E0" : "#FF2E2E42");
        ApplyBrushColor("SurfaceBrush", useLight ? "#FFFFFFFF" : "#FF141420");
        ApplyBrushColor("SurfaceHighBrush", useLight ? "#FFF7FAFF" : "#FF1C1C30");
        ApplyBrushColor("InputBackgroundBrush", useLight ? "#FFFFFFFF" : "#FF1A1A1A");
        ApplyBrushColor("OverlayBackgroundBrush", useLight ? "#F0FFFFFF" : "#F2141414");
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

    private static void ApplyBrushColor(string key, string hex)
    {
        if (Application.Current.Resources[key] is not SolidColorBrush brush)
            return;

        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (brush.IsFrozen)
        {
            // Some merged resources are frozen Freezables, so mutate by replacement.
            Application.Current.Resources[key] = new SolidColorBrush(color);
            return;
        }

        brush.Color = color;
    }
}
