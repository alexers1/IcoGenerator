using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SysEnv = System.Environment;

namespace IcoGenerator.Core.Environment;

/// <summary>
/// Options controlling which components are treated as required.
/// </summary>
public sealed class EnvironmentProbeOptions
{
    /// <summary>Minimum Windows build (default: 17763 = Windows 10 1809).</summary>
    public int MinWindowsBuild { get; init; } = 17763;

    /// <summary>Major .NET version required for framework-dependent CLI / FDD apps.</summary>
    public int DotNetMajorVersion { get; init; } = 10;

    /// <summary>
    /// When true, missing .NET 10 is required (framework-dependent publish).
    /// Self-contained app hosts may set this false for the GUI process itself.
    /// </summary>
    public bool RequireDotNetRuntime { get; init; } = true;

    /// <summary>When true, require Microsoft.WindowsDesktop.App shared framework.</summary>
    public bool RequireWindowsDesktopRuntime { get; init; } = false;

    /// <summary>
    /// When true, require a Windows App Runtime 2.x package (for non-self-contained WinUI).
    /// </summary>
    public bool RequireWindowsAppRuntime { get; init; } = false;

    /// <summary>Minimum Windows App Runtime major version (2 = WASDK 2.x).</summary>
    public int WindowsAppRuntimeMajor { get; init; } = 2;
}

/// <summary>
/// Scans the local machine for OS / .NET / Windows App Runtime prerequisites.
/// Safe to call from CLI and GUI (no UI dependencies).
/// </summary>
public static class RuntimeEnvironmentProbe
{
    public const string DotNetRuntimeDownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";
    public const string WindowsAppRuntimeDownloadUrl = "https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads";

    /// <summary>
    /// Default probe for the framework-dependent single-file CLI.
    /// </summary>
    public static EnvironmentProbeOptions CliOptions { get; } = new()
    {
        RequireDotNetRuntime = true,
        RequireWindowsDesktopRuntime = false,
        RequireWindowsAppRuntime = false
    };

    /// <summary>
    /// Probe for the WinUI host. Self-contained builds still recommend .NET 10 for the companion CLI.
    /// </summary>
    public static EnvironmentProbeOptions AppOptions { get; } = new()
    {
        // GUI is typically WindowsAppSDKSelfContained — .NET is recommended for CLI tooling.
        RequireDotNetRuntime = false,
        RequireWindowsDesktopRuntime = false,
        // Self-contained WinUI bundles WASDK; mark as recommended so FDD deploys still surface it.
        RequireWindowsAppRuntime = false
    };

    public static EnvironmentReport Scan(EnvironmentProbeOptions? options = null)
    {
        options ??= new EnvironmentProbeOptions();
        var items = new List<EnvironmentCheckItem>
        {
            CheckWindowsVersion(options.MinWindowsBuild),
            CheckDotNetSharedFramework("Microsoft.NETCore.App", options.DotNetMajorVersion, options.RequireDotNetRuntime,
                displayName: $".NET {options.DotNetMajorVersion} Runtime (Microsoft.NETCore.App)",
                description: "Required to run the framework-dependent single-file CLI (IcoGenerator.exe) published without --self-contained.",
                installHint: $"Install the .NET {options.DotNetMajorVersion} Runtime (or SDK) for Windows x64."),
            CheckDotNetSharedFramework("Microsoft.WindowsDesktop.App", options.DotNetMajorVersion, options.RequireWindowsDesktopRuntime,
                displayName: $".NET {options.DotNetMajorVersion} Desktop Runtime",
                description: "Required for framework-dependent WinUI / WPF / WinForms apps targeting Windows Desktop.",
                installHint: $"Install the .NET {options.DotNetMajorVersion} Desktop Runtime for Windows x64."),
            CheckWindowsAppRuntime(options.WindowsAppRuntimeMajor, options.RequireWindowsAppRuntime)
        };

        return new EnvironmentReport { Items = items };
    }

