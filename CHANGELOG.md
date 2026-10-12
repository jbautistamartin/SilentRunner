# Registro de cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/);
versionado según [SemVer](https://semver.org/lang/es/).

## [1.1.0] - 2026-10-11

### Añadido

- **Aviso de versión nueva.** Una vez al día pregunta a GitHub si hay publicada una versión más
  nueva; si la hay, lo dice con un globo en la bandeja, con una entrada en su menú y en la ventana
  «Acerca de», desde donde se puede abrir el release o **descargar el ZIP comprobando su hash**
  contra el `SHA256SUMS.txt` publicado. No se actualiza solo: reemplazar el ejecutable que está
  corriendo lo hace una persona, con el programa cerrado. Se apaga con `checkForUpdates: false`.

- **Ventana «Acerca de»**, abierta desde el menú de la bandeja: versión instalada (leída del
  ensamblado, no escrita a mano), autoría, enlace a la página del proyecto en GitHub, aviso de
  responsabilidad, donativo y licencia. Es la única ventana del programa, no es modal —con ella
  delante se sigue pudiendo parar una tarea o abrir un log— y abrirla dos veces trae al frente la
  que ya estaba.

## [1.0.0] - 2026-10-10

Primera versión publicada.

### Añadido

- **Lanzamiento invisible de procesos.** `OutputType=WinExe`, `CreateNoWindow`,
  `WindowStyle=Hidden` y `UseShellExecute=false` garantizan que ni el launcher ni
  sus hijos abran ventana o consola.
- **Configuración por convención.** El ejecutable lee el JSON con su mismo nombre
  base en su misma carpeta (`SilentRunner.exe` → `SilentRunner.json`). Sin
  configuración arranca igualmente, con la bandeja vacía.
- **Tres modos de ejecución por tarea**: reinicio automático (`autoRestart`),
  espera a la salida con tiempo límite opcional (`waitForExit` + `timeoutMs`) y
  disparo sin seguimiento.
- **Icono en la bandeja del sistema** con menú contextual: iniciar y detener todas
  las tareas o cada una por separado, abrir y borrar su log, abrir el log del
  launcher, abrir la carpeta de logs y limpiarlos. El estado de cada tarea se ve
  con un punto verde (en ejecución) o un anillo gris (detenida).
- **Iconos de menú dibujados con GDI+** (`MenuIconos`), porque los menús de
  Windows Forms pintan el texto con GDI y los emoji saldrían en monocromo.
- **Logs rotativos** por proceso hijo, con recorte por el principio hasta el
  siguiente salto de línea para no cortar entradas a medias. No se crean copias
  de seguridad.
- **Limpieza de procesos huérfanos.** Un Job Object de Windows con
  `KILL_ON_JOB_CLOSE` mata a todos los hijos cuando el launcher termina.
- **Instancia única** mediante mutex global, para evitar lanzar las tareas por
  duplicado.
- **Versionado y nombre seguro.** El ensamblado se publica como `1.0.0` y va
  firmado con *strong name* (token público `a313609336f4f9ff`).
- Enlace de donación opcional en el menú de la bandeja y botón *Sponsor* del
  repositorio.

### Notas

- El ejecutable **no** lleva certificado de firma de código, así que SmartScreen
  avisará de «Editor desconocido» la primera vez. El release incluye
  `SHA256SUMS.txt` para verificar la descarga.
- El paquete publicado requiere el **runtime de .NET 10** en la máquina destino.

[1.0.0]: https://github.com/jbautistamartin/SilentRunner/releases/tag/v1.0.0