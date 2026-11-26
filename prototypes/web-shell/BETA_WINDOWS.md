# NexSpace · Beta privada para Windows 11 x64

Este procedimiento separa la verificación automatizada del paquete y la aceptación del instalador. La matriz de aceptación histórica del piloto está en [ACEPTACION_BETA.md](ACEPTACION_BETA.md). La decisión de empaquetado está en [ADR-016](../../docs/decisions/ADR-016-beta-windows-tauri.md).

## Construcción y preflight

Desde la raíz del repositorio, con la rama `experiment/tauri-nexcore` activa:

```powershell
.\prototypes\web-shell\Build-Pilot.ps1 -Beta -CheckOnly
.\prototypes\web-shell\Build-Pilot.ps1 -Beta
.\tools\NexSpace-Test.ps1 -Test windows-beta-package
.\tools\NexSpace-Test.ps1 -Test windows-beta-first-run
.\tools\NexSpace-Test.ps1 -Test windows-beta-persistence
```

Herramientas de construcción utilizadas en la candidata 0.2.0-beta.1: Tauri 2.12.7, CLI 2.12.0, NSIS 3.11, Rust 1.100.0 Windows GNU x64, Node 24.22.0, npm 11.17.0 y SDK .NET 10.0.401. La estación de compilación necesita esas herramientas o equivalentes compatibles. El equipo de destino usa NexSpace y NexCore autocontenido con SQLite nativo. WebView2 Evergreen es el único runtime externo previsto. Con Evergreen instalado el instalador lo reutiliza. Si falta, Tauri descarga el bootstrapper de Microsoft, por lo que ese escenario requiere conectividad y puede quedar condicionado por políticas del equipo. [Documentación de instaladores Windows de Tauri](https://v2.tauri.app/distribute/windows-installer/).

```powershell
$root = Resolve-Path '.\artifacts\beta\windows'
$current = Get-Content (Join-Path $root 'build-status.json') -Raw | ConvertFrom-Json
if ($current.status -ne 'ready') { throw 'Empaquetado incompleto' }
$build = Join-Path $root "builds\$($current.id)"
$manifest = Get-Content (Join-Path $build 'package.json') -Raw | ConvertFrom-Json
$installer = Join-Path $build $manifest.installer.file
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $manifest.installer.sha256) { throw 'SHA-256 inesperado' }
Get-AuthenticodeSignature -LiteralPath $installer | Select-Object Status, StatusMessage
$manifest | Select-Object id, commit, version, architecture, identifier, installMode, installer
```

Cada build conserva su EXE instalador, `payload/`, `installer.nsi`, mapa de recursos, inputs de construcción y `package.json` con SHA de Git, fingerprint de entradas, toolchain, firma y hashes. El Test Center conserva `windows-beta.json`, logs y recibos en `artifacts/test-center/sessions/{id}`. Los resultados físicos se registran aparte. El estado `NotSigned` describe esta candidata y puede activar SmartScreen o restricciones de ejecución. Valida el SHA-256 por un canal de confianza y respeta la política de seguridad del equipo.

## Layout y compatibilidad

- Instalación por usuario, sin elevación solicitada por NSIS. Ruta predeterminada `%LOCALAPPDATA%\NexSpace`, con `NexSpace.exe`, el loader de WebView2, `core\NexSpace.CorePilot.exe` y dependencias .NET x64. El instalador admite otra ruta local. El acceso de Inicio apunta al EXE. El acceso de escritorio es opcional.
- Datos en `%LOCALAPPDATA%\com.duvanmesal.nexspace`. `floating.db` contiene perfiles, referencias, estado de las islas y configuración de NexDock. `backups\` conserva respaldos, `logs\` contiene diagnósticos locales, y `webview\` contiene la caché de WebView2. El candado `instance.lock` se libera al salir del proceso.
- La beta inicia en modo flotante con el perfil Principal y tres islas vacías. El laboratorio mantiene su propia biblioteca y build. Cada prueba automatizada usa un directorio temporal nuevo. Los archivos referenciados pertenecen al usuario y la persistencia guarda rutas y metadatos.
- Ante corrupción o esquema futuro, la apertura se interrumpe y se conserva la base. Ante una migración fallida, `logs\core.log` guarda una categoría del error. Con NexSpace cerrado, conserva una copia del directorio original antes de recuperar un respaldo. La sustitución de `floating.db` requiere un respaldo íntegro y compatible.
- NSIS está preparado para Windows 11 x64. ARM64 nativo, Windows 10, MSI/WiX, instalación offline cuando falte WebView2 y ejecución con otras cuentas o políticas empresariales quedan fuera de esta verificación.

## Instalación, actualización y desinstalación

Usar un Windows 11 x64 aislado o contar con autorización expresa del operador. Si SmartScreen o la política de la máquina impiden ejecutar el instalador, registrar BLOCKED y el motivo. Completar los siguientes pasos en una cuenta de prueba con archivos propios del ensayo.

1. Con el piloto y la beta cerrados, ejecutar `Start-Process -FilePath $installer -Wait -PassThru` tras verificar el hash. Comprobar nombre, icono de jade, instalación para el usuario, entrada en Aplicaciones instaladas y acceso en Inicio. Verificar también el icono en NexDock, las islas, la barra de tareas y la bandeja, y en el acceso de escritorio si se eligió.
2. Desde Inicio, abrir NexSpace. Verificar NexDock, Archivos, Juegos y Aplicaciones. Confirmar que el perfil Principal está activo y las islas se crean vacías. Consultar `Get-Item "$env:LOCALAPPDATA\com.duvanmesal.nexspace\floating.db"`. Crear un perfil y añadir una referencia a un TXT propio del ensayo. Salir desde la bandeja con **Cerrar NexSpace**. Verificar que se cierran el host y NexCore y que el árbol observado de WebView2 desaparece. Reabrir desde Inicio y verificar perfil, preferencias y referencia. Este recorrido comprueba la ruta predeterminada que el smoke aislado sustituye por una carpeta temporal.
3. Para validar actualización, preparar un NSIS posterior con SemVer superior e identidad `com.duvanmesal.nexspace`, nombre `NexSpace`, publisher `duvanmesal`, EXE `NexSpace.exe` y modo `currentUser`. Con la beta anterior cerrada, respaldar la biblioteca de prueba y ejecutar el instalador posterior. Verificar una sola entrada de Aplicaciones instaladas, la versión nueva, un solo árbol de procesos y la biblioteca anterior intacta. Comprobar los perfiles y referencias tras otro reinicio. Conservar ambos instaladores y hashes. El actualizador Tauri queda deshabilitado hasta disponer de firma de artefactos, clave pública distribuida y un canal HTTPS de versiones. [Requisitos del actualizador](https://v2.tauri.app/plugin/updater/).
4. Cerrar la beta desde la bandeja y desinstalarla mediante Aplicaciones instaladas. Mantener desmarcada la opción de borrado de datos. Comprobar eliminación de binarios y accesos y conservación de `floating.db`, referencias, respaldos y TXT original. Reinstalar una versión compatible y comprobar que recupera los datos. Registrar esta comprobación por separado de la actualización.

El Test Center mantiene `windows-beta-install`, `windows-beta-upgrade` y `windows-beta-uninstall` como pruebas humanas. Los comandos `-Test` de esos tres escenarios crean un recibo BLOCKED mientras falte la aceptación física y no inician instaladores. Para cada recorrido conserva con su recibo el build, SHA de Git, SHA-256 de los instaladores, versión de Windows, PIDs observados, rutas de prueba, capturas auténticas y observaciones. Usa `-Report -Session <ID>` para localizar el recibo. La evidencia se incorpora conservando los estados históricos originales.

## Alcance de seguridad

- Las páginas se sirven desde el frontend incluido. El host restringe navegación a su origen local. La CSP limita scripts al origen propio, bloquea objetos y marcos, y mantiene estilos inline que utiliza React. La capability local tiene `permissions: []`. Los comandos propios verifican el tipo de ventana, la operación y los identificadores. El selector y OLE entregan rutas a NexCore; la interfaz no dispone de un permiso global de archivos ni de una consola de ejecución.
- El host localiza el sidecar junto a su propio ejecutable y conserva el límite y la correlación del IPC. La beta ignora sesiones de diagnóstico del piloto. Los logs se quedan en el directorio de usuario y no incluyen trazas OLE de rutas de archivos. Los archivos de evidencias de pruebas no se distribuyen.
- En la auditoría del build, npm y NuGet no devolvieron avisos de vulnerabilidad. OSV revisó 457 paquetes del lockfile Rust. El árbol del objetivo Windows usa `cookie 0.18.2`, cuyo aviso afecta procesos Unix que usan `fork`. `chrono`, `glib` y `proc-macro-error` aparecen en el lockfile multisoporte y quedaron fuera del árbol Windows consultado. [Aviso de cookie en RustSec](https://rustsec.org/advisories/RUSTSEC-2026-0594.html) · [API OSV](https://google.github.io/osv.dev/post-v1-querybatch/).
- La firma Authenticode del instalador, la reputación de SmartScreen y la verificación de instalación limpia, actualización y desinstalación en otro Windows siguen pendientes. La evidencia automática del payload cubre ejecución y persistencia con el runtime WebView2 ya presente en la máquina de ensayo.