    private static EnvironmentCheckItem CheckWindowsVersion(int minBuild)
    {
        var os = SysEnv.OSVersion;
        var present = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && os.Version.Major >= 10;
        // SysEnv.OSVersion may be lied about on older apps; also try registry build.
        var build = TryGetWindowsBuild();
        var versionLabel = build is int b
            ? $"Windows 10/11 build {b}"
            : os.VersionString;

        if (build is int actualBuild)
        {
            present = actualBuild >= minBuild;
        }

        return new EnvironmentCheckItem
        {
            Id = "windows-os",
            DisplayName = "Windows version",
            Description = $"Windows 10 build {minBuild}+ (or Windows 11) is required for WinUI 3 / modern icons tooling.",
            IsPresent = present,
            IsRequired = true,
            DetectedVersion = versionLabel,
            InstallHint = "Upgrade Windows to a supported build (Windows 10 1809+ or Windows 11).",
            InstallUrl = "https://www.microsoft.com/software-download/windows11"
        };
    }

    private static EnvironmentCheckItem CheckDotNetSharedFramework(
        string frameworkName,
        int major,
        bool required,
        string displayName,
        string description,
        string installHint)
    {
        var (present, version) = FindSharedFramework(frameworkName, major);
        return new EnvironmentCheckItem
        {
            Id = $"dotnet-{frameworkName.ToLowerInvariant()}",
            DisplayName = displayName,
            Description = description,
            IsPresent = present,
            IsRequired = required,
            DetectedVersion = version,
            InstallHint = installHint,
            InstallUrl = DotNetRuntimeDownloadUrl
        };
    }

    private static EnvironmentCheckItem CheckWindowsAppRuntime(int major, bool required)
    {
        var (present, version) = FindWindowsAppRuntimePackage(major);
        return new EnvironmentCheckItem
        {
            Id = "windows-app-runtime",
            DisplayName = $"Windows App Runtime {major}.x",
            Description = "Required for framework-dependent (non-self-contained) WinUI 3 applications.",
            IsPresent = present,
            IsRequired = required,
            DetectedVersion = version,
            InstallHint = $"Install Windows App Runtime {major}.x (x64) from Microsoft.",
            InstallUrl = WindowsAppRuntimeDownloadUrl
        };
    }

    private static (bool Present, string? Version) FindSharedFramework(string frameworkName, int major)
    {
        foreach (var root in EnumerateDotNetSharedRoots())
        {
            var fxDir = Path.Combine(root, frameworkName);
            if (!Directory.Exists(fxDir))
            {
                continue;
            }

            string? best = null;
            Version? bestVer = null;
            foreach (var dir in Directory.EnumerateDirectories(fxDir))
            {
                var name = Path.GetFileName(dir);
                var core = name.Split('-')[0];
                if (!Version.TryParse(core, out var v) || v.Major != major)
                {
                    continue;
                }

                if (bestVer is null || v > bestVer)
                {
                    bestVer = v;
                    best = name;
                }
            }

            if (best is not null)
            {
                return (true, best);
            }
        }

        return (false, null);
    }

