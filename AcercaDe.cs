// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;

namespace SilentRunner;

/// <summary>
/// Ventana «Acerca de»: qué es esto, de qué versión, quién responde por ello y dónde
/// vive el proyecto.
///
/// Es la única ventana que abre SilentRunner —todo lo demás es el menú de la bandeja—,
/// así que es también donde viven las dos direcciones que se abren fuera
/// (<see cref="UrlProyecto"/> y <see cref="UrlDonacion"/>): el menú las abre desde aquí
/// en vez de llevar su propia copia.
///
/// Se construye a mano y sin diseñador, como el resto del programa, y lo que enseña no
/// se teclea dos veces: la versión sale del ensamblado y el icono es el mismo que carga
/// la bandeja.
/// </summary>
internal sealed class AcercaDe : Form
{
    /// <summary>Página del proyecto: el código, los cambios y los ejecutables publicados.</summary>
    internal const string UrlProyecto = "https://github.com/jbautistamartin/SilentRunner";

    /// <summary>Enlace del donativo, el que ofrecen esta ventana y el menú de la bandeja.</summary>
    internal const string UrlDonacion = "https://paypal.me/jbautistamartin";

    /// <summary>Ancho del contenido en píxeles de diseño. El alto lo pone el texto.</summary>
    private const int Ancho = 420;

    /// <summary>La ventana abierta, si hay una: pedirla dos veces la trae al frente.</summary>
    private static AcercaDe? _abierta;

    private readonly Icon _icono = TrayApp.CargarIcono();

    /// <summary>Lo que esta ventana ha creado y tiene que liberar al cerrarse.</summary>
    private readonly List<IDisposable> _propios = [];

    // Las piezas de la ficha de actualizaciones, que cambian con lo que se vaya sabiendo.
    private Label? _estadoVersion;
    private Button? _botonBuscar;
    private Button? _botonDescargar;
    private LinkLabel? _enlaceRelease;

    // ── Apertura ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Abre la ventana, o la trae al frente si ya estaba abierta. No es modal a
    /// propósito: con esto delante se sigue pudiendo parar una tarea o abrir un log
    /// desde la bandeja.
    /// </summary>
    internal static void Mostrar()
    {
        if (_abierta is { IsDisposed: false })
        {
            if (_abierta.WindowState == FormWindowState.Minimized)
                _abierta.WindowState = FormWindowState.Normal;
            _abierta.Activate();
            return;
        }

        var ventana = new AcercaDe();
        _abierta = ventana;
        ventana.FormClosed += (_, _) => { if (_abierta == ventana) _abierta = null; };
        ventana.Show();
    }

    /// <summary>
    /// Abre <paramref name="url"/> con lo que el sistema tenga asociado.
    ///
    /// Si falla no se avisa: pasa cuando no hay navegador o la asociación está rota, y
    /// un cuadro de error por no haber podido abrir una página web sería más ruido que
    /// la página misma en un programa cuyo oficio es no enseñar ventanas.
    /// </summary>
    internal static void AbrirEnlace(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    // ── Construcción ──────────────────────────────────────────────────────────

    private AcercaDe()
    {
        Font = SystemFonts.MessageBoxFont ?? Font;
        Text = "Acerca de SilentRunner";
        Icon = _icono;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = SystemColors.Window; // la ficha de un programa, no un diálogo gris
        Padding = new Padding(20);

        var columna = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        columna.Controls.Add(Cabecera());
        columna.Controls.Add(Raya());
        columna.Controls.Add(Parrafo(
            "Lanza procesos y comandos de consola sin ventana y sin consola visible, y guarda " +
            "lo que escriben en un log que se rota solo.",
            new Padding(0, 0, 0, 16)));

        columna.Controls.Add(FichaActualizaciones());

        var enlace = new LinkLabel
        {
            Text = "github.com/jbautistamartin/SilentRunner",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        enlace.LinkClicked += (_, _) => AbrirEnlace(UrlProyecto);

        columna.Controls.Add(Ficha(
            "El proyecto",
            "El código, el registro de cambios y los ejecutables publicados están en GitHub, " +
            "junto al sitio donde dar parte de un fallo.",
            enlace));

        columna.Controls.Add(Ficha(
            "Uso bajo tu responsabilidad",
            "SilentRunner se entrega «tal cual», sin garantía de ningún tipo. Quien lo instala " +
            "y lo configura es el único responsable de los procesos que lance con él y de lo " +
            "que esos procesos hagan en el equipo."));

        var corazon = new Bitmap(MenuIconos.Corazon, 20, 20);
        _propios.Add(corazon);

        var donar = new Button
        {
            Text = "Invitar a un café",
            Image = corazon,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 6, 12, 6),
            Margin = new Padding(0, 10, 0, 0),
        };
        donar.Click += (_, _) => AbrirEnlace(UrlDonacion);

        // Donativo: opcional de principio a fin. El programa no avisa, no insiste y no
        // deja de hacer nada si nadie dona.
        columna.Controls.Add(Ficha(
            "Apoyar el proyecto",
            "SilentRunner es gratuito, sin anuncios y de código abierto. Si te resulta útil, " +
            "puedes invitarme a un café. Es completamente opcional.",
            donar));

        columna.Controls.Add(Tenue(
            "Distribuido bajo licencia MIT" + Environment.NewLine +
            "Copyright © 2026 José Luis Bautista Martín",
            new Padding(0, 4, 0, 0)));

        columna.Controls.Add(Pie());

        Controls.Add(columna);
    }

    /// <summary>
    /// La ficha de actualizaciones: dice si hay una versión más nueva publicada y deja bajarla.
    ///
    /// El botón solo descarga y comprueba el ZIP — reemplazar el ejecutable que está corriendo no
    /// lo hace el programa, y el porqué está en <see cref="ComprobadorDeVersion"/>.
    /// </summary>
    private Control FichaActualizaciones()
    {
        var rotulo = new Font(Font, FontStyle.Bold);
        _propios.Add(rotulo);

        _estadoVersion = Parrafo(TextoDeEstado(), Padding.Empty, Ancho - 26);

        _botonBuscar = new Button
        {
            Text = "Buscar ahora",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 8, 0),
        };
        _botonBuscar.Click += async (_, _) => await BuscarAhora();

        _botonDescargar = new Button
        {
            Text = "Descargar el ZIP",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 8, 0),
            Visible = ComprobadorDeVersion.Conocida is { ZipUrl: not null },
        };
        _botonDescargar.Click += async (_, _) => await DescargarAhora();

