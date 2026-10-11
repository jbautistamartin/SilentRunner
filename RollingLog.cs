// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

namespace SilentRunner;

/// <summary>
/// Escritor de log rotativo y seguro para hilos concurrentes.
/// Añade líneas con marca de tiempo. Cuando el archivo supera <c>maxBytes</c>,
/// elimina contenido desde el principio (entradas más antiguas) hasta volver
/// al límite. No genera archivos de copia de seguridad.
/// </summary>
internal static class RollingLog
{
    // Un objeto de bloqueo por ruta de archivo para que tareas concurrentes no colisionen.
    private static readonly Dictionary<string, object> _locks = new(StringComparer.OrdinalIgnoreCase);

    private static readonly object _dictLock = new();

    internal static void Write(string logPath, string message, long maxBytes)
    {
        var fileLock = GetLock(logPath);
        lock (fileLock)
        {
            try
            {
                string linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";
                File.AppendAllText(logPath, linea);
                RecortarSiNecesario(logPath, maxBytes);
            }
            catch { /* el log nunca debe interrumpir la ejecución principal */ }
        }
    }

    /// <summary>
    /// Si el archivo supera <paramref name="maxBytes"/>, elimina las entradas más antiguas
    /// del principio hasta que el tamaño vuelva a estar dentro del límite.
    /// </summary>
    private static void RecortarSiNecesario(string logPath, long maxBytes)
    {
        var info = new FileInfo(logPath);
        if (!info.Exists || info.Length <= maxBytes) return;

        byte[] bytes = File.ReadAllBytes(logPath);
        long exceso = bytes.Length - maxBytes;

        // Avanzar más allá de los bytes en exceso hasta el siguiente salto de línea
        // para no cortar una entrada a la mitad.
        int inicio = (int)exceso;
        while (inicio < bytes.Length && bytes[inicio] != (byte)'\n')
            inicio++;

        inicio++; // saltar el propio salto de línea

        if (inicio >= bytes.Length)
        {
            // Todo el archivo es más antiguo que el límite — vaciarlo.
            File.WriteAllBytes(logPath, []);
            return;
        }

        File.WriteAllBytes(logPath, bytes[inicio..]);
    }

    private static object GetLock(string ruta)
    {
        lock (_dictLock)
        {
            if (!_locks.TryGetValue(ruta, out var bloqueo))
                _locks[ruta] = bloqueo = new object();
            return bloqueo;
        }
    }
}