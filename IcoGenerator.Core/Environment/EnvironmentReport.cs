using System.Text;

namespace IcoGenerator.Core.Environment;

/// <summary>
/// Result of scanning the machine for runtimes / OS requirements.
/// </summary>
public sealed class EnvironmentReport
{
    public required IReadOnlyList<EnvironmentCheckItem> Items { get; init; }
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.Now;

    public IEnumerable<EnvironmentCheckItem> Missing => Items.Where(i => !i.IsPresent);
    public IEnumerable<EnvironmentCheckItem> MissingRequired => Items.Where(i => i.IsRequired && !i.IsPresent);
    public bool HasMissing => Missing.Any();
    public bool HasMissingRequired => MissingRequired.Any();

    /// <summary>
    /// Human-readable multi-line summary (English, for dialogs / CLI).
    /// </summary>
    public string FormatSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Environment check");
        sb.AppendLine("-----------------");
        foreach (var item in Items)
        {
            var req = item.IsRequired ? "required" : "recommended";
            sb.AppendLine($"[{item.StatusText}] {item.DisplayName} ({req})");
            sb.AppendLine($"    {item.Description}");
            if (!item.IsPresent)
            {
                if (!string.IsNullOrWhiteSpace(item.InstallHint))
                {
                    sb.AppendLine($"    Fix: {item.InstallHint}");
                }

                if (!string.IsNullOrWhiteSpace(item.InstallUrl))
                {
                    sb.AppendLine($"    Download: {item.InstallUrl}");
                }
            }
        }

        if (HasMissingRequired)
        {
            sb.AppendLine();
            sb.AppendLine("One or more required components are missing. Install them, then restart.");
        }
        else if (HasMissing)
        {
            sb.AppendLine();
            sb.AppendLine("Some recommended components are missing. The app may still run, but CLI or optional features might not.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("All checked components look OK.");
        }

        return sb.ToString().TrimEnd();
    }
}
