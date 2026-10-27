using System.Runtime.InteropServices;
using System.Text;
using IcoGenerator.Core.Cli;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IcoGenerator_App;

/// <summary>
/// Dual-mode entry point:
/// - No args / --gui  -> WinUI GUI
/// - Any other args   -> CLI conversion (attaches to parent console when possible)
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // Required for PublishSingleFile + Windows App SDK (before any WinAppSDK/WinUI use).
            // See Microsoft.WindowsAppSDK.SingleFile.targets warning.
            Environment.SetEnvironmentVariable(
                "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
                AppContext.BaseDirectory);

            // CLI mode first — never spin up WinUI if the user only wants conversion.
            if (CommandLineApp.ShouldRunAsCli(args))
            {
                NativeConsole.TryAttachOrAlloc();
                var code = CommandLineApp.RunAsync(
                        args,
                        unifiedHost: true,
                        // Self-contained single-file host: .NET is inside the exe.
                        requireDotNetRuntime: false)
                    .GetAwaiter()
                    .GetResult();
                Environment.Exit(code);
                return;
            }

            // GUI mode
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(static p =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            CrashLog.ShowMessage(
                "IcoGenerator failed to start."
                + Environment.NewLine + Environment.NewLine
                + ex.GetType().Name + ": " + ex.Message
                + Environment.NewLine + Environment.NewLine
                + "A log was written to:"
                + Environment.NewLine
                + CrashLog.LogPath);
            Environment.Exit(1);
        }
    }
}

internal static class CrashLog
{
    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IcoGenerator",
        "startup-error.log");

    public static void Write(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var sb = new StringBuilder();
            sb.AppendLine(DateTimeOffset.Now.ToString("O"));
            sb.AppendLine(ex.ToString());
            sb.AppendLine("---");
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
            // ignore
        }
    }

    public static void ShowMessage(string text)
    {
        try
        {
            MessageBoxW(IntPtr.Zero, text, "IcoGenerator", 0x00000010); // MB_ICONERROR
        }
        catch
        {
            // ignore
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}

/// <summary>
/// WinExe subsystem has no console by default. Attach to the parent console (cmd/pwsh)
/// so CLI stdout/stderr work when launched from a terminal.
/// </summary>
internal static class NativeConsole
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static void TryAttachOrAlloc()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            // Not launched from a console — allocate one so CLI output is visible.
            AllocConsole();
        }

        try
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            var stdin = new StreamReader(Console.OpenStandardInput());
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Console.SetIn(stdin);
        }
        catch
        {
            // Best-effort.
        }
    }
}
