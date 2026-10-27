namespace IcoGenerator.Core;

/// <summary>
/// Result of a successful ICO conversion.
/// </summary>
public sealed class IcoConversionResult
{
    /// <summary>Source image path.</summary>
    public required string InputPath { get; init; }

    /// <summary>Main multi-resolution .ico path (all selected sizes in one file).</summary>
    public required string OutputPath { get; init; }

    /// <summary>Sizes embedded in the main multi-resolution .ico.</summary>
    public required IReadOnlyList<int> Sizes { get; init; }

    /// <summary>Byte length of the main multi-resolution .ico.</summary>
    public long OutputBytes { get; init; }

    /// <summary>
    /// Per-size single-image .ico files (empty when <see cref="IcoConversionOptions.WriteSeparateFilesPerSize"/> is false).
    /// Key = size, Value = full path.
    /// </summary>
    public IReadOnlyDictionary<int, string> SeparateFiles { get; init; } =
        new Dictionary<int, string>();

    /// <summary>Total number of .ico files written (main + separate).</summary>
    public int FileCount => 1 + SeparateFiles.Count;
}
