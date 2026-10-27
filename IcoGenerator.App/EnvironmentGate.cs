using IcoGenerator.Core.Environment;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace IcoGenerator_App;

/// <summary>
/// Runs a machine environment scan at startup and surfaces missing components in a dialog.
/// </summary>
internal static class EnvironmentGate
{
    private static bool _ran;

    /// <summary>
    /// Scan once per process. Shows a ContentDialog when anything is missing.
    /// </summary>
    public static async Task RunAsync(XamlRoot xamlRoot)
    {
        if (_ran || xamlRoot is null)
        {
            return;
        }

        _ran = true;

        EnvironmentReport report;
        try
        {
            // Self-contained single-file: runtime is inside the exe; scan is informational.
            report = RuntimeEnvironmentProbe.Scan(new EnvironmentProbeOptions
            {
                RequireDotNetRuntime = false,
                RequireWindowsDesktopRuntime = false,
                RequireWindowsAppRuntime = false
            });
        }
        catch (Exception ex)
        {
            report = new EnvironmentReport
            {
                Items =
                [
                    new EnvironmentCheckItem
                    {
                        Id = "probe-error",
                        DisplayName = "Environment probe",
                        Description = $"Environment scan failed: {ex.Message}",
                        IsPresent = false,
                        IsRequired = false,
                        InstallHint = "Retry after restarting the app."
                    }
                ]
            };
        }

        if (!report.HasMissing)
        {
            return;
        }

        await ShowMissingDialogAsync(xamlRoot, report).ConfigureAwait(true);
    }

    private static async Task ShowMissingDialogAsync(XamlRoot xamlRoot, EnvironmentReport report)
    {
        var missing = report.Missing.ToList();
        var lines = string.Join(
            Environment.NewLine + Environment.NewLine,
            missing.Select(m =>
            {
                var req = m.IsRequired ? "[REQUIRED] " : "[Recommended] ";
                var hint = string.IsNullOrWhiteSpace(m.InstallHint) ? "" : $"{Environment.NewLine}  → {m.InstallHint}";
                var url = string.IsNullOrWhiteSpace(m.InstallUrl) ? "" : $"{Environment.NewLine}  → {m.InstallUrl}";
                return $"{req}{m.DisplayName}{Environment.NewLine}  {m.Description}{hint}{url}";
            }));

        var scroll = new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = "The following components were not detected on this PC:"
                      + Environment.NewLine + Environment.NewLine
                      + lines
                      + Environment.NewLine + Environment.NewLine
                      + "Install the missing component(s), then restart IcoGenerator."
                      + Environment.NewLine
                      + "This build is a self-contained single .exe (runtime packed inside).",
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            },
            MaxHeight = 360
        };

        var dialog = new ContentDialog
        {
            Title = report.HasMissingRequired
                ? "Missing required environment"
                : "Missing recommended environment",
            Content = scroll,
            PrimaryButtonText = "Open download page",
            CloseButtonText = "Continue",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var url = missing
                          .Select(m => m.InstallUrl)
                          .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
                      ?? RuntimeEnvironmentProbe.DotNetRuntimeDownloadUrl;
            try
            {
                await Launcher.LaunchUriAsync(new Uri(url));
            }
            catch
            {
                // User can copy the URL from the dialog text.
            }
        }
    }
}
