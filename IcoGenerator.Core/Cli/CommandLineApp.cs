using System.CommandLine;
using System.Globalization;
using System.Text;
using IcoGenerator.Core.Environment;

namespace IcoGenerator.Core.Cli;

/// <summary>
/// Shared CLI entry used by the pure console host and the unified GUI+CLI executable.
/// </summary>
public static class CommandLineApp
{
    /// <summary>
    /// Returns true when the process should run conversion/help in console mode
    /// (instead of opening the WinUI GUI).
    /// </summary>
    public static bool ShouldRunAsCli(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return false;
        }

        // Force GUI
        if (args.Any(IsGuiSwitch))
        {
            return false;
        }

        // Any other argument => CLI (including -h / --version / input path)
        return true;
    }

    private static bool IsGuiSwitch(string a) =>
        a.Equals("--gui", StringComparison.OrdinalIgnoreCase)
        || a.Equals("-gui", StringComparison.OrdinalIgnoreCase)
        || a.Equals("/gui", StringComparison.OrdinalIgnoreCase);

    /// <param name="unifiedHost">When true, welcome text mentions dual GUI+CLI behavior.</param>
    /// <param name="requireDotNetRuntime">
    /// Framework-dependent CLI should set true; self-contained unified host can set false.
    /// </param>
    public static async Task<int> RunAsync(
        string[] args,
        bool unifiedHost = false,
        bool requireDotNetRuntime = true,
        CancellationToken cancellationToken = default)
    {
        ConfigureCultureAndConsole();

        // Strip --gui if present (should not reach here for dual host, but safe).
        args = args.Where(a => !IsGuiSwitch(a)).ToArray();

        var inputArgument = new Argument<FileInfo?>("input")
        {
            Description = "Source image path (PNG, JPG, BMP, GIF, WebP, TIFF, ...). Omit for welcome/usage.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output multi-resolution .ico path. Defaults to <input>.ico."
        };

        var sizesOption = new Option<string>("--sizes", "-s")
        {
            Description =
                "Icon sizes (1-256). Comma/space/semicolon separated. Default: 16,32,48,64,128,256.",
            DefaultValueFactory = _ => string.Join(',', IcoConversionOptions.DefaultSizes)
        };

        var separateOption = new Option<bool>("--separate")
        {
            Description = "Also write per-size .ico files (default: true).",
            DefaultValueFactory = _ => true
        };

        var noSeparateOption = new Option<bool>("--no-separate")
        {
            Description = "Only write the multi-resolution .ico."
        };

        var overwriteOption = new Option<bool>("--overwrite")
        {
            Description = "Overwrite existing output file(s) (default: true).",
            DefaultValueFactory = _ => true
        };

        var noOverwriteOption = new Option<bool>("--no-overwrite")
        {
            Description = "Fail if any output file already exists."
        };

        var skipEnvOption = new Option<bool>("--skip-env-check")
        {
            Description = "Skip the environment scan."
        };

        var root = new RootCommand(
            unifiedHost
                ? "IcoGenerator (unified GUI+CLI) - convert images to Windows .ico files."
                : "IcoGenerator - convert images to multi-resolution and/or per-size Windows .ico files.")
        {
            inputArgument,
            outputOption,
            sizesOption,
            separateOption,
            noSeparateOption,
            overwriteOption,
            noOverwriteOption,
            skipEnvOption
        };

        root.SetAction(async (parseResult, ct) =>
        {
            var skipEnv = parseResult.GetValue(skipEnvOption);
            if (!skipEnv)
            {
                var report = RuntimeEnvironmentProbe.Scan(new EnvironmentProbeOptions
                {
                    RequireDotNetRuntime = requireDotNetRuntime,
                    RequireWindowsDesktopRuntime = false,
                    RequireWindowsAppRuntime = false
                });

                if (report.HasMissing)
                {
                    await Console.Error.WriteLineAsync(report.FormatSummary());
                    await Console.Error.WriteLineAsync();
                }

                if (report.HasMissingRequired)
                {
                    await Console.Error.WriteLineAsync(
                        "Error: required environment components are missing. Install them, then retry.");
                    await Console.Error.WriteLineAsync(
                        $"Download .NET 10: {RuntimeEnvironmentProbe.DotNetRuntimeDownloadUrl}");
                    return 3;
                }
            }

            var input = parseResult.GetValue(inputArgument);
            if (input is null)
            {
                CliWelcome.WriteStartupWelcome(Console.Out, unifiedHost: unifiedHost);
                return 0;
            }

            CliWelcome.WriteCompactBanner(Console.Out);

            var output = parseResult.GetValue(outputOption);
            var sizesText = parseResult.GetValue(sizesOption) ?? string.Join(',', IcoConversionOptions.DefaultSizes);
            var writeSeparate = parseResult.GetValue(separateOption);
            if (parseResult.GetValue(noSeparateOption))
            {
                writeSeparate = false;
            }

            var overwrite = parseResult.GetValue(overwriteOption);
            if (parseResult.GetValue(noOverwriteOption))
            {
                overwrite = false;
            }

            if (!input.Exists)
            {
                await Console.Error.WriteLineAsync($"Error: input file not found: {input.FullName}");
                return 2;
            }

            IReadOnlyList<int> sizes;
            try
            {
                sizes = IcoConverter.ParseSizes(sizesText);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 2;
            }

            var outputPath = output?.FullName
                ?? Path.ChangeExtension(input.FullName, ".ico");

            if (!outputPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.ChangeExtension(outputPath, ".ico") ?? outputPath + ".ico";
            }

            var converter = new IcoConverter();
            if (!converter.IsSupportedExtension(input.FullName))
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: extension '{Path.GetExtension(input.FullName)}' is not in the known list; attempting decode anyway.");
            }

            try
            {
                var result = await converter.ConvertAsync(
                    input.FullName,
                    outputPath,
                    new IcoConversionOptions
                    {
                        Sizes = sizes,
                        Overwrite = overwrite,
                        WriteSeparateFilesPerSize = writeSeparate
                    },
                    ct);

                Console.WriteLine($"OK  {result.InputPath}");
                Console.WriteLine($" -> multi: {result.OutputPath}");
                Console.WriteLine($"    sizes: {string.Join(", ", result.Sizes)}");
                Console.WriteLine($"    multi bytes: {result.OutputBytes}");
                if (result.SeparateFiles.Count > 0)
                {
                    Console.WriteLine($"    separate files ({result.SeparateFiles.Count}):");
                    foreach (var (size, path) in result.SeparateFiles.OrderBy(kv => kv.Key))
                    {
                        Console.WriteLine($"      {size}px -> {path}");
                    }
                }
                else
                {
                    Console.WriteLine("    separate: (disabled)");
                }

                Console.WriteLine($"    total files: {result.FileCount}");
                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        });

        return await root.Parse(args).InvokeAsync(cancellationToken: cancellationToken);
    }

    private static void ConfigureCultureAndConsole()
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = en;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = en;

        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch
        {
            // ignore when redirected / WinExe without console
        }
    }
}
