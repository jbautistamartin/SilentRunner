// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.ComponentModel;

namespace SilentRunner;

/// <summary>
/// Contexto de aplicación sin ventana principal que muestra un icono en el área de notificación.
/// Mantiene vivo el bucle de mensajes de Windows Forms.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly IReadOnlyList<TaskController> _controllers;
    private readonly string _dirLogs;
    private readonly string _logLauncher;
    private readonly long _maxLogBytes;
    private readonly NotifyIcon _trayIcon;

    /// <summary>
    /// El que pregunta de vez en cuando si hay versión nueva. Es un <c>Timer</c> de Windows
    /// Forms y no uno de hilos a propósito: su <c>Tick</c> llega en el hilo de la interfaz, que
    /// es el único desde el que se puede tocar el icono de la bandeja.
    /// </summary>
    private readonly System.Windows.Forms.Timer? _vigilante;

    internal TrayApp(
        IReadOnlyList<TaskController> controllers,
        string dirLogs,
        string logLauncher,
        long maxLogBytes,
        bool comprobarVersion)
    {
        _controllers = controllers;
        _dirLogs = dirLogs;
        _logLauncher = logLauncher;
        _maxLogBytes = maxLogBytes;

        var menu = new ContextMenuStrip();
        menu.Opening += OnMenuOpening;

        _trayIcon = new NotifyIcon
        {
            Text = "SilentRunner",
            Icon = CargarIcono(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.BalloonTipClicked += (_, _) => AcercaDe.Mostrar();

        if (comprobarVersion)
        {
            // Lo que se encontró en una ejecución anterior sigue valiendo: así la entrada del
            // menú está desde el primer clic derecho y no solo cuando toque volver a preguntar.
            ComprobadorDeVersion.Conocida = ComprobadorDeVersion.LeerRecordada();

            // El primer tic a los 30 segundos: lo urgente es arrancar las tareas, y preguntarle
            // a GitHub no puede retrasarlo. Luego cada seis horas, aunque el comprobador solo
            // pregunte de verdad una vez al día — así un equipo que se apaga cada noche no se
            // queda sin preguntar nunca.
            _vigilante = new System.Windows.Forms.Timer { Interval = 30_000 };
            _vigilante.Tick += AlTocarElVigilante;
            _vigilante.Start();
        }
    }

    // ── Versión nueva ─────────────────────────────────────────────────────────

    private async void AlTocarElVigilante(object? sender, EventArgs e)
    {
        _vigilante!.Interval = (int)TimeSpan.FromHours(6).TotalMilliseconds;

        var novedad = await ComprobadorDeVersion.Buscar(forzar: false, anotar: Anotar);
        if (novedad is null) return;

        ComprobadorDeVersion.Conocida = novedad;

        // El globo, una vez por versión y no en cada arranque: la entrada del menú se queda
        // puesta, que es lo que hace falta para volver a ella cuando se pueda.
        if (ComprobadorDeVersion.YaSeAviso()) return;

        // Un globo de la bandeja y nada más: este programa no tiene ventana que sacar delante,
        // y sacarla sin que nadie la pida sería justo lo contrario de lo que hace.
        _trayIcon.BalloonTipTitle = "SilentRunner";
        _trayIcon.BalloonTipText =
            $"Hay una versión nueva publicada: {novedad.Etiqueta}. Mírala en «Acerca de».";
        _trayIcon.ShowBalloonTip(10_000);
        ComprobadorDeVersion.MarcarAvisado();
    }

    private void Anotar(string mensaje) => RollingLog.Write(_logLauncher, mensaje, _maxLogBytes);

    // ── Construcción del menú contextual ─────────────────────────────────────

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        var menu = (ContextMenuStrip)sender!;

        // Eliminar entradas dinámicas anteriores y reconstruirlas con el estado actual.
        var itemsAnteriores = menu.Items.Cast<ToolStripItem>().ToList();
        menu.Items.Clear();
        foreach (var item in itemsAnteriores)
            item.Dispose(); // los iconos de MenuIconos son compartidos: no se liberan aquí

        if (_controllers.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("Sin tareas configuradas") { Enabled = false });
        }
        else
        {
            bool hayEnEjecucion = _controllers.Any(c => c.IsRunning);
            bool hayDetenidas = _controllers.Any(c => !c.IsRunning);

            // ── Acciones globales ────────────────────────────────────────────
            var iniciarTodos = new ToolStripMenuItem("Iniciar todos")
            {
                Image = MenuIconos.Iniciar,
                Enabled = hayDetenidas,
            };
            iniciarTodos.Click += (_, _) => { foreach (var c in _controllers) c.Start(); };

            var detenerTodos = new ToolStripMenuItem("Detener todos")
            {
                Image = MenuIconos.Detener,
                Enabled = hayEnEjecucion,
            };
            detenerTodos.Click += (_, _) => { foreach (var c in _controllers) c.Stop(); };

            menu.Items.Add(iniciarTodos);
            menu.Items.Add(detenerTodos);
            menu.Items.Add(new ToolStripSeparator());

            // ── Una entrada por tarea ────────────────────────────────────────
            foreach (var ctrl in _controllers)
            {
                bool activa = ctrl.IsRunning;

                var tareaItem = new ToolStripMenuItem(ctrl.Name)
                {
                    Image = activa ? MenuIconos.TareaActiva : MenuIconos.TareaInactiva,
                };

                if (activa)
                {
                    var detener = new ToolStripMenuItem("Detener") { Image = MenuIconos.Detener };
                    var c = ctrl; // captura local para el closure
                    detener.Click += (_, _) => c.Stop();
                    tareaItem.DropDownItems.Add(detener);
                }
                else
                {
                    var iniciar = new ToolStripMenuItem("Iniciar") { Image = MenuIconos.Iniciar };
                    var c = ctrl;
                    iniciar.Click += (_, _) => c.Start();
                    tareaItem.DropDownItems.Add(iniciar);
                }

                tareaItem.DropDownItems.Add(new ToolStripSeparator());

                var logPath = ctrl.LogPath;
                bool logExiste = File.Exists(logPath);

                var abrirLog = new ToolStripMenuItem("Abrir log")
                {
                    Image = MenuIconos.Documento,
                    Enabled = logExiste,
                };
                abrirLog.Click += (_, _) =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(logPath) { UseShellExecute = true }); }
                    catch { }
                };
                tareaItem.DropDownItems.Add(abrirLog);

                var borrarLog = new ToolStripMenuItem("Borrar log")
                {
                    Image = MenuIconos.Papelera,
                    Enabled = logExiste,
                };
                borrarLog.Click += (_, _) =>
                {
                    try { File.Delete(logPath); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"No se pudo borrar el log:\n{ex.Message}",
                            "SilentRunner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                tareaItem.DropDownItems.Add(borrarLog);

                menu.Items.Add(tareaItem);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        var abrirLogLauncher = new ToolStripMenuItem("Abrir log del launcher") { Image = MenuIconos.Documento };
        abrirLogLauncher.Enabled = File.Exists(_logLauncher);
        abrirLogLauncher.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_logLauncher) { UseShellExecute = true }); }
            catch { }
        };
        menu.Items.Add(abrirLogLauncher);

        var abrirLogs = new ToolStripMenuItem("Abrir carpeta de logs") { Image = MenuIconos.Carpeta };
        abrirLogs.Click += (_, _) => AbrirLogs();
        menu.Items.Add(abrirLogs);

        var limpiarLogs = new ToolStripMenuItem("Limpiar logs") { Image = MenuIconos.Escoba };
        limpiarLogs.Click += (_, _) => LimpiarLogs();
        menu.Items.Add(limpiarLogs);

        menu.Items.Add(new ToolStripSeparator());

        if (ComprobadorDeVersion.Conocida is { } novedad)
        {
            var nueva = new ToolStripMenuItem($"Hay una versión nueva: {novedad.Etiqueta}")
            {
                Image = MenuIconos.Informacion,
            };
            nueva.Click += (_, _) => AcercaDe.Mostrar();
            menu.Items.Add(nueva);
        }

        var acerca = new ToolStripMenuItem("Acerca de SilentRunner") { Image = MenuIconos.Informacion };
        acerca.Click += (_, _) => AcercaDe.Mostrar();
        menu.Items.Add(acerca);

        var donar = new ToolStripMenuItem("Invitar a un café") { Image = MenuIconos.Corazon };
        donar.Click += (_, _) => AcercaDe.AbrirEnlace(AcercaDe.UrlDonacion);
        menu.Items.Add(donar);

        menu.Items.Add(new ToolStripSeparator());

        var salir = new ToolStripMenuItem("Salir") { Image = MenuIconos.Salir };
        salir.Click += (_, _) => Salir();
        menu.Items.Add(salir);
    }

    // ── Salida ────────────────────────────────────────────────────────────────

    private void Salir()
    {
        // Detener todos los procesos antes de salir.
        foreach (var ctrl in _controllers)
        {
            try { ctrl.Stop(); } catch { }
        }

        _trayIcon.Visible = false; // eliminar el icono del área de notificación de inmediato
        ExitThread();              // detiene el bucle de mensajes de Application.Run()
    }

    private void AbrirLogs()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", _dirLogs); }
        catch { }
    }

    private void LimpiarLogs()
    {
        try
        {
            foreach (string archivo in Directory.EnumerateFiles(_dirLogs, "*.log"))
                File.WriteAllText(archivo, string.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudieron limpiar los logs:\n{ex.Message}",
                "SilentRunner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vigilante?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── Carga del icono ───────────────────────────────────────────────────────

    /// <summary>
    /// Busca el icono del programa por los tres sitios donde puede estar. Lo usa
    /// también la ventana «Acerca de», que enseña el mismo: cada una se queda con su
    /// propia instancia, porque la de la bandeja vive mientras viva el proceso.
    /// </summary>
    internal static Icon CargarIcono()
    {
        // 1. Icono Win32 incrustado en el propio ejecutable (vía <ApplicationIcon> en el .csproj).
        try
        {
            string? exePath = Environment.ProcessPath;
            if (exePath is not null)
            {
                var icono = Icon.ExtractAssociatedIcon(exePath);
                if (icono is not null) return icono;
            }
        }
        catch { }

        // 2. Archivo .ico junto al ejecutable.
        try
        {
            string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (exeDir is not null)
            {
                string ruta = Path.Combine(exeDir, "SilentRunner.ico");
                if (File.Exists(ruta))
                    return new Icon(ruta);
            }
        }
        catch { }

        // 3. Icono de aplicación genérico del sistema.
        return SystemIcons.Application;
    }
}
