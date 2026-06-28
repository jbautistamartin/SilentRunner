// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Diagnostics;

namespace SilentRunner;

/// <summary>
/// Gestiona el ciclo de vida de una única tarea: arranque, parada y reinicio automático.
/// </summary>
internal sealed class TaskController
{
    private readonly TaskEntry _task;
    private readonly string _exeDir;
    private readonly string _dirLogs;
    private readonly string _logLauncher;
    private readonly long _maxBytes;
    private readonly ProcessJob _job;

    private volatile Process? _process;
    private CancellationTokenSource? _cts;
    private Thread? _hilo; // hilo de bucle (solo tareas AutoRestart o WaitForExit)

    public string Name => string.IsNullOrWhiteSpace(_task.Name) ? _task.Executable : _task.Name;
    public bool IsRunning { get; private set; }
    public int DelayAfterMs => _task.DelayAfterMs;
    public string LogPath { get; }

    internal TaskController(
        TaskEntry task, string exeDir, string dirLogs,
        string logLauncher, long maxBytes, ProcessJob job)
    {
        _task = task;
        _exeDir = exeDir;
        _dirLogs = dirLogs;
        _logLauncher = logLauncher;
        _maxBytes = maxBytes;
        _job = job;

        string baseName = Path.GetFileNameWithoutExtension(
            Environment.ExpandEnvironmentVariables(task.Executable));
        LogPath = Path.Combine(dirLogs, baseName + ".log");
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    internal void Start()
    {
        if (IsRunning) return;
        IsRunning = true;

        var cts = new CancellationTokenSource();
        _cts = cts;

        if (_task.AutoRestart)
        {
            _hilo = new Thread(() => BucleAutoRestart(cts)) { IsBackground = true, Name = $"SR-{Name}" };
            _hilo.Start();
        }
        else if (_task.WaitForExit)
        {
            _hilo = new Thread(() => EjecutarConEspera(cts)) { IsBackground = true, Name = $"SR-{Name}" };
            _hilo.Start();
        }
        else
        {
            // Fire-and-forget: lanzar y no esperar.
            try { _process = IniciarProceso(); }
            catch (Exception ex)
            {
                RollingLog.Write(_logLauncher, $"[ERROR] Tarea '{Name}': {ex.Message}", _maxBytes);
                IsRunning = false;
            }
        }
    }

    internal void Stop()
    {
        IsRunning = false;

        var cts = _cts;
        _cts = null;
        cts?.Cancel();

        MatarProceso();

        // Dar tiempo al hilo de fondo para que salga limpiamente.
        if (_hilo is { } h)
        {
            h.Join(3_000);
            _hilo = null;
        }

        cts?.Dispose();
    }

    // ── Bucles de fondo ───────────────────────────────────────────────────────

    private void BucleAutoRestart(CancellationTokenSource cts)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            Process? p = null;
            try
            {
                p = IniciarProceso();
                _process = p;

                if (p is null)
                {
                    // No se pudo iniciar; esperar antes de reintentar.
                    EsperarInterrumpible(Math.Max(_task.RestartDelayMs, 1_000), cts.Token);
                    continue;
                }

                // Bloquea hasta que el proceso termine (o sea matado por Stop()).
                p.WaitForExit();
            }
            catch (Exception ex) when (!cts.Token.IsCancellationRequested)
            {
                RollingLog.Write(_logLauncher, $"[ERROR] Auto-reinicio '{Name}': {ex.Message}", _maxBytes);
            }
            finally
            {
                // Siempre limpiar el proceso antes de reintentar o salir.
                var viejo = Interlocked.Exchange(ref _process, null);
                if (viejo != null)
                {
                    try
                    {
                        // Drenar stdout/stderr asíncronos pendientes.
                        if (_task.CaptureOutput) viejo.WaitForExit();
                    }
                    catch { }
                    viejo.Dispose();
                }
            }

            if (cts.Token.IsCancellationRequested) break;

            RollingLog.Write(_logLauncher,
                $"[REINICIO] '{Name}' terminó — reiniciando en {_task.RestartDelayMs} ms", _maxBytes);

            EsperarInterrumpible(_task.RestartDelayMs, cts.Token);
        }
        // IsRunning lo gestiona Stop(); no lo tocamos aquí.
    }

    private void EjecutarConEspera(CancellationTokenSource cts)
    {
        Process? p = null;
        try
        {
            p = IniciarProceso();
            _process = p;
            if (p is null) return;

            bool terminado;
            if (_task.TimeoutMs > 0)
            {
                terminado = p.WaitForExit(_task.TimeoutMs);
                if (!terminado)
                {
                    RollingLog.Write(_logLauncher,
                        $"[TIMEOUT] Tarea '{Name}' superó {_task.TimeoutMs} ms — terminando proceso", _maxBytes);
                    try { p.Kill(entireProcessTree: true); } catch { }
                }
            }
            else
            {
                p.WaitForExit(); // espera infinita (interrumpible mediante Kill() desde Stop())
            }

            // Drenar los buffers de salida asíncronos.
            if (_task.CaptureOutput) p.WaitForExit();

            if (!cts.Token.IsCancellationRequested)
                RollingLog.Write(_logLauncher, $"[FIN {p.ExitCode}] '{Name}'", _maxBytes);
        }
        catch (Exception ex) when (!cts.Token.IsCancellationRequested)
        {
            RollingLog.Write(_logLauncher, $"[ERROR] Tarea '{Name}': {ex.Message}", _maxBytes);
        }
        finally
        {
            Interlocked.Exchange(ref _process, null)?.Dispose();
            IsRunning = false; // la tarea completó su ciclo naturalmente
        }
    }

    // ── Arranque de proceso ───────────────────────────────────────────────────

    private Process? IniciarProceso()
    {
        string exe = Environment.ExpandEnvironmentVariables(_task.Executable);
        string args = Environment.ExpandEnvironmentVariables(_task.Arguments);
        string dir = _task.WorkingDirectory is { Length: > 0 }
            ? Environment.ExpandEnvironmentVariables(_task.WorkingDirectory)
            : _exeDir;

        string logSalida = LogPath;
        bool capturar = _task.CaptureOutput;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = capturar,
            RedirectStandardError = capturar,
        };

        RollingLog.Write(_logLauncher,
            $"[INICIO] '{Name}'  exe={exe}  args={args}", _maxBytes);

        var p = Process.Start(psi);
        if (p is null)
        {
            RollingLog.Write(_logLauncher, $"[AVISO] No se pudo iniciar '{Name}'", _maxBytes);
            return null;
        }

        if (!_job.TryAssign(p))
            RollingLog.Write(_logLauncher,
                $"[AVISO] No se pudo asignar '{Name}' al Job Object", _maxBytes);

        if (capturar)
        {
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) RollingLog.Write(logSalida, e.Data, _maxBytes);
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) RollingLog.Write(logSalida, $"[ERR] {e.Data}", _maxBytes);
            };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }

        return p;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    private void MatarProceso()
    {
        var p = Interlocked.Exchange(ref _process, null);
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch { }
        // No disponemos aquí el proceso: el hilo propietario lo dispone en su finally.
    }

    /// <summary>
    /// Duerme <paramref name="ms"/> milisegundos pero se despierta inmediatamente si
    /// se cancela el token (Stop() fue llamado).
    /// </summary>
    private static void EsperarInterrumpible(int ms, CancellationToken ct)
    {
        if (ms <= 0 || ct.IsCancellationRequested) return;
        ct.WaitHandle.WaitOne(ms);
    }
}