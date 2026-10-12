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

## Versioning & signing

Version and signing live in `SilentRunner.csproj`:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<InformationalVersion>1.0.0</InformationalVersion>
<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
<AssemblyOriginatorKeyFile>..\Claves\capicua.snk</AssemblyOriginatorKeyFile>
<SignAssembly Condition="Exists('$(AssemblyOriginatorKeyFile)')">true</SignAssembly>
```

- `IncludeSourceRevisionInInformationalVersion=false` keeps Windows' "Product version" as a clean `1.0.0`; without it .NET appends `+<commit sha>`.
- **The strong-name key is never committed.** It lives outside the repository, one level up (`C:\Proyectos\Claves\capicua.snk` for the canonical checkout), and `*.snk` / `*.pfx` are gitignored.
- The `Exists(...)` condition means a clone without the key still builds — it just produces an **unsigned** assembly instead of failing. Verified: overriding the key path to a missing file and forcing a rebuild yields a working, unsigned binary.
- Consequence to watch: moving the repo so it no longer sits beside `Claves\` silently drops the signature, with no build error. Check the public key token before shipping a release:

```powershell
[System.Reflection.AssemblyName]::GetAssemblyName("bin\Release\net10.0-windows\SilentRunner.dll").FullName
# expected token: a313609336f4f9ff
```

The token is **not** readable from the single-file published `.exe` — the managed assembly is embedded in the bundle, so `GetAssemblyName` throws there. Verify on the intermediate `.dll` instead.

Strong-naming is not Authenticode: it gives the assembly an identity, but does nothing for SmartScreen. The published `.exe` is unsigned in the code-signing sense, and the release notes say so.

## Release process

Bumping a version means editing the four version properties above, updating `CHANGELOG.md`, and then:

```bash
# 1. Everything committed on a release/vX.Y.Z branch, docs included.
# 2. Merge into main with a merge commit, then tag main.
git switch main && git merge --no-ff release/vX.Y.Z -m "Release vX.Y.Z"
git tag -a vX.Y.Z -m "SilentRunner vX.Y.Z"

# 3. Merge back into develop so the release commits are not stranded.
git switch develop && git merge --no-ff release/vX.Y.Z

# 4. Push branches and tag.
git push origin main develop vX.Y.Z

# 5. Build the release artifact (framework-dependent single file).
dotnet publish SilentRunner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o publish/fx

