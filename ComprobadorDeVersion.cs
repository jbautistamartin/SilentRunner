// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace SilentRunner;

/// <summary>
/// Mira si en GitHub hay publicada una versión más nueva que la que se está ejecutando, y
/// descarga su ZIP cuando se le pide.
///
/// **No actualiza nada.** Un ejecutable en uso no se puede sobrescribir, y el apaño habitual
/// —un actualizador que espera a que el proceso muera, intercambia ficheros y relanza— choca con
/// tres cosas de este programa: el Job Object se lleva por delante los procesos hijo al salir,
/// SmartScreen vuelve a avisar porque el binario no está firmado con Authenticode, y un binario
/// sin firmar que reemplaza ejecutables es justo lo que marca cualquier antivirus por heurística.
/// Una carpeta a medio actualizar es peor que una versión vieja, así que esto llega hasta dejar
/// el ZIP comprobado en <c>%TEMP%</c> y abrir la carpeta: el último paso lo da una persona.
/// </summary>
internal static class ComprobadorDeVersion
{
    /// <summary>La versión publicada que supera a la instalada, con lo que hace falta para bajarla.</summary>
    internal sealed record Novedad(
        Version Version,
        string Etiqueta,
        string Notas,
        string PaginaUrl,
        string? ZipUrl,
        string? ZipNombre,
        long ZipBytes,
        string? SumasUrl);

    /// <summary>Resultado de bajar un ZIP: dónde quedó y si se pudo comprobar su hash.</summary>
    internal sealed record Descarga(string Ruta, bool Verificado);

    private const string UrlApi = "https://api.github.com/repos/jbautistamartin/SilentRunner/releases/latest";

    /// <summary>
    /// La novedad encontrada en esta ejecución, si hay alguna. La escriben tanto la comprobación
    /// de fondo de la bandeja como el botón «Buscar ahora» de la ventana, y la lee el menú para
    /// enseñar su entrada: así los dos caminos cuentan lo mismo.
    /// </summary>
    internal static Novedad? Conocida { get; set; }

    /// <summary>
    /// Por qué no se pudo comprobar, si es que no se pudo. Lo pone a <c>null</c> cada intento.
    ///
    /// Existe porque <see cref="Buscar"/> devuelve <c>null</c> tanto cuando no hay nada nuevo
    /// como cuando no hubo forma de preguntar, y eso da igual para la comprobación de fondo
    /// —las dos cosas son callarse— pero no para quien acaba de pulsar «Buscar ahora»: a ese hay
    /// que decirle si está al día o si se quedó sin red.
    /// </summary>
    internal static string? UltimoProblema { get; private set; }

    /// <summary>Cada cuánto se pregunta, como mucho. Una vez al día sobra para esto.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    /// <summary>
    /// Memoria entre arranques: cuándo se preguntó por última vez y con qué <c>ETag</c>.
    ///
    /// Va en <c>%LOCALAPPDATA%</c> y no junto al ejecutable a propósito: lo normal es dejar
    /// SilentRunner en una carpeta de herramientas donde el usuario no tiene por qué poder
    /// escribir, y un fallo al guardar esto no puede impedir que el programa arranque.
    /// </summary>
    private static string RutaEstado => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SilentRunner", "actualizacion.json");

    private sealed class Estado
    {
        public DateTimeOffset UltimaComprobacion { get; set; }
        public string? Etag { get; set; }

        /// <summary>
        /// El release encontrado, si había uno más nuevo.
        ///
        /// Se guarda entero —y no solo la etiqueta— porque el aviso tiene que seguir en pie
        /// después de cerrar y volver a abrir el programa: dentro del mismo día no se vuelve a
        /// preguntar, y sin esto la entrada del menú desaparecía en el siguiente arranque.
        /// </summary>
        public string? Etiqueta { get; set; }
        public string? Pagina { get; set; }
        public string? ZipUrl { get; set; }
        public string? ZipNombre { get; set; }
        public long ZipBytes { get; set; }
        public string? SumasUrl { get; set; }

        /// <summary>Si ya se enseñó el globo de esta versión: se avisa una vez, no en cada arranque.</summary>
        public bool Avisado { get; set; }
    }

