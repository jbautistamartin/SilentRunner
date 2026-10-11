// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Drawing.Drawing2D;

namespace SilentRunner;

/// <summary>
/// Iconos del menú de bandeja dibujados con GDI+.
/// Los menús de Windows Forms pintan el texto con GDI, que muestra los emoji en
/// monocromo — por eso los iconos van como imagen y no como carácter en la etiqueta.
/// Se generan una sola vez y se reutilizan en cada reconstrucción del menú, así que
/// nunca deben liberarse mientras el icono de bandeja siga vivo.
/// </summary>
internal static class MenuIconos
{
    /// <summary>Lado del lienzo. El menú lo reescala al tamaño que toque según el DPI.</summary>
    private const int Lado = 32;

    // ── Paleta ────────────────────────────────────────────────────────────────

    private static readonly Color Verde = Color.FromArgb(46, 158, 75);
    private static readonly Color Rojo = Color.FromArgb(200, 80, 63);
    private static readonly Color Gris = Color.FromArgb(138, 143, 152);
    private static readonly Color Pizarra = Color.FromArgb(90, 100, 112);
    private static readonly Color Ambar = Color.FromArgb(227, 165, 60);
    private static readonly Color Madera = Color.FromArgb(166, 124, 82);
    private static readonly Color Dorado = Color.FromArgb(255, 203, 76);
    private static readonly Color DoradoBorde = Color.FromArgb(214, 158, 46);
    private static readonly Color Papel = Color.FromArgb(238, 243, 248);

    // ── Iconos (generación diferida y cacheada) ───────────────────────────────

    private static Image? _iniciar, _detener, _activa, _inactiva;
    private static Image? _documento, _papelera, _carpeta, _escoba, _corazon, _salir;

    internal static Image Iniciar => _iniciar ??= Crear(DibujarIniciar);
    internal static Image Detener => _detener ??= Crear(DibujarDetener);
    internal static Image TareaActiva => _activa ??= Crear(DibujarTareaActiva);
    internal static Image TareaInactiva => _inactiva ??= Crear(DibujarTareaInactiva);
    internal static Image Documento => _documento ??= Crear(DibujarDocumento);
    internal static Image Papelera => _papelera ??= Crear(DibujarPapelera);
    internal static Image Carpeta => _carpeta ??= Crear(DibujarCarpeta);
    internal static Image Escoba => _escoba ??= Crear(DibujarEscoba);
    internal static Image Corazon => _corazon ??= Crear(DibujarCorazon);
    internal static Image Salir => _salir ??= Crear(DibujarSalir);

    // ── Infraestructura de dibujo ─────────────────────────────────────────────

    private static Image Crear(Action<Graphics, float> dibujar)
    {
        var bmp = new Bitmap(Lado, Lado);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        dibujar(g, Lado);
        return bmp;
    }

    /// <summary>Convierte coordenadas normalizadas (0..1) a píxeles del lienzo.</summary>
    private static PointF P(float s, float x, float y) => new(x * s, y * s);

    private static RectangleF R(float s, float x, float y, float ancho, float alto) =>
        new(x * s, y * s, ancho * s, alto * s);

    // ── Un método por icono ───────────────────────────────────────────────────

