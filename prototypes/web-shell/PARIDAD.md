# NexContainers y NexDock sobre Tauri

La iteración posterior documenta la corrección del arrastre, Shell, monitores y las mediciones con reposo prolongado en [Validación de producto](VALIDACION_PRODUCTO.md).

Base histórica `695f70f`. Rama `experiment/tauri-nexcore`. Validación local en Windows 11 x64 el 23 de noviembre de 2026.

## Ejecutar

Desde `prototypes/web-shell`, ejecutar `./Build-Pilot.ps1` con Node/npm, Rust/Cargo para Windows x64 y el SDK .NET 10 en PATH. El script acepta las rutas de Cargo y dotnet. Arrancar `artifacts/tauri-nexcore/B/nexspace-tauri-lab.exe` desde la raíz del proyecto. La primera preparación crea tres contenedores, recursos de demostración locales, tres categorías enlazadas y una sesión abierta en `pilot-data/floating.db`. Las siguientes ejecuciones conservan la biblioteca.

Con una salida nueva, `-DemoDatabase` toma una instantánea SQLite de una biblioteca **de prueba** abierta en modo de solo lectura. Las referencias conservan sus destinos originales. Elegir el engranaje del dock para gestionar contenedores, categorías, orientación y perfiles. En un grupo se puede usar **Opciones** para añadir archivos o carpetas y **Mayús+F10** sobre una referencia para gestionar sus metadatos. El agarre inferior admite las flechas de teclado.

## Contrato y propiedad de los datos

| Entrada | Operaciones |
|---|---|
| Host nativo | `startup` aplica la política de inicio. `manifest` devuelve el perfil activo, los grupos, geometrías, visibilidad, orientación y categorías. |
| Contenedor | `createGroup`, `renameGroup`, `deleteGroup`, `show`, `lock`, `collapse`, `listView` y `placement`. La altura expandida se conserva al mover un grupo contraído. |
| Categoría | `createCategory`, `editCategory`, `deleteCategory`, `moveCategory` y `vertical`. Una categoría puede usar referencias propias o resolver las de un contenedor. |
| Referencia | `page` devuelve 24 entradas como máximo, con disponibilidad e iconos. `open`, `rename`, `remove` y `moveItem` operan por identificador. `add` recibe rutas seleccionadas en el host o entregadas por el sistema al soltar. |
| Perfil | `createProfile` admite duplicar el activo. `renameProfile` y `switchProfile` delegan en ProfileService. Las ventanas del perfil saliente se retiran antes de mostrar las del entrante. |

El IPC usa JSON por línea en UTF-8. Cada solicitud lleva versión 2, correlación y operación. Las acciones sobre la biblioteca incluyen el perfil esperado. El host limita la petición a 32 KiB, la respuesta a 512 KiB, la espera por el canal compartido a dos segundos y el intercambio a cinco. La respuesta contiene `v`, `id`, `data` y `error`. Rust valida el alcance de la ventana emisora. Un fallo de transporte descarta el hijo afectado. La solicitud posterior puede restablecer la conexión. La ventana muestra el fallo y permite reintentar.

## Matriz de validación

| Estado | Evidencia |
|---|---|
| PASS | Solución x64 Debug y Release. En cada configuración pasaron 92 pruebas unitarias y 35 de integración. TypeScript/Vite, ejecutable Tauri Release y sidecar .NET autocontenido compilados. El enlazador GNU avisa sobre manifiestos de recursos múltiples. |
| PASS | `python bench/Test-Floating.py` ejercitó grupos, referencias, listas, orden, categorías propias y enlazadas, bloqueo, geometría, contracción, perfiles independientes y reinicio real del sidecar en una copia SQLite. Confirmó errores de duplicado, destino ausente y perfil obsoleto. |
| PASS | En ventanas reales, el dock abrió el popover Juegos con los nombres de la biblioteca. Crear y activar un perfil vacío dejó visibles el organizador y NexDock. Volver a Principal restauró Archivos, Juegos y Aplicaciones. |
| PASS | Ocultar NexDock con Alt+F4 y pulsar Ctrl+Alt+Mayús+Inicio desde Explorer devolvió la ventana al inventario visible. La captura de teclado mostró el indicador de foco. |
| PASS | `--verify-floating-ipc` terminó el sidecar del propio host, observó el fallo de la siguiente lectura, obtuvo un manifiesto idéntico desde otro hijo y salió con código 0. |
| PASS | El ensayo de referencias verificó las rutas y hashes de TXT, PDF, carpeta y acceso directo tras las operaciones de metadatos. Una referencia ausente informó `missing`. |
| PASS | Al usar `Test-Floating.py --open-folder`, la solicitud de apertura por ID devolvió éxito y Explorer presentó exactamente la carpeta aislada. |
| PASS | `Test-Offscreen.py` inició el ejecutable con Archivos guardado en (−65000, −65000). Windows informó un rectángulo visible de (0, 0) a (386, 272). La prueba inspecciona el HWND del proceso propio. |
| BLOCKED | Gesto físico desde Explorer. La herramienta interactiva limita ambos extremos del arrastre a una sola ventana. El ensayo deja un verificador independiente de la IU para comprobar el resultado del gesto manual. En la última lectura había cero de cuatro destinos de este gesto en SQLite y los hashes originales coincidían. El selector nativo de archivos y carpetas es la alternativa disponible. |
| NOT_TESTED | Desconexión física de pantalla, DPI mixto, clic en el menú del área de notificación, lectura con Narrador y recorrido visual de todos los temas de WinUI. |