    /// <summary>
    /// Pregunta a GitHub por el último release. Devuelve <c>null</c> cuando no hay nada más nuevo,
    /// cuando todavía no toca preguntar o cuando algo falla: quedarse callado es la respuesta
    /// correcta a no haber podido comprobar.
    /// </summary>
    /// <param name="forzar">
    /// Salta el intervalo y el <c>ETag</c>. Es lo que usa el botón «Buscar ahora»: quien lo pulsa
    /// quiere una respuesta, no la de ayer.
    /// </param>
    internal static async Task<Novedad?> Buscar(bool forzar, Action<string>? anotar, CancellationToken ct = default)
    {
        UltimoProblema = null;

        try
        {
            var estado = LeerEstado();
            if (!forzar && DateTimeOffset.UtcNow - estado.UltimaComprobacion < Intervalo)
            {
                // No toca preguntar, pero lo que se encontró ayer sigue valiendo.
                return Recordada(estado);
            }

            using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // api.github.com responde 403 sin User-Agent. No es opcional.
            cliente.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SilentRunner", VersionInstalada()?.ToString() ?? "1.0"));
            cliente.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var peticion = new HttpRequestMessage(HttpMethod.Get, UrlApi);
            // Una respuesta 304 no gasta cuota del límite de 60 peticiones por hora e IP.
            if (!forzar && estado.Etag is { Length: > 0 })
                peticion.Headers.TryAddWithoutValidation("If-None-Match", estado.Etag);

            using var respuesta = await cliente.SendAsync(peticion, ct).ConfigureAwait(false);

            estado.UltimaComprobacion = DateTimeOffset.UtcNow;

            if (respuesta.StatusCode == HttpStatusCode.NotModified)
            {
                GuardarEstado(estado);
                // Un 304 dice que el último release no ha cambiado, no que sea el instalado.
                return Recordada(estado);
            }

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                // Repositorio sin releases publicados. No es un error, y no hay de qué avisar.
                Olvidar(estado);
                GuardarEstado(estado);
                return null;
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                UltimoProblema = $"GitHub respondió HTTP {(int)respuesta.StatusCode}.";
                anotar?.Invoke($"No se pudo comprobar si hay versión nueva: HTTP {(int)respuesta.StatusCode}");
                GuardarEstado(estado);
                return null;
            }

            estado.Etag = respuesta.Headers.ETag?.ToString();

            using var flujo = await respuesta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(flujo, cancellationToken: ct).ConfigureAwait(false);
            var raiz = json.RootElement;

            string etiqueta = raiz.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var publicada = Interpretar(etiqueta);
            var instalada = VersionInstalada();
            if (publicada is null || instalada is null || publicada <= instalada)
            {
                // En cuanto se instala la versión de la que se avisaba, el aviso se va con ella.
                Olvidar(estado);
                GuardarEstado(estado);
                return null;
            }

            string? zipUrl = null, zipNombre = null, sumasUrl = null;
            long zipBytes = 0;
            if (raiz.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string nombre = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && zipUrl is null)
                    {
                        zipUrl = url;
                        zipNombre = nombre;
                        zipBytes = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    }
                    else if (nombre.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        sumasUrl = url;
                    }
                }
            }

            string pagina = raiz.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

            // Si es otra versión distinta de la que ya se anunció, el globo vuelve a salir.
            if (!string.Equals(estado.Etiqueta, etiqueta, StringComparison.Ordinal))
                estado.Avisado = false;

            estado.Etiqueta = etiqueta;
            estado.Pagina = pagina;
            estado.ZipUrl = zipUrl;
            estado.ZipNombre = zipNombre;
            estado.ZipBytes = zipBytes;
            estado.SumasUrl = sumasUrl;
            GuardarEstado(estado);

            anotar?.Invoke($"Hay una versión nueva publicada: {etiqueta} (instalada: {instalada})");

            return new Novedad(
                publicada,
                etiqueta,
                raiz.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                pagina,
                zipUrl, zipNombre, zipBytes, sumasUrl);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            // Sin red, con el DNS caído o con GitHub de mal humor: no es noticia.
            UltimoProblema = ex.Message;
            anotar?.Invoke($"No se pudo comprobar si hay versión nueva: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Baja el ZIP de <paramref name="novedad"/> a <c>%TEMP%</c> y lo comprueba contra el
    /// <c>SHA256SUMS.txt</c> del release.
    ///
    /// Si el hash no cuadra, el fichero descargado **se borra** y se lanza: un ZIP que no es el
    /// publicado no se deja en el disco para que alguien lo abra por error. Si el release no
    /// publica sumas, la descarga vale igual pero se devuelve como *no verificada*, que es
    /// distinto de verificada y así se le dice a quien mira.
    /// </summary>
    internal static async Task<Descarga> Descargar(Novedad novedad, CancellationToken ct = default)
    {
        if (novedad.ZipUrl is null or { Length: 0 } || novedad.ZipNombre is null)
            throw new InvalidOperationException("Este release no publica ningún ZIP.");

        string carpeta = Path.Combine(Path.GetTempPath(), "SilentRunner-descargas");
        Directory.CreateDirectory(carpeta);
        string destino = Path.Combine(carpeta, novedad.ZipNombre);

        using var cliente = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        cliente.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SilentRunner", VersionInstalada()?.ToString() ?? "1.0"));

        using (var origen = await cliente.GetStreamAsync(novedad.ZipUrl, ct).ConfigureAwait(false))
        using (var fichero = File.Create(destino))
        {
            await origen.CopyToAsync(fichero, ct).ConfigureAwait(false);
        }

        if (novedad.SumasUrl is null)
            return new Descarga(destino, Verificado: false);

        string sumas = await cliente.GetStringAsync(novedad.SumasUrl, ct).ConfigureAwait(false);
        string? esperado = BuscarHash(sumas, novedad.ZipNombre);
        if (esperado is null)
            return new Descarga(destino, Verificado: false);

        string calculado = await Sha256De(destino, ct).ConfigureAwait(false);
        if (!calculado.Equals(esperado, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(destino); } catch { }
            throw new InvalidOperationException(
                "El ZIP descargado no coincide con el hash publicado en SHA256SUMS.txt. Se ha borrado.");
        }

        return new Descarga(destino, Verificado: true);
    }

    /// <summary>
    /// Lo último que se encontró, mientras siga siendo más nuevo que lo instalado.
    ///
    /// Es público para que la bandeja lo cargue al arrancar: así la entrada del menú está desde
    /// el primer clic derecho, sin esperar a que toque preguntar.
    /// </summary>
    internal static Novedad? LeerRecordada() => Recordada(LeerEstado());

    /// <summary>Si ya se enseñó el globo de la versión que se recuerda.</summary>
    internal static bool YaSeAviso() => LeerEstado().Avisado;

    /// <summary>Apunta que el globo de esta versión ya salió, para no repetirlo en cada arranque.</summary>
    internal static void MarcarAvisado()
    {
        var estado = LeerEstado();
        estado.Avisado = true;
        GuardarEstado(estado);
    }

    private static Novedad? Recordada(Estado estado)
    {
        var guardada = Interpretar(estado.Etiqueta);
        var instalada = VersionInstalada();
        if (guardada is null || instalada is null || guardada <= instalada)
            return null;

        return new Novedad(
            guardada,
            estado.Etiqueta ?? "",
            "", // las notas no se guardan: para leerlas se abre el release
            estado.Pagina ?? "",
            estado.ZipUrl,
            estado.ZipNombre,
            estado.ZipBytes,
            estado.SumasUrl);
    }

    private static void Olvidar(Estado estado)
    {
        estado.Etiqueta = null;
        estado.Pagina = null;
        estado.ZipUrl = null;
        estado.ZipNombre = null;
        estado.ZipBytes = 0;
        estado.SumasUrl = null;
        estado.Avisado = false;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    /// <summary>
    /// La versión que se está ejecutando, la misma que enseña «Acerca de»: sale del ensamblado,
    /// que es donde la fija el proyecto.
    /// </summary>
    internal static Version? VersionInstalada()
    {
        var ensamblado = typeof(ComprobadorDeVersion).Assembly;
        var informativa = ensamblado
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        return Interpretar(informativa) ?? ensamblado.GetName().Version;
    }

    /// <summary>
    /// Convierte «v1.2.3», «1.2.3» o «1.2.3+abc» en un <see cref="Version"/>.
    ///
    /// Se compara como números y nunca como texto: «1.10.0» es *menor* que «1.9.0» alfabéticamente
    /// y ahí se cae todo el mundo una vez.
    /// </summary>
    private static Version? Interpretar(string? etiqueta)
    {
        if (string.IsNullOrWhiteSpace(etiqueta)) return null;

        string limpia = etiqueta.Trim();
        if (limpia.StartsWith('v') || limpia.StartsWith('V')) limpia = limpia[1..];

        int corte = limpia.IndexOfAny(['+', '-']);
        if (corte > 0) limpia = limpia[..corte];

        return Version.TryParse(limpia, out var version) ? version : null;
    }

    /// <summary>Busca en un SHA256SUMS.txt («hash  fichero» por línea) el hash de un fichero.</summary>
    private static string? BuscarHash(string sumas, string nombre)
    {
        foreach (string linea in sumas.Split('\n'))
        {
            string[] partes = linea.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2) continue;

            // El nombre puede venir con un «*» delante (modo binario de sha256sum).
            string fichero = partes[^1].TrimStart('*');
            if (fichero.Equals(nombre, StringComparison.OrdinalIgnoreCase))
                return partes[0];
        }
        return null;
    }

    private static async Task<string> Sha256De(string ruta, CancellationToken ct)
    {
        using var flujo = File.OpenRead(ruta);
        byte[] hash = await SHA256.HashDataAsync(flujo, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static Estado LeerEstado()
    {
        try
        {
            if (File.Exists(RutaEstado))
                return JsonSerializer.Deserialize<Estado>(File.ReadAllText(RutaEstado)) ?? new Estado();
        }
        catch { }
        return new Estado();
    }

    private static void GuardarEstado(Estado estado)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaEstado)!);
            File.WriteAllText(RutaEstado, JsonSerializer.Serialize(estado));
        }
        catch { /* si no se puede guardar, se preguntará otra vez: no es grave */ }
    }
}
