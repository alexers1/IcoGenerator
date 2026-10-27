# IcoGenerator — CMD / CLI User Guide

**IcoGenerator** converts raster images (PNG, JPG, WebP, BMP, GIF, TIFF, and more) into Windows **`.ico`** files.

This guide covers **command-line (CMD / PowerShell) mode only**.

---

## 1. What you need

### Published unified host (recommended product build)

| Item | Path (default after publish) |
|------|------------------------------|
| Executable | `artifacts\unified\win-x64\IcoGenerator.App.exe` |

This is a **self-contained single-file** build (GUI + CLI in one executable).

- **Do not rename** `IcoGenerator.App.exe`. Renaming can break the single-file host (GUI will fail; CLI may still work).
- If you want a nicer desktop name, create a **shortcut** and rename the shortcut only.

### Optional pure console host

| Item | Path |
|------|------|
| Executable | `artifacts\cli\win-x64\IcoGenerator.Cli.exe` |

Framework-dependent by default: the PC needs **.NET 10 Runtime** installed.

---

## 2. How mode is chosen (unified host)

When you run `IcoGenerator.App.exe`:

| How you start it | Mode |
|------------------|------|
| Double-click / no arguments | GUI |
| Any conversion / CLI arguments | **CLI (console)** |
| `--gui` | Force GUI |

This guide assumes **CLI mode**: you pass arguments on the command line.

---

## 3. Open a terminal

**CMD**

```bat
cd /d D:\programming\NetProjects\WINUI\IcoGenerator\artifacts\unified\win-x64
```

**PowerShell 7**

```powershell
cd D:\programming\NetProjects\WINUI\IcoGenerator\artifacts\unified\win-x64
```

Use the folder that contains your published exe.

---

## 4. Basic syntax

```text
IcoGenerator.App.exe <input> [options]
```

| Part | Meaning |
|------|---------|
| `<input>` | Path to the source image |
| `-o`, `--output` | Output multi-resolution `.ico` path (default: same name as input, `.ico`) |
| `-s`, `--sizes` | Sizes to generate (see below) |
| `--separate` | Also write one `.ico` per size (default: **on**) |
| `--no-separate` | Only write the multi-resolution `.ico` |
| `--overwrite` | Overwrite existing files (default: **on**) |
| `--no-overwrite` | Fail if an output file already exists |
| `--skip-env-check` | Skip environment scan |
| `-h`, `--help` | Help |
| `--version` | Version |

**Pure CLI host** uses the same options; replace the exe name with `IcoGenerator.Cli.exe`.

---

## 5. Output files

By default the tool writes:

1. **Multi-resolution ICO** — one file containing all selected sizes  
   Example: `app.ico` with 16, 32, 48, 64, 128, 256 inside.

2. **Per-size ICOs** (when `--separate` is on, which is the default)  
   Example next to `app.ico`:
   - `app_16.ico`
   - `app_32.ico`
   - `app_48.ico`
   - …

| Flag | Result |
|------|--------|
| (default) | Multi + per-size files |
| `--no-separate` | Multi file only |

---

## 6. Sizes (`-s` / `--sizes`)

- Allowed range: **1–256** pixels (square icons).
- Default: `16,32,48,64,128,256`
- Separators: comma, space, semicolon, or `|`
- Also accepted: `32px`, `32x32`

**Examples**

```bat
IcoGenerator.App.exe logo.png -s 16,32,48,256
IcoGenerator.App.exe logo.png -s "16 24 40 128"
IcoGenerator.App.exe logo.png -s 16;32;64
```

---

## 7. Examples (CMD)

Assume the current directory is the publish folder, and `logo.png` is in the current directory.

### 7.1 Default conversion

Creates `logo.ico` plus `logo_16.ico`, `logo_32.ico`, … (default sizes):

```bat
IcoGenerator.App.exe logo.png
```

### 7.2 Choose output path

```bat
IcoGenerator.App.exe logo.png -o app.ico
```

### 7.3 Custom sizes

```bat
IcoGenerator.App.exe logo.png -o app.ico -s 16,32,48,64,128,256
```

### 7.4 Multi-resolution only (no per-size files)

```bat
IcoGenerator.App.exe logo.png -o app.ico -s 16,32,48,256 --no-separate
```

