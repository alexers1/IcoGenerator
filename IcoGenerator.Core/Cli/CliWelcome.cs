using System.Reflection;

namespace IcoGenerator.Core.Cli;

/// <summary>
/// CLI startup banner and usage guide (English only).
/// </summary>
public static class CliWelcome
{
    public static string ProductName => "IcoGenerator";

    public static string Version
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString(3)
                ?? "0.0.0";
        }
    }

    public static void WriteStartupWelcome(TextWriter writer, string? executableName = null, bool unifiedHost = false)
    {
        var exe = executableName ?? GetExecutableName();
        var color = ShouldUseColor();

        WriteLine(writer, color, ConsoleColor.Cyan, "============================================================");
        WriteLine(writer, color, ConsoleColor.Cyan, $"  {ProductName}  v{Version}");
        WriteLine(writer, color, ConsoleColor.Cyan, "  Convert images to multi-resolution Windows .ico files");
        WriteLine(writer, color, ConsoleColor.Cyan, "============================================================");
        writer.WriteLine();
        if (unifiedHost)
        {
            WriteLine(writer, color, ConsoleColor.White, "  Unified host: GUI + CLI in one executable.");
            writer.WriteLine("  - Double-click / no args  -> open GUI");
            writer.WriteLine("  - With conversion args    -> CLI mode (this console)");
            writer.WriteLine("  - --gui                   -> force GUI even with extra args ignored");
        }
        else
        {
            WriteLine(writer, color, ConsoleColor.White, "  Welcome! You launched the CLI host.");
            writer.WriteLine("  GUI and CLI share the same conversion engine (IcoGenerator.Core).");
        }

        writer.WriteLine();
        WriteUsageGuide(writer, exe, color, unifiedHost);
    }

    public static void WriteUsageGuide(
        TextWriter writer,
        string? executableName = null,
        bool? useColor = null,
        bool unifiedHost = false)
    {
        var exe = executableName ?? GetExecutableName();
        var color = useColor ?? ShouldUseColor();
        var defaults = string.Join(',', IcoConversionOptions.DefaultSizes);

        WriteLine(writer, color, ConsoleColor.Yellow, "How to use");
        writer.WriteLine("------------------------------------------------------------");
        writer.WriteLine();
        writer.WriteLine("  Basic conversion:");
        WriteLine(writer, color, ConsoleColor.Green, $"    {exe} <input-image> [-o <output.ico>] [-s <sizes>]");
        writer.WriteLine();
        writer.WriteLine("  Examples:");
        WriteLine(writer, color, ConsoleColor.Green, $"    {exe} logo.png");
        writer.WriteLine("        -> multi logo.ico + separate logo_16.ico ...");
        WriteLine(writer, color, ConsoleColor.Green, $"    {exe} logo.png -o app.ico -s 16,32,48,256");
        writer.WriteLine("        -> custom sizes");
        WriteLine(writer, color, ConsoleColor.Green, $"    {exe} logo.png --no-separate");
        writer.WriteLine("        -> only multi-resolution .ico");
        if (unifiedHost)
        {
            WriteLine(writer, color, ConsoleColor.Green, $"    {exe}");
            writer.WriteLine("        -> open the GUI");
            WriteLine(writer, color, ConsoleColor.Green, $"    {exe} --gui");
            writer.WriteLine("        -> force GUI");
        }

        writer.WriteLine();
        writer.WriteLine("  Options:");
        writer.WriteLine("    <input>              Source image (PNG, JPG, BMP, GIF, WebP, TIFF, ...)");
        writer.WriteLine("    -o, --output <path>  Multi-resolution .ico path (default: <input>.ico)");
        writer.WriteLine($"    -s, --sizes <list>   Custom sizes 1-256 (default: {defaults})");
        writer.WriteLine("    --separate           Also write per-size .ico files (default)");
        writer.WriteLine("    --no-separate        Only multi-resolution .ico");
        writer.WriteLine("    --overwrite          Overwrite existing output (default: true)");
        writer.WriteLine("    --no-overwrite       Do not overwrite existing files");
        writer.WriteLine("    --skip-env-check     Skip environment scan");
        if (unifiedHost)
        {
            writer.WriteLine("    --gui                Open GUI (ignore conversion args)");
        }

        writer.WriteLine("    -h, --help           Show help");
        writer.WriteLine("    --version            Show version");
        writer.WriteLine();
        writer.WriteLine("  Exit codes: 0 ok | 1 convert fail | 2 bad args | 3 env missing");
        writer.WriteLine();
    }

    public static void WriteCompactBanner(TextWriter writer)
    {
        if (ShouldUseColor())
        {
            var prev = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                writer.WriteLine($"{ProductName} v{Version}");
            }
            finally
            {
                Console.ForegroundColor = prev;
            }
        }
        else
        {
            writer.WriteLine($"{ProductName} v{Version}");
        }
    }

    public static string GetExecutableName()
    {
        try
        {
            var path = System.Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
        }
        catch
        {
            // ignore
        }

        return "IcoGenerator.exe";
    }

    private static bool ShouldUseColor()
    {
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }

        return true;
    }

    private static void WriteLine(TextWriter writer, bool color, ConsoleColor consoleColor, string text)
    {
        if (!color || !ReferenceEquals(writer, Console.Out))
        {
            writer.WriteLine(text);
            return;
        }

        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = consoleColor;
            writer.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