# 6. Package exe + example config + README + LICENSE, add SHA256SUMS.txt, publish.
gh release create vX.Y.Z --title "SilentRunner vX.Y.Z" --notes-file <notes> <assets>
```

Releases ship the **framework-dependent** single-file build (~1.8 MB, requires .NET 10 on the target), not the self-contained one. `PublishReadyToRun` is what makes it ~1.8 MB rather than a few hundred KB. Pass the publish flags explicitly rather than relying on `Properties/PublishProfiles/FolderProfile.pubxml`, so the command is reproducible.
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
| `CHANGELOG.md` | Release notes per version — the source for `gh release --notes-file` |
| `AcercaDe.cs` | The only window the program opens: «Acerca de», built by hand with no designer. Also holds the two outbound URLs |
| `ComprobadorDeVersion.cs` | Asks GitHub for the latest release, compares it with the running version and downloads the ZIP on demand. Never replaces the running exe |
| `MenuIconos.cs` | GDI+ drawn icons for every tray menu item (cached, never disposed — they are shared across menu rebuilds) |
| `SilentRunner.ico` | Tray/app icon (ghost on dark background) — also embedded as resource |

**Tray menu structure** (rebuilt on every right-click via `ContextMenuStrip.Opening`):
- `Iniciar todos` / `Detener todos` (disabled if inapplicable)
- Per task: `TaskName` with a filled green dot (running) or hollow grey ring (stopped), submenu → Iniciar/Detener, `Abrir log`, `Borrar log` (both disabled if log file doesn't exist)
- `Abrir log del launcher` — opens `<ExeName>.log` directly (disabled if not yet created)
- `Abrir carpeta de logs` — opens the log directory in Explorer
- `Limpiar logs` — truncates all `.log` files in the log directory
- `Hay una versión nueva: vX.Y.Z` — only when the version check found one; opens the About window
- `Acerca de SilentRunner` — opens the About window (see below)
- `Invitar a un café` — opens the PayPal donation link in the default browser
- `Salir` — stops all tasks and exits

Every item carries an `Image` from `MenuIconos`, not an emoji in its text: `ToolStripItem` labels are drawn by GDI, which renders emoji monochrome. The icons are cached statics — `ToolStripItem.Dispose()` does **not** dispose an item's `Image` (verified), so the menu rebuild must not dispose them either.

**The About window** (`AcercaDe.cs`) is the **only** window the program has, and it exists because
a tray menu has nowhere to say what version is running or who answers for it. It follows the shape
of the «Acerca de» screens in the house's Android apps — identity block, a bordered card per
subject (the project, the liability notice, the donation) and the licence line at the bottom —
rendered with Windows Forms controls instead of Material cards, which is why it is laid out with
nested `FlowLayoutPanel`s and `AutoSize` rather than fixed coordinates: the text wraps and the
window grows to fit it at any DPI.

Three things about it are deliberate:

- **It owns the two outbound URLs.** `UrlProyecto` and `UrlDonacion` are `internal const` here and
  the tray menu reads them from this class, instead of `TrayApp` keeping its own copy as it did.
  This is the screen that signs the program, exactly as `AboutFragment` holds them in the Android
  apps.
- **It is not modal, and there is only ever one.** A static `_abierta` brings the open window to
  the front instead of stacking a second one, and being non-modal keeps the tray usable — a task
  can still be stopped or a log opened while it is up.
- **Version and icon are not typed out.** The version comes from
  `AssemblyInformationalVersionAttribute` (the `.csproj` is the single source, and
  `IncludeSourceRevisionInInformationalVersion=false` is what keeps it a clean `1.0.0`), and the
  icon from `TrayApp.CargarIcono()`, which became `internal` for this. Each one takes its **own**
  `Icon` instance: the tray's lives as long as the process, so the window must not dispose it.
  Everything the window creates — the two scaled bitmaps and the fonts — is tracked and disposed
  with it, but `MenuIconos.Corazon` is only ever **copied**, never handed over, because those
  icons are shared statics.

**The version check** (`ComprobadorDeVersion.cs`, on by default, `checkForUpdates: false` turns it
off) asks `api.github.com` for the latest release, compares the tag with the assembly version and —
if the published one wins — sets `ComprobadorDeVersion.Conocida`, which is what the tray menu and
the About window both read. `TrayApp` drives it with a **Windows Forms** timer (first tick 30 s
after start so it never delays launching the tasks, then every six hours; the checker itself
enforces a 24-hour minimum), and that choice matters: its `Tick` arrives on the UI thread, the only
one allowed to touch the `NotifyIcon`. A balloon tip announces it once, and the menu grows a «Hay
una versión nueva» entry that opens the About window.

Four things that are deliberate, and will look wrong if you change them without reading this:

- **It compares numbers, never strings.** `"1.10.0" < "1.9.0"` as text, which would silently stop
  the notice at the tenth release. `Interpretar` strips a leading `v` and anything after `-`/`+`
  and hands the rest to `System.Version`.
- **One check a day, with `If-None-Match`.** The unauthenticated API allows 60 requests per hour
  per IP and a 304 does not count against it. The *Buscar ahora* button passes `forzar: true`,
  because someone who pressed it wants today's answer. The timestamp and the ETag live in
  `%LOCALAPPDATA%\SilentRunner\actualizacion.json`, **not** beside the exe: the usual install is
  a read-only tools folder, and failing to write this can never stop the program from starting.
- **Silence on failure, but not for the person who asked.** `Buscar` returns `null` both for «you
  are up to date» and for «could not ask», which is right for the background check — both mean say
  nothing — and wrong for the button, so `UltimoProblema` carries the reason for the window to show.
  A 404 is not a failure: it means the repo has no releases yet.
- **It does not update anything.** The window downloads the ZIP to `%TEMP%`, verifies it against the
  release's `SHA256SUMS.txt` (deleting the file if the hash does not match) and opens Explorer with
  it selected. Replacing the running exe is left to a person, for the reasons written at the top of
  `ComprobadorDeVersion.cs`: the Job Object kills the children on exit, SmartScreen re-prompts on
  an unsigned binary, and an unsigned self-replacing executable is an antivirus heuristic.

**`TaskController`** has three execution modes determined by `TaskEntry` flags:
- `AutoRestart=true` — background thread loops: launch → `WaitForExit()` → sleep `RestartDelayMs` → repeat. `Stop()` cancels immediately via `CancellationTokenSource`; cancellable sleep uses `ct.WaitHandle.WaitOne(ms)`.
- `WaitForExit=true` (no AutoRestart) — background thread launches once, waits (optionally bounded by `TimeoutMs`), kills on timeout, then sets `IsRunning=false`.
- Neither flag — fire-and-forget: `Process.Start()` on the calling thread, no monitoring.

**`RollingLog`** uses a per-file lock dictionary (`_locks`) so concurrent tasks writing to different log files don't serialize unnecessarily. Rotation trims bytes from the start of the file up to the next newline boundary — no backup files are created.

**JSON deserialization** uses `System.Text.Json` with `PropertyNameCaseInsensitive=true`, comments skipped, and trailing commas allowed, so the example JSON with `//` comments is valid input.

## Config file convention

The config file must share the executable's base name and sit in the same folder. Renaming the `.exe` requires renaming the `.json` accordingly. Use `SilentRunner.example.json` as the starting template.
