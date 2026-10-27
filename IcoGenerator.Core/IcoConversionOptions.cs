namespace IcoGenerator.Core;

/// <summary>
/// Options for converting a source image into Windows .ico file(s).
/// </summary>
public sealed class IcoConversionOptions
{
    /// <summary>
    /// Default Windows-friendly icon sizes.
    /// </summary>
    public static IReadOnlyList<int> DefaultSizes { get; } = [16, 32, 48, 64, 128, 256];

    /// <summary>
    /// Pixel sizes to generate (1-256 inclusive).
    /// </summary>
    public IReadOnlyList<int> Sizes { get; init; } = DefaultSizes;

    /// <summary>
    /// When true, overwrites existing output files.
    /// </summary>
    public bool Overwrite { get; init; } = true;

    /// <summary>
    /// When true (default), also writes one single-size .ico per selected size
    /// next to the main multi-resolution file, named like <c>app_16.ico</c>, <c>app_32.ico</c>.
    /// The main multi-resolution file at the requested output path is always written.
    /// </summary>
    public bool WriteSeparateFilesPerSize { get; init; } = true;
}
