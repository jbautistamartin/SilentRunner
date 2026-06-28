// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

namespace SilentRunner;

/// <summary>
/// Configuración raíz cargada desde &lt;NombreExe&gt;.json junto al ejecutable.
/// Renombra el .exe libremente — siempre busca un .json con el mismo nombre base.
/// </summary>
public sealed class LaunchConfig
{
    /// <summary>Espera en milisegundos antes de lanzar la primera tarea.</summary>
    public int InitialDelayMs { get; set; } = 0;

    /// <summary>
    /// Carpeta donde se escriben los archivos de log.
    /// Cadena vacía (valor por defecto) = misma carpeta del ejecutable.
    /// Admite variables de entorno: %TEMP%, %APPDATA%, etc.
    /// </summary>
    public string LogDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño máximo en bytes de cada archivo de log antes de eliminar las entradas más antiguas.
    /// Valor por defecto: 5 242 880 bytes (5 MB).
    /// </summary>
    public long MaxLogSizeBytes { get; set; } = 5 * 1024 * 1024;

    public List<TaskEntry> Tasks { get; set; } = [];
}

public sealed class TaskEntry
{
    /// <summary>Etiqueta descriptiva que aparece en el log del launcher.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ruta del ejecutable. Admite variables de entorno: %WINDIR%, %TEMP%, etc.</summary>
    public string Executable { get; set; } = string.Empty;

    /// <summary>Argumentos de línea de comandos.</summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>Directorio de trabajo. Cadena vacía = carpeta del launcher.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Captura stdout y stderr del proceso hijo y los escribe en un log rotativo
    /// nombrado según el ejecutable (p. ej. powershell.exe → powershell.log).
    /// </summary>
    public bool CaptureOutput { get; set; } = true;

    /// <summary>Esperar a que el proceso termine antes de continuar con la siguiente tarea.</summary>
    public bool WaitForExit { get; set; } = false;

    /// <summary>
    /// Tiempo máximo de espera en ms cuando WaitForExit=true. 0 = sin límite.
    /// </summary>
    public int TimeoutMs { get; set; } = 0;

    /// <summary>Pausa en ms después de lanzar esta tarea antes de la siguiente.</summary>
    public int DelayAfterMs { get; set; } = 0;

    /// <summary>
    /// Si es <c>true</c>, el launcher monitoriza el proceso y lo reinicia automáticamente
    /// cada vez que termina (por cualquier motivo). Incompatible con <c>WaitForExit=true</c>.
    /// </summary>
    public bool AutoRestart { get; set; } = false;

    /// <summary>
    /// Tiempo de espera en ms antes de reiniciar el proceso tras una caída.
    /// Solo se usa cuando <c>AutoRestart=true</c>. Valor por defecto: 1000 ms.
    /// </summary>
    public int RestartDelayMs { get; set; } = 1000;
}