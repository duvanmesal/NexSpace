# NexSpace Tauri · Compilar, abrir y verificar un ensayo

El punto de entrada para pruebas nuevas es [NexSpace Test Center](../../tools/test-center/README.md). El centro reutiliza estos scripts, archiva resultados por sesión y permite revalidar un arrastre con `-Verify -Session ID` sin abrir otro piloto.

Desde la raíz de NexSpace, en Windows PowerShell 5.1 o PowerShell 7:

```powershell
.\prototypes\web-shell\Build-Pilot.ps1 -CheckOnly
.\prototypes\web-shell\Build-Pilot.ps1
$ensayo = .\prototypes\web-shell\Run-Pilot.ps1 -DropTrial | ConvertFrom-Json
$ensayo
```

`-CheckOnly` valida requisitos y prueba el enlazador con un PE x64 antes de instalar dependencias o compilar React. El script busca primero en el PATH y luego en ubicaciones de instalación habituales. También consulta `artifacts/toolchain`, `artifacts/web-lab` y la instalación del laboratorio A/B identificada por su rama en `git worktree list`. La última ruta es una alternativa de descubrimiento y puede sustituirse con `-ToolchainRoot` o `NEXSPACE_TOOLCHAIN_ROOT`. Para instalaciones independientes se pueden indicar `-Cargo`, `-Rustc`, `-GnuBin`, `-DotNet`, `-Node`, `-Npm` y `-Python`. El entorno del subproceso se restaura al salir.

El fallo original de Cargo se reprodujo porque el binario existía en la instalación privada del laboratorio y no estaba en el PATH de una terminal nueva. En el equipo de prueba se detectaron Node x64 24.22.0, npm 11.17.0, Python x64 3.14.0, Windows PowerShell 5.1, PowerShell 7.6.9, Rust/Cargo 1.100.0 para `x86_64-pc-windows-gnu`, MinGW GCC 16.2.0, SDK .NET 10.0.401 y WebView2 Evergreen 157/158. Windows App SDK 2.5.1 está en la caché NuGet y pertenece a la variante WinUI. La compilación Tauri usa WebView2 y NexCore autocontenido. La ruta GNU sigue activa. Una instalación Rust MSVC x64 debe tener su enlazador operativo en la sesión y superar la misma prevalidación.

## Compilación y procedencia

- Cada compilación tiene una carpeta bajo `artifacts/tauri-nexcore/builds`. `build.json` incluye SHA Git, indicador de cambios locales, huella de los archivos de entrada, hora UTC, arquitectura, versiones, validaciones y hashes SHA-256 del EXE, el cargador WebView2 y los archivos de NexCore. El host incorpora el identificador en el EXE.
- El orden es preflight, `npm ci`, TypeScript/Vite, Cargo con `--locked`, publicación .NET x64 autocontenida, comprobación PE, preparación SQLite y consulta IPC al sidecar publicado. Un fallo marca `build-status.json` como inválido. Los artefactos previos se conservan. El lanzador comprueba la revisión, la huella de entrada y los hashes de los archivos antes de abrir la última compilación verificada.
- El resultado es reproducible desde el código y los lockfiles con las herramientas registradas. Cada compilación incorpora un ID nuevo. Eso cambia el hash del EXE aunque las entradas sean idénticas.

### Icono de Windows

El [símbolo de jade transparente](../../assets/icons/nexspace-jade-symbol.png) se conserva tal como llegó. En una copia de trabajo, el generador descarta el alfa residual de 1 a 3 sobre 255, mide el contorno restante y lo centra para ocupar el 90 % del lienzo por su lado mayor. La interpolación bicúbica con alfa premultiplicado aplica la misma escala a ambas dimensiones. Conserva el fondo transparente, las proporciones y los reflejos visibles. El ICO y los PNG RGBA de `src-tauri/icons` incluyen 16, 24, 32, 48, 64, 128 y 256 px. Windows 11 normalmente muestra 24 px en la barra de tareas y 16 px en la bandeja al 100 % de escala. [Tamaños de iconos de Windows](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction)

