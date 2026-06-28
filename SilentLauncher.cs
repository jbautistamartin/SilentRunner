// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Text.Json;
using System.Windows.Forms;

namespace SilentRunner;

/// <summary>
/// Punto de entrada lógico de SilentRunner: carga la configuración, construye los
/// <see cref="TaskController"/> y arranca el bucle de mensajes con el icono de bandeja.
/// </summary>
internal static class SilentLauncher
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    internal static void Run()
    {
        // ── Resolver rutas ────────────────────────────────────────────────────
        string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        string exeNombre = Path.GetFileNameWithoutExtension(exePath);

        string rutaConfig = Path.Combine(exeDir, exeNombre + ".json");

        LaunchConfig config;
        try
        {
            if (!File.Exists(rutaConfig))
                config = new LaunchConfig(); // sin config → bandejas vacía igualmente
            else
            {
                using var stream = File.OpenRead(rutaConfig);
                config = JsonSerializer.Deserialize<LaunchConfig>(stream, OpcionesJson)
                         ?? new LaunchConfig();
            }
        }
        catch
        {
            config = new LaunchConfig();
        }

        // ── Resolver directorio de logs ───────────────────────────────────────
        string dirLogs = config.LogDirectory is { Length: > 0 }
            ? Environment.ExpandEnvironmentVariables(config.LogDirectory)
            : exeDir;

        Directory.CreateDirectory(dirLogs);

        // ── Limpiar logs al inicio si está configurado ────────────────────────
        if (config.ClearLogsOnStart)
        {
            foreach (var logFile in Directory.EnumerateFiles(dirLogs, "*.log"))
            {
                try { File.Delete(logFile); } catch { }
            }
        }

        long maxBytes = config.MaxLogSizeBytes > 0
            ? config.MaxLogSizeBytes
            : 5 * 1024 * 1024;

        string logLauncher = Path.Combine(dirLogs, exeNombre + ".log");

        // ── Crear Job Object y controladores ──────────────────────────────────
        using var job = new ProcessJob();

        var controladores = config.Tasks
            .Select(t => new TaskController(t, exeDir, dirLogs, logLauncher, maxBytes, job))
            .ToList();

        // ── Retraso inicial ───────────────────────────────────────────────────
        if (config.InitialDelayMs > 0)
            Thread.Sleep(config.InitialDelayMs);

        // ── Arrancar todas las tareas ─────────────────────────────────────────
        foreach (var ctrl in controladores)
        {
            ctrl.Start();
            if (ctrl.DelayAfterMs > 0)
                Thread.Sleep(ctrl.DelayAfterMs);
        }

        // ── Bucle de mensajes Windows Forms con icono de bandeja ──────────────
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var trayApp = new TrayApp(controladores, dirLogs, logLauncher);
        Application.Run(trayApp);

        // ── Al salir: detener las tareas que queden activas ───────────────────
        foreach (var ctrl in controladores)
        {
            try { ctrl.Stop(); } catch { }
        }
        // Al terminar el 'using var job', el Job Object cierra su handle y Windows
        // termina automáticamente todos los procesos hijo que aún estén en ejecución.
    }
}