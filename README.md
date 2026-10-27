# IcoGenerator

Windows-only tool that converts PNG / JPG / WebP / BMP / GIF / TIFF (and other ImageSharp formats) into **multi-resolution `.ico`** files.

| Project | Role |
|---------|------|
| `IcoGenerator.Core` | Conversion library (shared) |
| `IcoGenerator.Cli` | PowerShell / CMD friendly console (`IcoGenerator.exe`) |
| `IcoGenerator.App` | WinUI 3 desktop GUI |

- **Target:** .NET 10 (`net10.0` / `net10.0-windows…`)
- **Solution:** `IcoGenerator.slnx`
- **Image library:** SixLabors.ImageSharp **3.1.x** (Apache-2.0; v4 requires a commercial license key)

---

## Prerequisites

- Windows 10/11 x64 (or arm64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell) for repo scripts
- Visual Studio 2022/2026 with WinUI / Windows App SDK workloads (for GUI development)

Check:

```powershell
dotnet --version   # expect 10.x
pwsh -v            # expect 7.x
```

---

## Quick start (PowerShell 7)

All scripts live under `scripts/`. Root `build.ps1` is a thin dispatcher.

```powershell
cd D:\programming\NetProjects\WINUI\IcoGenerator

# Build (Release)
pwsh ./build.ps1
# same as:
pwsh ./scripts/Build.ps1

# Clean
pwsh ./build.ps1 clean
pwsh ./build.ps1 clean -All          # also delete bin/obj/artifacts

# Smoke test (build + convert sample → artifacts/test)
pwsh ./build.ps1 test

# Publish CLI (single-file → artifacts/cli/<rid>/IcoGenerator.exe)
pwsh ./build.ps1 publish
pwsh ./build.ps1 publish -Target Cli -SelfContained -Runtime win-x64

# Publish GUI as well
pwsh ./build.ps1 publish -Target All -Runtime win-x64

# Run
pwsh ./build.ps1 run-cli -- .\logo.png -o .\app.ico -s 16,32,48,256
pwsh ./build.ps1 run-app
```

### Script map

| Script | Purpose |
|--------|---------|
| `scripts/Common.ps1` | Shared helpers (repo root, dotnet wrapper, RID) |
| `scripts/Build.ps1` | `dotnet restore` + `build` |
| `scripts/Clean.ps1` | `dotnet clean` (+ optional wipe) |
| `scripts/Publish.ps1` | Publish Cli / App / All → `artifacts/` |
| `scripts/Test.ps1` | CLI smoke test + ICO header check |
| `scripts/Run-Cli.ps1` | `dotnet run` CLI with forwarded args |
| `scripts/Run-App.ps1` | `dotnet run` WinUI app |
| `build.ps1` | Root entry: `build \| clean \| publish \| test \| run-cli \| run-app` |

---

## CLI usage

After publish:

```powershell
.\artifacts\cli\win-x64\IcoGenerator.exe .\logo.png -o .\app.ico -s 16,32,48,64,128,256
```

Or via `dotnet run`:

```powershell
dotnet run --project .\IcoGenerator.Cli -- .\logo.png -o .\app.ico
```

| Argument / option | Description |
|-------------------|-------------|
| `input` | Source image path |
| `-o`, `--output` | Output `.ico` (default: same name as input) |
| `-s`, `--sizes` | Comma-separated sizes 1–256 (default: 16,32,48,64,128,256) |
| `--overwrite` / `--no-overwrite` | Overwrite policy |

Exit codes: `0` success, `1` conversion error, `2` bad args / missing input.

---

## Maintainability layout

```
IcoGenerator/
├── IcoGenerator.slnx              # solution
├── Directory.Build.props          # shared version, nullable, lang
├── Directory.Packages.props       # central NuGet versions (CPM)
├── global.json                    # SDK pin / roll-forward
├── build.ps1                      # PS7 task launcher
├── scripts/                       # build / publish / test
├── artifacts/                     # publish + smoke outputs (gitignored)
├── IcoGenerator.Core/             # pure library
├── IcoGenerator.Cli/              # console host
└── IcoGenerator.App/              # WinUI 3 host
```

### Where to change what

| Want to change… | Edit |
|-----------------|------|
| Product version `0.1.0` | `Directory.Build.props` |
| NuGet package versions | `Directory.Packages.props` only |
| Default TFM / nullable / analysis | `Directory.Build.props` |
| Required SDK band | `global.json` |
| Build / publish behavior | `scripts/*.ps1` |
| Conversion algorithm / ICO packing | `IcoGenerator.Core` |
| CLI flags | `IcoGenerator.Cli/Program.cs` |
| GUI | `IcoGenerator.App/MainPage.*` |

Projects use **Central Package Management**: `PackageReference` has no `Version=` attribute; versions live only in `Directory.Packages.props`.

---

## Architecture notes

```
┌─────────────────┐     ┌─────────────────┐
│ IcoGenerator.App│     │ IcoGenerator.Cli│
│   (WinUI 3)     │     │  (console exe)  │
└────────┬────────┘     └────────┬────────┘
         │                       │
         └───────────┬───────────┘
                     ▼
            ┌─────────────────┐
            │ IcoGenerator.Core│
            │  IcoConverter    │
            └─────────────────┘
```

- Core writes **PNG-in-ICO** multi-size icons (Windows Vista+).
- CLI is a real console subsystem (clean exit codes / stdout for scripting).
- GUI and CLI share one conversion implementation.

---

## Visual Studio

1. Open `IcoGenerator.slnx` in VS 2026 (or 2022 with .NET 10 support).
2. Set startup project to `IcoGenerator.App` or `IcoGenerator.Cli`.
3. F5 to debug.

Scripts and VS builds both honor the same `Directory.*.props` settings.