Tauri toma el ICO para el ejecutable y el icono predeterminado de las ventanas. La bandeja reutiliza ese icono. Windows lo usa para las superficies que muestran el icono de aplicación en la barra de tareas. La configuración de bundle apunta a los nuevos recursos. El piloto genera un EXE portátil y la creación de instaladores sigue desactivada. [Recursos de icono en Tauri](https://v2.tauri.app/develop/icons/)

Cargo vuelve a incrustar el icono cuando cambia el ICO. Antes de verificar el Release, el lanzador compara todos los fotogramas del ejecutable con los PNG. Para comprobarlo en Windows, abre el build vigente por `Run-Pilot.ps1` y revisa el botón del proceso en la barra de tareas y el icono de NexSpace experimental en la bandeja. Si solo un anclaje anterior muestra el símbolo viejo, comprueba primero qué ejecutable abre ese acceso.

Desde la raíz del repositorio, regenera, compila y comprueba el recurso del EXE:

```powershell
powershell.exe -NoProfile -File .\prototypes\web-shell\Generate-AppIcon.ps1
.\tools\NexSpace-Test.ps1 -Test build-release
$build = python -X utf8 .\prototypes\web-shell\pilot_support.py latest
python -X utf8 .\prototypes\web-shell\bench\Validate-AppIcon.py --exe (Join-Path $build 'nexspace-tauri-lab.exe')
```

## Sesiones y cierre

`Run-Pilot.ps1` crea una base aislada por arranque. `-DemoDatabase` copia una biblioteca de demostración desde una conexión SQLite de solo lectura. Por defecto el lanzador rechaza un segundo piloto si encuentra un proceso activo. `-AllowParallel` permite una decisión explícita y cada sesión recibe su propia base y su propio directorio WebView2. Para instancias antiguas que arrancaron fuera del lanzador, la procedencia de la biblioteca se informa como desconocida.

La salida muestra el ID de sesión, el build y su hora UTC, la ruta del EXE, los PID de host y NexCore, el perfil, el contenedor de destino, la base real y las rutas de evidencias. El host escribe `ready.json` después de recibir el manifiesto de NexCore y el lanzador verifica la relación entre ambos procesos. Las evidencias se guardan en la carpeta temporal de la sesión, bajo `evidence`. El recibo local queda en `artifacts/tauri-nexcore/sessions`. Las bases, trazas, rutas locales y capturas quedan fuera de Git.

```powershell
.\prototypes\web-shell\Run-Pilot.ps1 -Status
.\prototypes\web-shell\Run-Pilot.ps1 -Status -SessionId $ensayo.session
.\prototypes\web-shell\Run-Pilot.ps1 -StopSession $ensayo.session
```

La salida desde la bandeja sigue disponible. `-StopSession` valida la identidad del proceso, solicita su salida ordinaria y comprueba que el árbol observado terminó. Si el cierre se atasca, informa del fallo y conserva el proceso para diagnóstico.

## Traza de recuperación desde la bandeja

`Run-Pilot.ps1 -TrayTrial` activa `NEXSPACE_FLOATING_TRAY_TRACE` en el host aislado. El arranque registra `trayTracing: true` en `ready.json` y abre `evidence/tray.jsonl` con el estado inicial de NexDock. El cierre nativo del dock registra el estado antes y después de ocultarlo. El menú nativo de bandeja registra la solicitud y el resultado de recuperación con un mismo identificador de intento. La traza contiene el PID, build y sesión. El modo habitual de arranque no activa esa instrumentación.

Para que la verificación y la evidencia queden en el historial, inicia el [ensayo de bandeja desde Test Center](../../tools/test-center/README.md#recuperación-desde-la-bandeja).

El [ensayo guiado de convivencia con Explorer](../../tools/test-center/README.md#convivencia-con-explorer) reutiliza la misma traza nativa de bandeja para identificar el cierre. Test Center conserva por separado los archivos aislados, las identidades observadas de las ventanas y los puntos de control aportados por el operador.

## Cierre de los menús de las islas

Para reproducir el recorrido físico, abre un piloto aislado con `Run-Pilot.ps1 -DropTrial`. En una isla con referencias, como **Juegos**, comprueba por separado **···** y el clic derecho sobre **Controles.txt**.

1. Abre cada menú, pulsa fuera de su superficie dentro de la isla y vuelve a abrirlo. Repite con **Escape** y comprueba que el foco vuelve al botón o al archivo invocador.
2. Cambia de un menú a otro con **···**, clic derecho sobre otro recurso o **Shift+F10**. Elige **Mostrar lista** en Opciones y **Renombrar** en el menú de un archivo. Comprueba que el menú se cierra y que la acción o el formulario aparece. Puedes cancelar el formulario.
3. Con un menú abierto, haz clic en otra isla y luego repite desde el primer panel con Explorer. Comprueba que el menú se descarta y que el destino conserva su interacción. En el popover de una categoría, pulsa **Escape** una vez para cerrar el menú y otra vez para cerrar el popover.
4. Comprueba que puedes volver a interactuar con una referencia, contraer y expandir la isla y recuperar NexDock desde la bandeja. Si repites el arrastre físico, usa los archivos aislados de la carpeta abierta por el lanzador y verifica esa sesión mediante el [recorrido de arrastre](#arrastre-físico-reproducible).

`NexSpace-Test.ps1 -Test panel-menus` ejecuta los componentes React en Edge headless con un puente IPC de prueba. Las selecciones se envían mediante el protocolo de DevTools para reproducir la captura y el burbujeo de un clic de navegador. Las pruebas comprueban el editor de referencias, la presentación de lista, los comandos del dock y el descarte de menús. El cambio físico entre ventanas Tauri y Explorer se comprueba en el paso 3. [Auditoría de las acciones](ACCIONES_PILOTO.md)

## Arrastre físico reproducible

1. Ejecutar `Run-Pilot.ps1 -DropTrial`. Abrir la ruta exacta que indica `dropSource`. Su nombre **Arrastrar a Archivos [ID corto]** distingue esta sesión de las carpetas abiertas en ensayos anteriores. Contiene el TXT, el PDF, el acceso directo y la carpeta Proyecto. El mecanismo de accesos directos busca primero `pwsh.exe` y recurre a `powershell.exe`. Se puede fijar `-PowerShell powershell.exe`.
2. Colocar Explorer al lado de **Archivos** en la ventana del piloto. Seleccionar los cuatro elementos, arrastrarlos al interior de la tarjeta y soltar una vez.
3. Ejecutar `python prototypes/web-shell/bench/Explorer-Drop.py --verify ID_DE_SESION`. El script comprueba sesión, build, perfil, contenedor, rutas, tipos y hashes. Correlaciona la hora de alta de cada destino en SQLite con un lote OLE completo del host. Guarda `drop-verdict.json` en las evidencias. Usa 0 para PASS, 1 para FAIL y 2 para BLOCKED. Una referencia añadida por el selector o por el banco de IPC no acredita un gesto. Si se arrastraron rutas de otra carpeta, el resultado identifica esa carpeta y la esperada. Un arrastre correcto posterior puede acreditar las cuatro rutas de la sesión.

Test Center llama al verificador con `--read-only`. Conserva el veredicto original y registra su nueva lectura por separado. Las consultas a SQLite usan modo de solo lectura.

La traza vincula el destino y sus coordenadas físicas y lógicas, el DPI, el recuento, la validación de cada ruta, el resultado de NexCore y el cierre del lote. La auditoría del sidecar distingue `ole-drop`, `picker` y `test-ipc`. En el grupo de prueba todas las altas exitosas deben proceder de OLE, ya que la auditoría registra el grupo y el origen de la operación sin guardar la ruta del elemento. El bloqueo por sesión impide abrir dos hosts con la misma base gestionada.

## Diagnóstico de los arrastres anteriores

La trazabilidad histórica confirmó un lote OLE real de cuatro elementos en **Archivos** el 24 de noviembre de 2026 a las 16:30:39 UTC. Los cuatro tiempos de alta en SQLite caen entre la recepción y el cierre del lote con cuatro respuestas satisfactorias de NexCore. Los cuatro destinos correctos procedían de la carpeta de otro ensayo. El verificador anterior comparaba únicamente las rutas de la carpeta del ensayo destino. Al usar la carpeta que realmente originó el gesto, los cuatro tipos y los hashes coinciden. Para comparar el árbol histórico se normalizaron los separadores de las rutas relativas. Esto explica la discrepancia sin cambiar el criterio del ensayo nuevo, que exige una sola identidad.

Un ensayo gestionado posterior recibió otro lote OLE de cuatro elementos el 24 de noviembre a las 17:04:50 UTC. El recibo y el diagnóstico de NexCore identificaron la misma base `floating.db`. Una consulta en solo lectura confirmó las cuatro altas en Archivos del perfil Principal, con los tipos esperados y dentro del intervalo del lote. La auditoría de NexCore registró cuatro altas `ole-drop` para ese perfil y grupo. Las rutas pertenecían a la carpeta de origen de un ensayo anterior. La verificación devolvió FAIL con cuatro referencias OLE ajenas, cero de las cuatro exigidas y los originales del ensayo intactos. Por cada referencia, NexCore devuelve el éxito después del commit de SQLite. El cierre del lote resume las respuestas de las cuatro operaciones.

## Validación del entorno

| Caso | Estado | Evidencia |
|---|---|---|
| PowerShell 5.1 con PATH sin toolchain local | PASS | Prevalidación, compilación Release y lanzamiento. Creación del .lnk con Windows PowerShell. |
| Cargo explícito inexistente | PASS | Terminó con error, dejó el marcador `failed` y el lanzador rechazó iniciar. |
| Identidad y artefacto obsoleto | PASS | El lanzador rechazó una huella de entrada diferente. El arranque ordinario comprobó build, base y parentesco del sidecar. |
| Sesiones duplicadas y cierre | PASS | Se rechazó un segundo piloto por omisión. El cierre ordinario comprobó los 12 procesos observados y devolvió cero residuales. |
| TypeScript, Rust, .NET y verificador | PASS | Pasaron TypeScript/Vite, dos pruebas Rust, 127 .NET por configuración y cinco pruebas Python de detección y correlación. Se conservó el aviso GNU de manifiestos múltiples. |
| Persistencia e iconos | PASS | `Test-Floating.py --shell-icons` comprobó referencias, PNG, perfiles, geometría y reinicio del sidecar por EOF. |
| Arrastre histórico reconstruido | PASS | Cuatro rutas y tipos correlacionados con una traza OLE histórica. Los originales coinciden con los hashes previos. |
| Nuevo ensayo trazado de extremo a extremo | BLOCKED | La comprobación previa al gesto devolvió 0 de 4 referencias, hashes intactos y ausencia de drop. La herramienta de escritorio admite arrastre dentro de una ventana. |
| Toolchain MSVC | NOT_TESTED | El equipo ensayado utilizó GNU x64. |
