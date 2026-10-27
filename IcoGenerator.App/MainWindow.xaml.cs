using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace IcoGenerator_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Multi-resolution app icon shipped as project Content under Assets/Icons.</summary>
    private const string AppIconRelativePath = @"Assets\Icons\AppIcon.ico";

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Taskbar + window chrome icon (multi-resolution ICO from project resources).
        SetWindowIconFromAssets();

        RootFrame.Navigate(typeof(MainPage));

        // After the window is loaded, XamlRoot is available for ContentDialog.
        RootFrame.Loaded += RootFrame_Loaded;
    }

    private async void RootFrame_Loaded(object sender, RoutedEventArgs e)
    {
        RootFrame.Loaded -= RootFrame_Loaded;
        if (RootFrame.XamlRoot is null)
        {
            return;
        }

        try
        {
            await EnvironmentGate.RunAsync(RootFrame.XamlRoot);
        }
        catch
        {
            // Never block app startup if the dialog fails.
        }
    }

    private void SetWindowIconFromAssets()
    {
        var baseDir = AppContext.BaseDirectory;
        var icoPath = Path.Combine(baseDir, AppIconRelativePath);

        if (!File.Exists(icoPath))
        {
            // Fallback relative path
            icoPath = AppIconRelativePath.Replace('\\', '/');
        }

        try
        {
            AppWindow.SetIcon(icoPath);
        }
        catch
        {
            // App can run without a custom icon.
        }

        try
        {
            var full = Path.Combine(baseDir, AppIconRelativePath);
            if (!File.Exists(full))
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var icon = NativeMethods.LoadImage(
                IntPtr.Zero,
                full,
                NativeMethods.IMAGE_ICON,
                0,
                0,
                NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);

            if (icon != IntPtr.Zero)
            {
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_SETICON, NativeMethods.ICON_BIG, icon);
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_SETICON, NativeMethods.ICON_SMALL, icon);
            }
        }
        catch
        {
            // Best-effort only.
        }
    }
}

internal static class NativeMethods
{
    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x0010;
    public const uint LR_DEFAULTSIZE = 0x0040;
    public const int WM_SETICON = 0x0080;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadImage(
        IntPtr hInst,
        string name,
        uint type,
        int cx,
        int cy,
        uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
}