    private static IEnumerable<string> EnumerateDotNetSharedRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var shared = path.EndsWith("shared", StringComparison.OrdinalIgnoreCase)
                ? path
                : Path.Combine(path, "shared");
            if (Directory.Exists(shared))
            {
                seen.Add(Path.GetFullPath(shared));
            }
        }

        var dotnetRoot = SysEnv.GetEnvironmentVariable("DOTNET_ROOT");
        Add(dotnetRoot);

        Add(Path.Combine(SysEnv.GetFolderPath(SysEnv.SpecialFolder.ProgramFiles), "dotnet"));
        Add(Path.Combine(SysEnv.GetFolderPath(SysEnv.SpecialFolder.ProgramFilesX86), "dotnet"));

        // Host may live next to the running process for some installs.
        try
        {
            var processPath = SysEnv.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Add(dir);
                    Add(Path.GetFullPath(Path.Combine(dir, "..")));
                }
            }
        }
        catch
        {
            // ignore
        }

        return seen;
    }

    /// <summary>
    /// Detect Windows App Runtime via installed package folders / Appx registry-friendly paths.
    /// </summary>
    private static (bool Present, string? Version) FindWindowsAppRuntimePackage(int major)
    {
        // 1) Package repository under LocalAppData (per-user)
        var localPackages = Path.Combine(
            SysEnv.GetFolderPath(SysEnv.SpecialFolder.LocalApplicationData),
            "Packages");
        var hit = ScanPackageFolder(localPackages, major);
        if (hit.Present)
        {
            return hit;
        }

        // 2) Program Files\WindowsApps (may be ACL-restricted; best-effort)
        var windowsApps = Path.Combine(
            SysEnv.GetFolderPath(SysEnv.SpecialFolder.ProgramFiles),
            "WindowsApps");
        hit = ScanPackageFolder(windowsApps, major);
        if (hit.Present)
        {
            return hit;
        }

        // 3) Known framework package names under AppData\Local\Microsoft\WindowsApps (stub)
        // Fallback: look for Microsoft.WindowsAppRuntime.* folders anywhere under LocalAppData\Microsoft
        var msLocal = Path.Combine(
            SysEnv.GetFolderPath(SysEnv.SpecialFolder.LocalApplicationData),
            "Microsoft");
        if (Directory.Exists(msLocal))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(msLocal, "Microsoft.WindowsAppRuntime*", SearchOption.AllDirectories)
                             .Take(50))
                {
                    var name = Path.GetFileName(dir);
                    if (TryParseWarVersion(name, major, out var label))
                    {
                        return (true, label);
                    }
                }
            }
            catch
            {
                // ACL / enumeration errors are fine
            }
        }

        return (false, null);
    }

    private static (bool Present, string? Version) ScanPackageFolder(string root, int major)
    {
        if (!Directory.Exists(root))
        {
            return (false, null);
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase)
                    && TryParseWarVersion(name, major, out var label))
                {
                    return (true, label);
                }
            }
        }
        catch
        {
            // ignore
        }

        return (false, null);
    }

    private static bool TryParseWarVersion(string packageFolderName, int major, out string label)
    {
        label = packageFolderName;
        // e.g. Microsoft.WindowsAppRuntime.2_2.3.1.0_x64__8wekyb3d8bbwe
        // or Microsoft.WindowsAppRuntime.1.5_...
        var m = Regex.Match(
            packageFolderName,
            @"Microsoft\.WindowsAppRuntime(?:\.CBS)?(?:\.(\d+))?(?:_(\d+(?:\.\d+)*))?",
            RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return false;
        }

        if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out var maj) && maj == major)
        {
            label = m.Groups[2].Success ? $"{maj} ({m.Groups[2].Value})" : maj.ToString();
            return true;
        }

        // Some names are Microsoft.WindowsAppRuntime.1.8_...
        var m2 = Regex.Match(packageFolderName, @"Microsoft\.WindowsAppRuntime\.(\d+)\.(\d+)", RegexOptions.IgnoreCase);
        if (m2.Success && int.TryParse(m2.Groups[1].Value, out var maj2) && maj2 == major)
        {
            label = $"{maj2}.{m2.Groups[2].Value}";
            return true;
        }

        // Accept any WindowsAppRuntime.2_* as major 2
        if (packageFolderName.Contains($"WindowsAppRuntime.{major}", StringComparison.OrdinalIgnoreCase)
            || packageFolderName.Contains($"WindowsAppRuntime.CBS.{major}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int? TryGetWindowsBuild()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("CurrentBuildNumber") is string s && int.TryParse(s, out var build))
            {
                return build;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}