### 7.5 WebP source

```bat
IcoGenerator.App.exe photo.webp -o icon.ico -s 16,32,48,256
```

### 7.6 Full path (spaces in path)

```bat
IcoGenerator.App.exe "C:\Users\You\Pictures\my logo.png" -o "C:\Users\You\Desktop\app.ico"
```

### 7.7 Do not overwrite existing files

```bat
IcoGenerator.App.exe logo.png -o app.ico --no-overwrite
```

### 7.8 Skip environment check

```bat
IcoGenerator.App.exe logo.png -o app.ico --skip-env-check
```

### 7.9 Help

```bat
IcoGenerator.App.exe --help
```

---

## 8. Examples (PowerShell 7)

Call operator is recommended when the path has spaces:

```powershell
& .\IcoGenerator.App.exe .\logo.png -o .\app.ico -s 16,32,48,256

& .\IcoGenerator.App.exe "C:\Users\You\Pictures\logo.png" -o "$env:USERPROFILE\Desktop\app.ico"
```

---

## 9. Supported input formats

Common extensions handled by the decoder (ImageSharp), including:

- `.png`, `.jpg` / `.jpeg`
- `.webp`
- `.bmp`, `.gif`
- `.tif` / `.tiff`
- and other formats supported by the library

If the extension is unusual, the tool may still try to decode; failures are reported as errors.

**Tips for best icons**

- Prefer a **square** source, ideally **≥ 256×256**.
- Prefer **PNG** (or WebP) **with transparency** when you need alpha in the icon.

---

## 10. Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Conversion failed (I/O, decode, write, …) |
| `2` | Bad arguments or input file not found |
| `3` | Required environment missing (mainly pure CLI FDD builds) |

**CMD**

```bat
IcoGenerator.App.exe logo.png -o app.ico
echo Exit code: %ERRORLEVEL%
```

**PowerShell**

```powershell
& .\IcoGenerator.App.exe .\logo.png -o .\app.ico
echo "Exit code: $LASTEXITCODE"
```

---

## 11. Sample CLI output

```text
IcoGenerator v0.1.0
OK  C:\path\logo.png
 -> multi: C:\path\app.ico
    sizes: 16, 32, 48, 256
    multi bytes: 12345
    separate files (4):
      16px -> C:\path\app_16.ico
      32px -> C:\path\app_32.ico
      48px -> C:\path\app_48.ico
      256px -> C:\path\app_256.ico
    total files: 5
```

---

## 12. Publishing the single-file build (developers)

From the repository root (PowerShell 7):

```powershell
cd D:\programming\NetProjects\WINUI\IcoGenerator
pwsh ./scripts/Publish.ps1 -Target Unified -Runtime win-x64
```

Output:

```text
artifacts\unified\win-x64\IcoGenerator.App.exe
```

Optional pure CLI (smaller, usually needs .NET 10 on the machine):

```powershell
pwsh ./scripts/Publish.ps1 -Target Cli -Runtime win-x64
```

---

## 13. Troubleshooting (CMD mode)

| Problem | What to try |
|---------|-------------|
| “No reaction” when double-clicking | That opens **GUI**. For CLI, always pass arguments from CMD/PowerShell. |
| GUI broken after renaming the exe | Restore the original name (`IcoGenerator.App.exe`). Do not rename the single-file host. |
| `input file not found` | Use a correct path; quote paths that contain spaces or `&`. |
| `Invalid size value` | Use integers 1–256, e.g. `16,32,48,256`. |
| Access denied / cannot write output | Choose an output folder you can write to; close programs locking the `.ico`. |
| Pure CLI fails with environment error | Install [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), or use the unified self-contained `IcoGenerator.App.exe`. |

---

## 14. Quick reference

```bat
REM Multi + per-size (default sizes)
IcoGenerator.App.exe logo.png

REM Multi + per-size, custom sizes and name
IcoGenerator.App.exe logo.png -o app.ico -s 16,32,48,64,128,256

REM Multi only
IcoGenerator.App.exe logo.png -o app.ico --no-separate

REM WebP
IcoGenerator.App.exe photo.webp -o icon.ico -s 16,32,256
```

---

*IcoGenerator — Windows image → multi-size / per-size `.ico` converter (CMD / CLI).*
