using IcoGenerator.Core.Cli;

// Pure console host (optional). Prefer the unified IcoGenerator.App / published IcoGenerator.exe.
return await CommandLineApp.RunAsync(
    args,
    unifiedHost: false,
    requireDotNetRuntime: true);
