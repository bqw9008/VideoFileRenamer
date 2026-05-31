using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VideoFileRenamer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string PersonalizeRegistryPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    private static bool isDarkTheme;

    protected override void OnStartup(StartupEventArgs e)
    {
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        base.OnStartup(e);

        foreach (Window window in Windows)
        {
            ApplyTitleBarTheme(window);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        base.OnExit(e);
    }

    public static void ApplyTitleBarTheme(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => ApplyTitleBarTheme(window);
            return;
        }

        var enabled = isDarkTheme ? 1 : 0;
        if (DwmSetWindowAttribute(helper.Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(helper.Handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color))
        {
            return;
        }

        Current.Dispatcher.Invoke(() =>
        {
            ApplySystemTheme();
            foreach (Window window in Current.Windows)
            {
                ApplyTitleBarTheme(window);
            }
        });
    }

    private static void ApplySystemTheme()
    {
        isDarkTheme = !IsSystemLightTheme();

        if (isDarkTheme)
        {
            SetBrush("ThemeWindowBackground", "#111827");
            SetBrush("ThemeSurfaceBackground", "#1F2937");
            SetBrush("ThemeSurfaceAltBackground", "#273449");
            SetBrush("ThemeControlBackground", "#1F2937");
            SetBrush("ThemeControlHoverBackground", "#374151");
            SetBrush("ThemeControlPressedBackground", "#4B5563");
            SetBrush("ThemeControlDisabledBackground", "#111827");
            SetBrush("ThemeTextBrush", "#F9FAFB");
            SetBrush("ThemeDisabledTextBrush", "#9CA3AF");
            SetBrush("ThemeBorderBrush", "#4B5563");
            SetBrush("ThemeSelectionBackground", "#1D4ED8");
            SetBrush("ThemeSelectionForeground", "#FFFFFF");
            return;
        }

        SetBrush("ThemeWindowBackground", "#F7F8FA");
        SetBrush("ThemeSurfaceBackground", "#FFFFFF");
        SetBrush("ThemeSurfaceAltBackground", "#F1F3F5");
        SetBrush("ThemeControlBackground", "#FFFFFF");
        SetBrush("ThemeControlHoverBackground", "#F1F5F9");
        SetBrush("ThemeControlPressedBackground", "#E5E7EB");
        SetBrush("ThemeControlDisabledBackground", "#E5E7EB");
        SetBrush("ThemeTextBrush", "#111827");
        SetBrush("ThemeDisabledTextBrush", "#6B7280");
        SetBrush("ThemeBorderBrush", "#D1D5DB");
        SetBrush("ThemeSelectionBackground", "#BFDBFE");
        SetBrush("ThemeSelectionForeground", "#111827");
    }

    private static bool IsSystemLightTheme()
    {
        var value = Registry.GetValue(PersonalizeRegistryPath, AppsUseLightThemeValue, 1);
        return value is not int intValue || intValue > 0;
    }

    private static void SetBrush(string resourceKey, string colorHex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!;
        Current.Resources[resourceKey] = new SolidColorBrush(color);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
