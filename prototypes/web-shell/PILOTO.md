# Piloto Tauri con NexCore

El documento describe la revisión histórica `695f70f`. El punto de entrada actual y la validación de la capa flotante están en [PARIDAD.md](PARIDAD.md). Para reproducir este piloto histórico se requiere `--legacy` y preparar su biblioteca con `NexSpace.CorePilot.exe --prepare`.

La rama local `experiment/tauri-nexcore` parte del laboratorio A/B en `ed6789c`. Archivos y su popover leen una biblioteca SQLite v3. Juegos y Aplicaciones siguen mostrando las fichas del laboratorio. La aplicación WinUI de producción conserva sus servicios, sus ventanas y su recuperación.

## Ejecutar desde la raíz

Se necesitan los mismos Node, Rust, enlazador Windows x64, WebView2 Evergreen y SDK .NET 10 del [laboratorio](README.md). Con las herramientas en PATH, ejecutar en PowerShell:

```powershell
./prototypes/web-shell/Build-Pilot.ps1 -DemoDatabase $rutaBaseDeDemostracion
& ./artifacts/tauri-nexcore/B/nexspace-tauri-lab.exe
```

`-DemoDatabase` acepta una biblioteca **de demostración**. El preparador la abre en modo de solo lectura y usa SQLite Backup para obtener una instantánea consistente. Sin ese argumento, inicializa una biblioteca de fixtures. Añade un contenedor Archivos de prueba con cinco TXT y una referencia ausente. Las fichas, los identificadores y la copia permanecen bajo `artifacts/tauri-nexcore/B/pilot-data/`. Ejecutar de nuevo conserva el estado. Una inicialización interrumpida exige preparar otro directorio de salida o revisar la copia incompleta. El EXE autocontenido del sidecar está en `B/core/`.

En Archivos, un clic abre la referencia según su asociación Windows. El menú del encabezado permite elegir una referencia para editar su etiqueta o cambiar su orden. También se abre el editor con el menú contextual de una ficha. Arrastrar el encabezado mueve la ventana. El tirador ajusta el tamaño y admite flechas de teclado. La opción **Recuperar ventanas del piloto** está en la bandeja, fuera de los WebViews. El botón de apagado del dock y la opción **Cerrar piloto** terminan la sesión.

## IPC y acceso a datos

React pide operaciones tipadas al host Tauri. Rust mantiene un único hijo .NET por instancia y serializa las peticiones. Dos pipes anónimos heredados para stdin/stdout aíslan el canal al árbol de procesos que posee sus handles. Este piloto tiene un único cliente nativo. Un pipe con nombre añadiría un punto de conexión que habría que autenticar y registrar para este mismo uso. El WebView solo carga la página local y conserva la política CSP y la lista vacía de permisos adicionales de Tauri.

Cada línea UTF-8 contiene un sobre JSON con `v: 1`, `id` monotónico y `op`. La respuesta repite versión y correlación y devuelve `data` o un código en `error`. El host limita la petición a 2 KiB, la respuesta a 512 KiB, la espera del canal a 2 s y el intercambio a 5 s. Una cancelación activa, un timeout o una pérdida de transporte descartan esa conexión. La siguiente acción explícita inicia otro sidecar. Una mutación interrumpida puede haber llegado a SQLite. Recargar Archivos permite consultar el resultado antes de repetirla.

| Operación | Acceso |
|---|---|
| `list`, `load` | Catálogo de contenedores y proyección del contenedor del piloto con estado, IDs, etiquetas, disponibilidad e icono PNG. |
| `open`, `rename`, `reorder` | Identificador de referencia dentro del grupo autorizado. NexCore resuelve el destino y valida pertenencia y tipo. |
| `placement`, `collapse` | Geometría lógica del host y presentación del grupo. El alto expandido se conserva durante la contracción. |
| `cancel_core` | Desde Archivos, cancela el intercambio en curso del canal compartido. |

Las reglas de datos usan `ContainerService`, los repositorios SQLite existentes y `WindowsContainerItemPlatformService`. El sidecar admite exclusivamente los destinos TXT de la carpeta de fixtures para inspección y apertura. Los iconos proceden de la asociación Shell de los TXT y se reutilizan por extensión. El popover Archivos solo carga y abre referencias. La geometría se restringe al área de trabajo del monitor principal. Al terminar el host se cierran los pipes y el sidecar sale al recibir EOF. Los diagnósticos de error se guardan en `pilot-data/core.log`.

## Repetir la validación

```powershell
python -X utf8 prototypes/web-shell/bench/Test-Core.py
python -X utf8 prototypes/web-shell/bench/Test-Recovery.py
./prototypes/web-shell/bench/Measure.ps1 B one
./prototypes/web-shell/bench/Measure.ps1 B three
./prototypes/web-shell/bench/Measure.ps1 B exercise -Capture
./prototypes/web-shell/bench/Test-Lifecycle.ps1 stress
./prototypes/web-shell/bench/Test-Lifecycle.ps1 startups
$p = Start-Process ./artifacts/tauri-nexcore/B/nexspace-tauri-lab.exe -ArgumentList '--one','--faults' -PassThru
$p.WaitForExit(30000)
$p.ExitCode
```

`Test-Core.py` abre una ficha TXT mediante el Shell y restaura orden, etiqueta y presentación al finalizar. Las escenas guardan JSON y, opcionalmente, capturas privadas en `artifacts/tauri-nexcore/evidence/`. `--faults` termina únicamente el hijo propio, verifica el fallo de la solicitud siguiente y exige que una nueva conexión recupere el mismo objeto. La serie de estrés espera que React confirme una nueva carga con datos para cada apertura. El muestreo del árbol usa parentesco de procesos, memoria privada y WS agregados, CPU, handles, GDI, USER y un censo acotado de Composition/Event. El WS agregado incluye páginas compartidas. Los cierres se evalúan por PID y hora de creación de procesos observados.

La decisión de migración y las cifras de esta ejecución se encuentran en [la validación del piloto](VALIDACION_PILOTO.md).
