// Copyright © 2026 José Luis Bautista Martín. Licensed under the MIT License.

// [STAThread] es obligatorio para Windows Forms (UI en hilo STA).
// Con sentencias de nivel superior el atributo se aplica en la clase parcial generada
// añadiéndolo explícitamente aquí mediante el atributo de módulo —
// pero la forma más sólida es la clase Program explícita a continuación.

namespace SilentRunner;

internal static class Program
{
    [System.STAThread]
    private static void Main()
    {
        using var mutex = new System.Threading.Mutex(initiallyOwned: true, "Global\\SilentRunner", out bool esNueva);
        if (!esNueva) return; // ya hay una instancia en ejecución

        SilentLauncher.Run();
    }
}
