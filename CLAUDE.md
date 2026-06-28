# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## License

MIT — see `LICENSE`.

## Build & publish

```bash
# Build (Debug)
dotnet build

# Build (Release)
dotnet build -c Release

# Publish as framework-dependent EXE
dotnet publish -c Release

# Publish as self-contained single-file EXE (no .NET required on target)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Target: `net10.0-windows`, `OutputType=WinExe` (no console window). No tests exist in this repo.

## Architecture

SilentRunner is a Windows-only, windowless process launcher. It reads a JSON config file named identically to the executable (`SilentRunner.exe` → `SilentRunner.json`) and launches each task invisibly.

**Flow** (`Program.cs` → `SilentLauncher.Run()`):

1. Locate `<ExeName>.json` beside the executable (missing config → empty tray, still runs).
2. Deserialize into `LaunchConfig` (root settings + `List<TaskEntry>`).
3. Resolve log directory (`logDirectory` or exe folder).
4. Create a `ProcessJob` (Windows Job Object — kills all children on launcher exit).
5. Build one `TaskController` per `TaskEntry`; apply `initialDelayMs`; call `Start()` on each with `delayAfterMs` between them.
6. Open the system tray icon (`TrayApp`) and start `Application.Run()` — the launcher stays alive indefinitely.
7. On exit (user clicks **Salir**): `TrayApp` stops all controllers, the `ProcessJob` handle closes, Windows kills any remaining children.

**Key files:**

| File | Role |
|---|---|
| `SilentLauncher.cs` | Bootstrapper: loads config, wires `TaskController`s, runs `Application.Run()` |
| `TaskController.cs` | Per-task lifecycle: `Start()` / `Stop()`, auto-restart loop, WaitForExit support |
| `TrayApp.cs` | `ApplicationContext` subclass with `NotifyIcon` and context menu |
| `LaunchConfig.cs` | Config model (`LaunchConfig`, `TaskEntry`) |
| `RollingLog.cs` | Thread-safe, in-place rotating log writer |
| `Program.cs` | Entry point with `[STAThread]` — single call to `SilentLauncher.Run()` |
| `ProcessJob.cs` | Windows Job Object wrapper (kill-on-close) |
| `SilentRunner.example.json` | Annotated config template |
| `SilentRunner.ico` | Tray/app icon (ghost on dark background) — also embedded as resource |

**Tray menu structure** (rebuilt on every right-click via `ContextMenuStrip.Opening`):
- `▶ Iniciar todos` / `⏹ Detener todos` (disabled if inapplicable)
- Per task: `● TaskName` (running) or `○ TaskName` (stopped), submenu → Iniciar/Detener, `📄 Abrir log`, `🗑 Borrar log` (both disabled if log file doesn't exist)
- `📄 Abrir log del launcher` — opens `<ExeName>.log` directly (disabled if not yet created)
- `Abrir carpeta de logs` — opens the log directory in Explorer
- `Limpiar logs` — truncates all `.log` files in the log directory
- `Salir` — stops all tasks and exits

**`TaskController`** has three execution modes determined by `TaskEntry` flags:
- `AutoRestart=true` — background thread loops: launch → `WaitForExit()` → sleep `RestartDelayMs` → repeat. `Stop()` cancels immediately via `CancellationTokenSource`; cancellable sleep uses `ct.WaitHandle.WaitOne(ms)`.
- `WaitForExit=true` (no AutoRestart) — background thread launches once, waits (optionally bounded by `TimeoutMs`), kills on timeout, then sets `IsRunning=false`.
- Neither flag — fire-and-forget: `Process.Start()` on the calling thread, no monitoring.

**`RollingLog`** uses a per-file lock dictionary (`_locks`) so concurrent tasks writing to different log files don't serialize unnecessarily. Rotation trims bytes from the start of the file up to the next newline boundary — no backup files are created.

**JSON deserialization** uses `System.Text.Json` with `PropertyNameCaseInsensitive=true`, comments skipped, and trailing commas allowed, so the example JSON with `//` comments is valid input.

## Config file convention

The config file must share the executable's base name and sit in the same folder. Renaming the `.exe` requires renaming the `.json` accordingly. Use `SilentRunner.example.json` as the starting template.