    private static void DibujarIniciar(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Verde);
        g.FillPolygon(brocha, [P(s, 0.28f, 0.16f), P(s, 0.82f, 0.50f), P(s, 0.28f, 0.84f)]);
    }

    private static void DibujarDetener(Graphics g, float s)
    {
        using var trazado = RectanguloRedondeado(R(s, 0.24f, 0.24f, 0.52f, 0.52f), s * 0.08f);
        using var brocha = new SolidBrush(Rojo);
        g.FillPath(brocha, trazado);
    }

    private static void DibujarTareaActiva(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Verde);
        g.FillEllipse(brocha, R(s, 0.26f, 0.26f, 0.48f, 0.48f));
    }

    private static void DibujarTareaInactiva(Graphics g, float s)
    {
        using var lapiz = new Pen(Gris, s * 0.09f);
        g.DrawEllipse(lapiz, R(s, 0.28f, 0.28f, 0.44f, 0.44f));
    }

    private static void DibujarDocumento(Graphics g, float s)
    {
        // Hoja con la esquina superior derecha doblada.
        PointF[] hoja =
        [
            P(s, 0.24f, 0.12f), P(s, 0.60f, 0.12f), P(s, 0.78f, 0.30f),
            P(s, 0.78f, 0.88f), P(s, 0.24f, 0.88f),
        ];

        using var papel = new SolidBrush(Papel);
        using var borde = new Pen(Pizarra, s * 0.09f) { LineJoin = LineJoin.Round };
        g.FillPolygon(papel, hoja);
        g.DrawPolygon(borde, hoja);

        // Pliegue de la esquina.
        using var pliegue = new Pen(Pizarra, s * 0.06f);
        g.DrawLines(pliegue, [P(s, 0.60f, 0.12f), P(s, 0.60f, 0.30f), P(s, 0.78f, 0.30f)]);

        // Renglones de texto.
        using var renglon = new Pen(Pizarra, s * 0.07f);
        g.DrawLine(renglon, P(s, 0.35f, 0.52f), P(s, 0.67f, 0.52f));
        g.DrawLine(renglon, P(s, 0.35f, 0.68f), P(s, 0.67f, 0.68f));
    }

    private static void DibujarPapelera(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Rojo);
        using var lapiz = new Pen(Rojo, s * 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // Asa y tapa.
        g.DrawLine(lapiz, P(s, 0.40f, 0.18f), P(s, 0.60f, 0.18f));
        g.DrawLine(lapiz, P(s, 0.18f, 0.28f), P(s, 0.82f, 0.28f));

        // Cubo ligeramente troncocónico.
        g.FillPolygon(brocha,
        [
            P(s, 0.25f, 0.36f), P(s, 0.75f, 0.36f),
            P(s, 0.68f, 0.86f), P(s, 0.32f, 0.86f),
        ]);

        // Estrías del cubo, caladas sobre el relleno.
        using var estria = new Pen(Papel, s * 0.055f);
        g.DrawLine(estria, P(s, 0.42f, 0.46f), P(s, 0.41f, 0.76f));
        g.DrawLine(estria, P(s, 0.58f, 0.46f), P(s, 0.59f, 0.76f));
    }

    private static void DibujarCarpeta(Graphics g, float s)
    {
        using var brocha = new SolidBrush(Ambar);

        // Pestaña.
        using var pestana = RectanguloRedondeado(R(s, 0.12f, 0.20f, 0.34f, 0.18f), s * 0.05f);
        g.FillPath(brocha, pestana);

        // Cuerpo.
        using var cuerpo = RectanguloRedondeado(R(s, 0.12f, 0.28f, 0.76f, 0.52f), s * 0.07f);
        g.FillPath(brocha, cuerpo);

        // Bisel claro que separa tapa y cuerpo.
        using var bisel = new Pen(Color.FromArgb(120, 255, 255, 255), s * 0.06f);
        g.DrawLine(bisel, P(s, 0.16f, 0.44f), P(s, 0.84f, 0.44f));
    }

    private static void DibujarEscoba(Graphics g, float s)
    {
        // Mango.
        using var mango = new Pen(Madera, s * 0.10f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(mango, P(s, 0.76f, 0.16f), P(s, 0.46f, 0.52f));

        // Cepillo.
        using var cerdas = new SolidBrush(Ambar);
        g.FillPolygon(cerdas,
        [
            P(s, 0.34f, 0.46f), P(s, 0.58f, 0.62f),
            P(s, 0.42f, 0.88f), P(s, 0.14f, 0.70f),
        ]);

        // Separación de las cerdas.
        using var hilo = new Pen(Color.FromArgb(150, 255, 255, 255), s * 0.05f);
        g.DrawLine(hilo, P(s, 0.26f, 0.56f), P(s, 0.34f, 0.80f));
        g.DrawLine(hilo, P(s, 0.38f, 0.56f), P(s, 0.44f, 0.76f));
    }

    private static void DibujarCorazon(Graphics g, float s)
    {
        // Contorno: punta inferior, lóbulo izquierdo, escote central y lóbulo derecho.
        using var trazado = new GraphicsPath();
        trazado.AddBezier(P(s, 0.50f, 0.94f), P(s, 0.10f, 0.62f), P(s, 0.02f, 0.38f), P(s, 0.17f, 0.22f));
        trazado.AddBezier(P(s, 0.17f, 0.22f), P(s, 0.30f, 0.09f), P(s, 0.46f, 0.13f), P(s, 0.50f, 0.26f));
        trazado.AddBezier(P(s, 0.50f, 0.26f), P(s, 0.54f, 0.13f), P(s, 0.70f, 0.09f), P(s, 0.83f, 0.22f));
        trazado.AddBezier(P(s, 0.83f, 0.22f), P(s, 0.98f, 0.38f), P(s, 0.90f, 0.62f), P(s, 0.50f, 0.94f));
        trazado.CloseFigure();

        using var relleno = new SolidBrush(Dorado);
        using var borde = new Pen(DoradoBorde, s / 28f);
        g.FillPath(relleno, trazado);
        g.DrawPath(borde, trazado);
    }

    private static void DibujarSalir(Graphics g, float s)
    {
        // Símbolo de encendido: arco abierto por arriba más el trazo vertical.
        using var lapiz = new Pen(Pizarra, s * 0.11f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(lapiz, R(s, 0.20f, 0.20f, 0.60f, 0.60f), -60f, 300f);
        g.DrawLine(lapiz, P(s, 0.50f, 0.12f), P(s, 0.50f, 0.46f));
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    private static GraphicsPath RectanguloRedondeado(RectangleF r, float radio)
    {
        float d = radio * 2;
        var trazado = new GraphicsPath();
        trazado.AddArc(r.Left, r.Top, d, d, 180, 90);
        trazado.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        trazado.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        trazado.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        trazado.CloseFigure();
        return trazado;
    }
}