        _enlaceRelease = new LinkLabel
        {
            Text = "Ver el release",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            Visible = ComprobadorDeVersion.Conocida is not null,
        };
        _enlaceRelease.LinkClicked += (_, _) =>
        {
            if (ComprobadorDeVersion.Conocida is { } novedad) AbrirEnlace(novedad.PaginaUrl);
        };

        var fila = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 10, 0, 0),
        };
        fila.Controls.Add(_botonBuscar);
        fila.Controls.Add(_botonDescargar);
        fila.Controls.Add(_enlaceRelease);

        var caja = Caja();
        caja.Controls.Add(new Label
        {
            Text = "Actualizaciones",
            Font = rotulo,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
        });
        caja.Controls.Add(_estadoVersion);
        caja.Controls.Add(fila);
        return caja;
    }

    private string TextoDeEstado() => ComprobadorDeVersion.Conocida is { } novedad
        ? TextoDeNovedad(novedad)
        : "Se comprueba una vez al día si hay una versión más nueva publicada en GitHub. "
          + $"La instalada es la {VersionDelEnsamblado()}.";

    private static string TextoDeNovedad(ComprobadorDeVersion.Novedad novedad) =>
        $"Hay una versión nueva publicada: {novedad.Etiqueta}."
        + (novedad.ZipNombre is null
            ? " Ese release no publica ningún ZIP, así que hay que bajarlo de su página."
            : " Al descargarla se comprueba su hash contra el SHA256SUMS.txt del release y el ZIP"
              + " se queda en la carpeta temporal: cambiar el ejecutable lo haces tú, con"
              + " SilentRunner cerrado.");

    private async Task BuscarAhora()
    {
        if (_botonBuscar is null || _estadoVersion is null) return;

        _botonBuscar.Enabled = false;
        _estadoVersion.Text = "Preguntando a GitHub...";

        var novedad = await ComprobadorDeVersion.Buscar(forzar: true, anotar: null);
        if (novedad is not null) ComprobadorDeVersion.Conocida = novedad;

        _estadoVersion.Text = novedad is not null
            ? TextoDeNovedad(novedad)
            : ComprobadorDeVersion.UltimoProblema is { } problema
                ? $"No se pudo comprobar: {problema}"
                : $"Estás en la última versión publicada ({VersionDelEnsamblado()}).";

        if (_botonDescargar is not null)
            _botonDescargar.Visible = ComprobadorDeVersion.Conocida is { ZipUrl: not null };
        if (_enlaceRelease is not null)
            _enlaceRelease.Visible = ComprobadorDeVersion.Conocida is not null;

        _botonBuscar.Enabled = true;
    }

    private async Task DescargarAhora()
    {
        if (_botonDescargar is null || _estadoVersion is null) return;
        if (ComprobadorDeVersion.Conocida is not { } novedad) return;

        if (novedad.ZipUrl is null)
        {
            AbrirEnlace(novedad.PaginaUrl);
            return;
        }

        _botonDescargar.Enabled = false;
        _estadoVersion.Text = $"Descargando {novedad.ZipNombre}...";

        try
        {
            var descarga = await ComprobadorDeVersion.Descargar(novedad);
            _estadoVersion.Text = descarga.Verificado
                ? $"Descargado y comprobado contra SHA256SUMS.txt:{Environment.NewLine}"
                  + $"{descarga.Ruta}{Environment.NewLine}{Environment.NewLine}"
                  + "Cierra SilentRunner antes de reemplazar el ejecutable."
                : "Descargado, pero el release no publica sumas y no se ha podido comprobar:"
                  + $"{Environment.NewLine}{descarga.Ruta}";

            AbrirCarpetaDe(descarga.Ruta);
        }
        catch (Exception ex)
        {
            _estadoVersion.Text = $"No se pudo descargar: {ex.Message}";
            _botonDescargar.Enabled = true;
        }
    }

    /// <summary>Abre el Explorador con el fichero ya señalado, que es el paso siguiente.</summary>
    private static void AbrirCarpetaDe(string ruta)
    {
        try { Process.Start("explorer.exe", "/select,\"" + ruta + "\""); }
        catch { }
    }

    /// <summary>Icono, nombre, versión y autor, en una fila como cualquier ficha de Windows.</summary>
    private Control Cabecera()
    {
        using var original = _icono.ToBitmap();
        var retrato = new Bitmap(original, 56, 56);
        _propios.Add(retrato);

        var nombre = new Font(Font.FontFamily, Font.SizeInPoints + 4.5f, FontStyle.Bold);
        _propios.Add(nombre);

        var firma = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
        };
        firma.Controls.Add(new Label
        {
            Text = "SilentRunner",
            Font = nombre,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        });
        firma.Controls.Add(Tenue($"Versión {VersionDelEnsamblado()}", new Padding(0, 0, 0, 2)));
        firma.Controls.Add(Tenue("José Luis Bautista Martín", Padding.Empty));

        var cabecera = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 14),
        };
        cabecera.Controls.Add(new PictureBox
        {
            Image = retrato,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(56, 56),
            Margin = new Padding(0, 0, 14, 0),
        });
        cabecera.Controls.Add(firma);
        return cabecera;
    }

    /// <summary>
    /// Una ficha con borde, con la idea de las tarjetas del «Acerca de» de las
    /// aplicaciones de Android de la casa: un rótulo, su texto y, si hace falta, lo que
    /// se pulsa.
    /// </summary>
    private Control Ficha(string titulo, string texto, params Control[] extras)
    {
        var rotulo = new Font(Font, FontStyle.Bold);
        _propios.Add(rotulo);

        var caja = Caja();
        caja.Controls.Add(new Label
        {
            Text = titulo,
            Font = rotulo,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
        });
        caja.Controls.Add(Parrafo(texto, Padding.Empty, Ancho - 26));

        foreach (var extra in extras)
            caja.Controls.Add(extra);

        return caja;
    }

    /// <summary>El recuadro con borde que comparten todas las fichas.</summary>
    private static FlowLayoutPanel Caja() => new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(12),
        Margin = new Padding(0, 0, 0, 14),
    };

    /// <summary>El botón de cerrar, a la derecha, que es donde Windows lo pone.</summary>
    private Control Pie()
    {
        var cerrar = new Button
        {
            Text = "Cerrar",
            Size = new Size(96, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        cerrar.Click += (_, _) => Close();
        cerrar.Location = new Point(Ancho - cerrar.Width, 4);

        var pie = new Panel
        {
            Width = Ancho,
            Height = 34,
            Margin = new Padding(0, 10, 0, 0),
        };
        pie.Controls.Add(cerrar);

        AcceptButton = cerrar;
        CancelButton = cerrar; // y con ello la tecla Escape
        return pie;
    }

    // ── Piezas de texto ───────────────────────────────────────────────────────

    /// <summary>
    /// Un párrafo que se parte solo. El ancho se fija por arriba y por abajo a la vez:
    /// con solo el máximo, una ficha de texto corto saldría más estrecha que las demás.
    /// </summary>
    private static Label Parrafo(string texto, Padding margen, int ancho = Ancho)
    {
        var caja = new Size(ancho, 0);
        return new Label
        {
            Text = texto,
            AutoSize = true,
            MinimumSize = caja,
            MaximumSize = caja,
            Margin = margen,
        };
    }

    private static Label Tenue(string texto, Padding margen) => new()
    {
        Text = texto,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = margen,
    };

    private static Control Raya() => new Panel
    {
        Width = Ancho,
        Height = 1,
        BackColor = SystemColors.ControlLight,
        Margin = new Padding(0, 0, 0, 14),
    };

    /// <summary>
    /// La versión que se enseña sale del ensamblado, que es donde la fija el proyecto:
    /// escribirla aquí a mano sería un segundo número que se queda atrás en el primer
    /// release.
    /// </summary>
    private static string VersionDelEnsamblado()
    {
        var ensamblado = typeof(AcercaDe).Assembly;
        string? informativa = ensamblado
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return informativa is { Length: > 0 }
            ? informativa
            : ensamblado.GetName().Version?.ToString(3) ?? "—";
    }

    // ── Limpieza ──────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var pieza in _propios)
            {
                try { pieza.Dispose(); } catch { }
            }
            _propios.Clear();
            _icono.Dispose(); // el de la bandeja es otra instancia: este es nuestro
        }
        base.Dispose(disposing);
    }
}
