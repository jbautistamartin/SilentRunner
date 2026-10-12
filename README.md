# SilentRunner

[![Última versión](https://img.shields.io/github/v/release/jbautistamartin/SilentRunner?label=descargar&color=2E7D32)](https://github.com/jbautistamartin/SilentRunner/releases/latest)
[![Licencia](https://img.shields.io/badge/licencia-MIT-blue)](LICENSE)
[![Donar con PayPal](https://img.shields.io/badge/Donar-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/jbautistamartin)

Lanzador de procesos y comandos de consola completamente oculto — sin ventanas, sin consola visible, con captura de salida en log rotativo.

## Descarga

**Versión actual: [v1.1.0](https://github.com/jbautistamartin/SilentRunner/releases/tag/v1.1.0)** —
descárgala desde la
**[página de releases](https://github.com/jbautistamartin/SilentRunner/releases/latest)**. Lo que
cambia en cada versión está en [`CHANGELOG.md`](CHANGELOG.md).

El paquete `SilentRunner-<version>-win-x64.zip` contiene:

| Archivo | Para qué sirve |
|---|---|
| `SilentRunner.exe` | Ejecutable único (~1,8 MB). Requiere .NET 10 instalado |
| `SilentRunner.example.json` | Plantilla de configuración anotada |
| `README.md` | Esta documentación |
| `LICENSE` | Licencia MIT |

Puesta en marcha:

1. Extrae el ZIP en la carpeta que quieras — por ejemplo `C:\tools\SilentRunner`.
2. Copia `SilentRunner.example.json` como `SilentRunner.json` y define tus tareas.
3. Ejecuta `SilentRunner.exe`. No abre ninguna ventana: aparece como icono en la bandeja del sistema, y desde su menú contextual controlas cada tarea.

En ese mismo menú, **Acerca de SilentRunner** abre la única ventana del programa: la versión instalada, la página del proyecto, el aviso de responsabilidad y la licencia.

Sin archivo de configuración el programa arranca igualmente, con la bandeja vacía.

### Verificar la descarga

El ejecutable **no está firmado con un certificado de firma de código**, así que la primera vez Windows SmartScreen mostrará un aviso de «Editor desconocido» — *Más información* → *Ejecutar de todas formas*. Es el comportamiento normal de cualquier binario sin certificado comercial.

Para confirmar que el archivo descargado es exactamente el publicado, compara su hash con el contenido de `SHA256SUMS.txt`, incluido en el release:

```powershell
Get-FileHash .\SilentRunner-1.1.0-win-x64.zip -Algorithm SHA256
```

El ensamblado interno lleva además **nombre seguro** (*strong name*), con token público `a313609336f4f9ff`.

## Funcionamiento

`SilentRunner.exe` lee un archivo JSON con el **mismo nombre base** que el ejecutable, ubicado en la misma carpeta, y lanza cada tarea definida en él de forma invisible.

```
carpeta/
├── SilentRunner.exe
└── SilentRunner.json   ← configuración leída automáticamente
```

Si renombras el ejecutable, crea un JSON con el nuevo nombre:

```
carpeta/
├── MiTarea.exe
└── MiTarea.json
```

## Logs de salida

Cada proceso lanzado genera su propio archivo de log, nombrado según el ejecutable hijo:

| Proceso lanzado | Archivo de log generado |
|---|---|
| `powershell.exe` | `powershell.log` |
| `cmd.exe` | `cmd.log` |
| `MiHerramienta.exe` | `MiHerramienta.log` |

El propio launcher escribe eventos operacionales (inicio, errores, códigos de salida) en un log con **su propio nombre**: `SilentRunner.log`.

Todos los logs son **rotativos**: cuando un archivo supera el tamaño máximo configurado, se eliminan las entradas más antiguas del principio hasta volver a estar dentro del límite. No se generan archivos de backup.

## Configuración

Copia `SilentRunner.example.json`, renómbralo igual que tu `.exe` y edítalo.

### Propiedades raíz

| Campo | Tipo | Por defecto | Descripción |
|---|---|---|---|
| `initialDelayMs` | entero | `0` | Espera en milisegundos antes de lanzar la primera tarea |
| `logDirectory` | cadena | `""` | Carpeta donde se escriben los logs. Vacío = misma carpeta del .exe. Admite variables de entorno (`%TEMP%`, `%APPDATA%`, etc.) |
| `maxLogSizeBytes` | entero | `5242880` | Tamaño máximo de **cada** archivo de log en bytes (por defecto 5 MB). Configurable por instalación |
| `checkForUpdates` | booleano | `true` | Preguntar una vez al día si hay una versión más nueva publicada. Solo avisa; no descarga ni instala nada por su cuenta |
| `tasks` | lista | `[]` | Lista de tareas a ejecutar, en orden |

### Propiedades de cada tarea

| Campo | Tipo | Por defecto | Descripción |
|---|---|---|---|
| `name` | cadena | `""` | Etiqueta descriptiva que aparece en el log del launcher |
| `executable` | cadena | — | Ruta del ejecutable. Admite variables de entorno |
| `arguments` | cadena | `""` | Argumentos de línea de comandos |
| `workingDirectory` | cadena | `""` | Directorio de trabajo. Vacío = carpeta del launcher |
| `captureOutput` | booleano | `true` | Captura stdout y stderr y los escribe en el log del proceso hijo |
| `waitForExit` | booleano | `false` | Esperar a que el proceso termine antes de continuar con la siguiente tarea |
| `timeoutMs` | entero | `0` | Tiempo máximo de espera en ms cuando `waitForExit=true`. `0` = sin límite |
| `delayAfterMs` | entero | `0` | Pausa en ms después de lanzar esta tarea antes de la siguiente |

### Ejemplo de configuración

```json
{
  "initialDelayMs": 0,
  "logDirectory": "%TEMP%\\MisLogs",
  "maxLogSizeBytes": 5242880,
  "tasks": [
    {
      "name": "Script de mantenimiento",
      "executable": "powershell.exe",
      "arguments": "-NonInteractive -File \"C:\\scripts\\mantenimiento.ps1\"",
      "workingDirectory": "",
      "captureOutput": true,
      "waitForExit": true,
      "timeoutMs": 60000,
      "delayAfterMs": 1000
    },
    {
      "name": "Herramienta en segundo plano",
      "executable": "%PROGRAMFILES%\\MiApp\\app.exe",
      "arguments": "--modo silencioso",
      "workingDirectory": "",
      "captureOutput": false,
      "waitForExit": false,
      "timeoutMs": 0,
      "delayAfterMs": 0
    }
  ]
}
```

## Garantías de invisibilidad

| Mecanismo | Efecto |
|---|---|
| `OutputType=WinExe` | El propio launcher no abre ventana de consola |
| `CreateNoWindow=true` | Los procesos hijos no abren ventana de consola |
| `WindowStyle=Hidden` | Las ventanas gráficas de los hijos se ocultan |
| `UseShellExecute=false` | El shell de Windows no abre ventanas intermedias |

## Rotación de logs

Cuando un archivo de log supera `maxLogSizeBytes`:

1. Se leen todos los bytes del archivo.
2. Se calcula cuántos bytes sobran desde el principio.
3. Se avanza hasta el siguiente salto de línea para no cortar a mitad de una entrada.
4. Se reescribe el archivo solo con el contenido restante.

No se crean archivos `.bak` ni copias de seguridad.

## Requisitos

- Windows 10 / 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado en la máquina destino

## Publicar como ejecutable único (sin instalar .NET)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El `.exe` resultante en `bin/Release/net10.0-windows/win-x64/publish/` funciona sin tener .NET instalado.

## Apoyar el proyecto

SilentRunner es gratuito y de código abierto. Si te resulta útil, puedes invitarme a un café:

[![Donar con PayPal](https://img.shields.io/badge/Donar-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/jbautistamartin)

También encontrarás el enlace en el menú del icono de la bandeja del sistema (**Invitar a un café**) y en la ventana **Acerca de SilentRunner**. Es completamente opcional: la aplicación no muestra avisos ni limita ninguna función.

## Avisos de versión nueva

Con `checkForUpdates` activado —lo está por defecto—, SilentRunner pregunta **una vez al día** a
GitHub si hay publicada una versión más nueva que la que está corriendo. Si la hay:

- sale un globo en la bandeja del sistema, una sola vez;
- el menú del icono gana una entrada **«Hay una versión nueva: vX.Y.Z»**;
- en la ventana **Acerca de** se puede ver el número, abrir el release y pulsar
  **Descargar el ZIP**, que lo baja a la carpeta temporal y **comprueba su hash** contra el
  `SHA256SUMS.txt` del release antes de darlo por bueno. Si no cuadra, el fichero se borra.

**SilentRunner no se actualiza solo, a propósito.** Un ejecutable en uso no se puede sobrescribir,
y el apaño habitual —esperar a que el proceso muera, intercambiar ficheros y relanzar— choca aquí
con tres cosas: el Job Object se lleva por delante los procesos hijo al salir, SmartScreen vuelve
a avisar porque el binario no está firmado con Authenticode, y un binario sin firmar que reemplaza
ejecutables es justo lo que marca cualquier antivirus por heurística. Una carpeta a medio
actualizar es peor que una versión vieja: el último paso —cerrar SilentRunner y reemplazar el
`.exe`— lo das tú.

La comprobación no manda nada: es una petición GET a la API pública de GitHub, sin credenciales, y
lo único que se guarda en el equipo es la fecha del último intento, en
`%LOCALAPPDATA%\SilentRunner\actualizacion.json`. Si no hay red, no pasa nada y no se avisa de
nada.

## Licencia

MIT — consulta el archivo [LICENSE](LICENSE) para más detalles.
