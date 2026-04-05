using System.Windows;
using System.Windows.Media;

namespace CateringEquipmentApp.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
    }

    public static void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;

        var resources = Application.Current.Resources;

        if (theme == AppTheme.Dark)
        {
            Set(resources, "AppWindowBackgroundBrush", "#131719");
            Set(resources, "AppShellBackgroundBrush", "#171D20");
            Set(resources, "AppShellSecondaryBrush", "#1E262A");
            Set(resources, "AppSidebarBackgroundBrush", "#11181B");
            Set(resources, "AppSidebarCardBrush", "#1D292D");
            Set(resources, "AppSidebarBorderBrush", "#304247");
            Set(resources, "AppSidebarButtonBrush", "#1B272B");
            Set(resources, "AppSidebarButtonHoverBrush", "#24343A");
            Set(resources, "AppSidebarSelectedBrush", "#3E8A80");
            Set(resources, "AppSurfaceBrush", "#1B2327");
            Set(resources, "AppSurfaceAltBrush", "#202A2E");
            Set(resources, "AppCardBrush", "#1F2A2F");
            Set(resources, "AppCardBorderBrush", "#314045");
            Set(resources, "AppControlBackgroundBrush", "#1C2529");
            Set(resources, "AppControlBorderBrush", "#3D5056");
            Set(resources, "AppControlHoverBrush", "#243036");
            Set(resources, "AppControlFocusBrush", "#7EA79C");
            Set(resources, "AppTextPrimaryBrush", "#F6F1EA");
            Set(resources, "AppTextSecondaryBrush", "#D5CEC4");
            Set(resources, "AppTextMutedBrush", "#A99F94");
            Set(resources, "AppAccentBrush", "#4FA59C");
            Set(resources, "AppDangerBrush", "#D67B63");
            Set(resources, "AppTableHeaderBrush", "#243137");
            Set(resources, "AppTableAltRowBrush", "#1C252A");
            Set(resources, "AppTableHoverBrush", "#29363B");
            Set(resources, "AppTableSelectedBrush", "#324247");
        }
        else
        {
            Set(resources, "AppWindowBackgroundBrush", "#EAE4D9");
            Set(resources, "AppShellBackgroundBrush", "#F2EDE5");
            Set(resources, "AppShellSecondaryBrush", "#FFFDF8");
            Set(resources, "AppSidebarBackgroundBrush", "#26363C");
            Set(resources, "AppSidebarCardBrush", "#36494F");
            Set(resources, "AppSidebarBorderBrush", "#4D6369");
            Set(resources, "AppSidebarButtonBrush", "#31454C");
            Set(resources, "AppSidebarButtonHoverBrush", "#3A535B");
            Set(resources, "AppSidebarSelectedBrush", "#3E8A80");
            Set(resources, "AppSurfaceBrush", "#F5F1EA");
            Set(resources, "AppSurfaceAltBrush", "#FFFCF7");
            Set(resources, "AppCardBrush", "#FFFFFF");
            Set(resources, "AppCardBorderBrush", "#DDD4C7");
            Set(resources, "AppControlBackgroundBrush", "#FFFCF7");
            Set(resources, "AppControlBorderBrush", "#D7CFC3");
            Set(resources, "AppControlHoverBrush", "#FFFFFF");
            Set(resources, "AppControlFocusBrush", "#7EA79C");
            Set(resources, "AppTextPrimaryBrush", "#243238");
            Set(resources, "AppTextSecondaryBrush", "#475D63");
            Set(resources, "AppTextMutedBrush", "#79888D");
            Set(resources, "AppAccentBrush", "#2F7C85");
            Set(resources, "AppDangerBrush", "#C96C54");
            Set(resources, "AppTableHeaderBrush", "#F2ECE2");
            Set(resources, "AppTableAltRowBrush", "#FBF8F2");
            Set(resources, "AppTableHoverBrush", "#F3F7F2");
            Set(resources, "AppTableSelectedBrush", "#E3EEE8");
        }
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
