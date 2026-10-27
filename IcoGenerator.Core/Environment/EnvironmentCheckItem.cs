namespace IcoGenerator.Core.Environment;

/// <summary>
/// One dependency checked during environment scan.
/// </summary>
public sealed class EnvironmentCheckItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public bool IsPresent { get; init; }
    public bool IsRequired { get; init; }
    public string? DetectedVersion { get; init; }
    public string? InstallUrl { get; init; }
    public string? InstallHint { get; init; }

    public string StatusText => IsPresent
        ? (string.IsNullOrEmpty(DetectedVersion) ? "OK" : $"OK ({DetectedVersion})")
        : "MISSING";
}
