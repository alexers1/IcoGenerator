using System.Buffers.Binary;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace IcoGenerator.Core;

/// <summary>
/// Converts raster images (PNG, JPEG, BMP, GIF, WebP, TIFF, etc.) into Windows .ico files.
/// Always writes a multi-resolution ICO at the requested path; optionally also writes
/// one single-size .ico per selected size (app_16.ico, app_32.ico, ...).
/// </summary>
public sealed class IcoConverter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jpe", ".jfif",
        ".bmp", ".gif", ".webp", ".tif", ".tiff",
        ".tga", ".pbm", ".pgm", ".ppm", ".qoi"
    };

    public bool IsSupportedExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    public IReadOnlyCollection<string> GetSupportedExtensions() => SupportedExtensions.ToArray();

    /// <summary>
    /// Converts <paramref name="inputPath"/> to .ico file(s) based on <paramref name="options"/>.
    /// </summary>
    public async Task<IcoConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        IcoConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        options ??= new IcoConversionOptions();
        var sizes = NormalizeSizes(options.Sizes);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Source image was not found.", inputPath);
        }

        var fullOutput = Path.GetFullPath(outputPath);
        var outputDir = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        if (File.Exists(fullOutput) && !options.Overwrite)
        {
            throw new IOException($"Output file already exists: {fullOutput}");
        }

        var separatePaths = BuildSeparatePaths(fullOutput, sizes);
        if (options.WriteSeparateFilesPerSize)
        {
            foreach (var path in separatePaths.Values)
            {
                if (File.Exists(path) && !options.Overwrite)
                {
                    throw new IOException($"Output file already exists: {path}");
                }
            }
        }

        Dictionary<int, string> writtenSeparate = new();

        // Decode + encode on a background thread — ImageSharp is CPU-bound.
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var source = Image.Load<Rgba32>(inputPath);
            var pngBlobs = new List<(int Size, byte[] Data)>(sizes.Count);

            foreach (var size in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var frame = source.Clone(ctx =>
                {
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(size, size),
                        Mode = ResizeMode.Stretch,
                        Sampler = KnownResamplers.Lanczos3,
                        PremultiplyAlpha = false
                    });
                });

                using var ms = new MemoryStream();
                frame.Save(ms, new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha,
                    CompressionLevel = PngCompressionLevel.BestCompression
                });
                pngBlobs.Add((size, ms.ToArray()));
            }

            // Main multi-resolution ICO (all selected sizes in one file).
            WriteIcoFile(fullOutput, pngBlobs);

            // Optional: one single-size ICO per checkbox / size.
            if (options.WriteSeparateFilesPerSize)
            {
                foreach (var (size, data) in pngBlobs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = separatePaths[size];
                    WriteIcoFile(path, [(size, data)]);
                    writtenSeparate[size] = path;
                }
            }
        }, cancellationToken).ConfigureAwait(false);

        var info = new FileInfo(fullOutput);
        return new IcoConversionResult
        {
            InputPath = Path.GetFullPath(inputPath),
            OutputPath = fullOutput,
            Sizes = sizes,
            OutputBytes = info.Length,
            SeparateFiles = writtenSeparate
        };
    }

    public async Task<byte[]> ConvertToBytesAsync(
        string inputPath,
        IcoConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"icogen-{Guid.NewGuid():N}.ico");
        try
        {
            // Bytes API only returns the multi-resolution blob.
            var opts = options ?? new IcoConversionOptions();
            await ConvertAsync(
                inputPath,
                temp,
                new IcoConversionOptions
                {
                    Sizes = opts.Sizes,
                    Overwrite = true,
                    WriteSeparateFilesPerSize = false
                },
                cancellationToken).ConfigureAwait(false);
            return await File.ReadAllBytesAsync(temp, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// Parses a free-form size list for CLI/UI.
    /// Accepts any integers 1-256, e.g. "16,32,48", "16 24 40", "16;32;100".
    /// Empty / whitespace returns <see cref="IcoConversionOptions.DefaultSizes"/>.
    /// </summary>
    public static IReadOnlyList<int> ParseSizes(string sizesText)
    {
        if (string.IsNullOrWhiteSpace(sizesText))
        {
            return IcoConversionOptions.DefaultSizes;
        }

        var parts = sizesText.Split(
            [',', ';', ' ', '|', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sizes = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            var token = part;
            if (token.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                token = token[..^2];
            }

            var xIndex = token.IndexOf('x', StringComparison.OrdinalIgnoreCase);
            if (xIndex > 0)
            {
                token = token[..xIndex];
            }

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            {
                throw new FormatException(
                    $"Invalid size value: '{part}'. Use custom integers from 1 to 256, e.g. 16,32,40,128,256.");
            }

            sizes.Add(size);
        }

        return NormalizeSizes(sizes);
    }

    public static IReadOnlyList<int> NormalizeSizeList(IEnumerable<int> sizes) =>
        NormalizeSizes(sizes as IReadOnlyList<int> ?? sizes.ToArray());

    /// <summary>
    /// Builds paths like C:\out\app_16.ico for a multi output C:\out\app.ico.
    /// </summary>
    public static IReadOnlyDictionary<int, string> BuildSeparatePaths(string multiOutputPath, IReadOnlyList<int> sizes)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(multiOutputPath)) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(multiOutputPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "icon";
        }

        var map = new Dictionary<int, string>(sizes.Count);
        foreach (var size in sizes)
        {
            map[size] = Path.Combine(dir, $"{baseName}_{size}.ico");
        }

        return map;
    }

    private static IReadOnlyList<int> NormalizeSizes(IReadOnlyList<int> sizes)
    {
        if (sizes is null || sizes.Count == 0)
        {
            throw new ArgumentException("At least one icon size is required.", nameof(sizes));
        }

        var normalized = sizes
            .Distinct()
            .OrderBy(s => s)
            .ToArray();

        foreach (var size in normalized)
        {
            if (size is < 1 or > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(sizes), size, "ICO image sizes must be between 1 and 256 pixels.");
            }
        }

        return normalized;
    }

    /// <summary>
    /// Writes an ICO file with PNG payloads (modern Windows format).
    /// One or more images; multi-resolution when <paramref name="images"/> has multiple entries.
    /// </summary>
    private static void WriteIcoFile(string outputPath, IReadOnlyList<(int Size, byte[] Data)> images)
    {
        const int headerSize = 6;
        const int entrySize = 16;
        var directorySize = headerSize + entrySize * images.Count;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> header = stackalloc byte[headerSize];
        BinaryPrimitives.WriteUInt16LittleEndian(header[..2], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(4, 2), (ushort)images.Count);
        fs.Write(header);

        var offset = directorySize;
        Span<byte> entry = stackalloc byte[entrySize];
        foreach (var (size, data) in images)
        {
            entry.Clear();
            entry[0] = size >= 256 ? (byte)0 : (byte)size;
            entry[1] = size >= 256 ? (byte)0 : (byte)size;
            entry[2] = 0;
            entry[3] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(4, 2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(6, 2), 32);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(8, 4), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(12, 4), (uint)offset);
            fs.Write(entry);
            offset += data.Length;
        }

        foreach (var (_, data) in images)
        {
            fs.Write(data, 0, data.Length);
        }
    }
}