Para verificar el gesto físico, ejecutar `python bench/Explorer-Drop.py`, arrastrar los cuatro elementos de la carpeta abierta al contenedor Archivos y ejecutar `python bench/Explorer-Drop.py --verify DIRECTORIO_DEL_ENSAYO`. La comprobación exige las cuatro rutas, el tipo de cada referencia y el mismo árbol de hashes que antes del gesto.

## Comparación con WinUI

Ejecutar `bench/Compare-Floating.ps1 -Engine WinUI -Cycles 100` y repetir con Tauri y con 300 ciclos, un ensayo a la vez. Requiere una compilación Release x64 de NexSpace.UI. `python bench/Summarize-Floating.py` comprueba finalización, mismo SHA-256 de la instantánea y descendientes observados tras el cierre. Las trazas por PID y las capturas se guardan en `artifacts/tauri-nexcore/evidence/paridad`, excluido de Git.

Ambas tecnologías abrieron y cerraron el primer contenedor de la misma biblioteca con una ventana de gestión residente, esperas de 100 ms por transición y diez segundos de reposo. Tauri esperó la confirmación de React y WinUI comprobó la destrucción de los HWND. WinUI usa NexManager y Tauri usa el organizador ligero, por lo que el contenido de la ventana residente difiere.

| Motor | Ciclos | Privada pico / reposo (MiB) | CPU observada (s) | Máx. procesos | Handles | GDI | USER | Event | Composition |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| WinUI | 100 | 143 / 139 | 30,4 | 1 | 2366 | 85 | 42 | 1484 | 114 |
| Tauri | 100 | 250 / 202 | 42,6 | 10 | 4756 | 93 | 141 | 1741 | 0 |
| WinUI | 300 | 147 / 146 | 74,7 | 1 | 2743 | 77 | 38 | 1677 | 314 |
| Tauri | 300 | 269 / 219 | 130,4 | 10 | 4953 | 96 | 148 | 1800 | 0 |

Las últimas cinco columnas corresponden a la última muestra. Los cuatro ensayos completaron los ciclos con salida 0, dentro de presupuesto y con cero descendientes conocidos a los tres segundos. El SHA-256 inicial común fue `B042EF79F66FF16D899F08B37CDD9E640C1FBA6986F369A68F429A6B63B0CEA6`. WebView2 escribió el aviso 1412 al desregistrar `Chrome_WidgetWin_0` en la repetición final. Merece seguimiento junto con los contadores en reposo.

La CPU es la suma del último tiempo acumulado observado por PID. El muestreo puede perder procesos efímeros. Composition cuenta handles con ese nombre que el sondeo pudo clasificar. Un cero en Tauri no mide el trabajo de GPU ni el coste del compositor de Windows. El descenso entre pico y reposo muestra retención temporal. Comparar los finales de ejecuciones independientes de 100 y 300 ciclos no demuestra por sí solo la presencia ni la ausencia de fugas.

## Alcance pendiente

Las capturas privadas de Archivos y NexDock proceden de ventanas reales de la compilación final. La de Juegos documenta el popover abierto durante el recorrido interactivo previo al ajuste del tamaño de iconos.

La geometría del piloto se limita al área de trabajo primaria. Los archivos con asociación disponible usan iconos del sistema y las carpetas usan un glifo de respaldo. Las superficies translúcidas se renderizan en WebView2. La paridad con Acrylic, el tema Automático y la accesibilidad de producción requiere ensayos específicos. Las capturas muestran datos de demostración, con carpetas y documentos reales también en los grupos Juegos y Aplicaciones.

**Recomendación.** Conservar el piloto como rama experimental. La siguiente aceptación debe cubrir el arrastre físico, bandeja, múltiples pantallas y un perfilado prolongado con una biblioteca de uso representativo. NexManager y la aplicación de producción siguen en WinUI.
