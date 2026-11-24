# NexSpace Tauri · Validación de producto

23 de noviembre de 2026 · Rama `experiment/tauri-nexcore` · Windows 11 x64

## Arrastre de Explorer

La revisión `4454a4e` escuchaba `WebviewEvent::DragDrop`. En `tauri-runtime-wry` 2.12.5, un WebView creado dentro de `WebviewWindowBuilder` usa `WebviewKind::WindowContent`. El runtime convierte el arrastre de ese tipo en un `WindowEvent::DragDrop`. La suscripción anterior correspondía a WebViews secundarios. La corrección conecta el evento de ventana al flujo real de NexCore. [Código del runtime](https://docs.rs/crate/tauri-runtime-wry/2.12.5/source/src/lib.rs)

Wry 0.57.3 obtiene rutas del objeto OLE, convierte la posición de pantalla a coordenadas físicas del cliente y anuncia `DROPEFFECT_COPY`. NexSpace toma el destino del identificador nativo de ventana, que incluye perfil y grupo o categoría. Las coordenadas lógicas se usan en el diagnóstico. El frontend solo recibe el estado visual y un eventual error. No recibe rutas ni decide el destino. NexCore exige el perfil activo, una ruta absoluta existente y el tipo compatible. El repositorio inserta metadatos en SQLite. [Destino OLE de Wry](https://docs.rs/crate/wry/0.57.3/source/src/webview2/drag_drop.rs)

En superficies transparentes, suelta dentro de la tarjeta visible y comprueba el contorno azul. Las esquinas y el espacio exterior no forman parte de la superficie esperada. Los datos virtuales de un origen que no entregue rutas de archivos quedan fuera de esta prueba. La sesión observada ejecutó Explorer y Tauri con integridad media `0x2000`. Una diferencia de elevación puede bloquear la interacción antes de que llegue al host. Si ocurre, usa una instancia del piloto abierta normalmente y conserva el selector nativo como alternativa. [Aislamiento de privilegios de Windows](https://learn.microsoft.com/en-us/troubleshoot/power-platform/power-automate/desktop-flows/ui-automation/uipi-issues)

### Reproducción física

1. Compilar con `prototypes/web-shell/Build-Pilot.ps1` y ejecutar desde la raíz `python prototypes/web-shell/bench/Explorer-Drop.py`. El comando crea un TXT, un PDF, una carpeta y un acceso directo aislados. Abre Explorer y NexSpace con trazas activadas e informa las integridades de cada proceso. Para usar una compilación alternativa, pasar su carpeta con `--pilot`.
2. Colocar Explorer y Archivos uno al lado del otro. Seleccionar los cuatro elementos y arrastrarlos dentro de la tarjeta. Observar el contorno durante la entrada y las nuevas referencias al soltar.
3. Ejecutar `python prototypes/web-shell/bench/Explorer-Drop.py --verify DIRECTORIO_DEL_ENSAYO`. Verifica en SQLite las cuatro rutas y tipos y compara rutas y SHA-256 de los originales. Repetir en un monitor secundario cuando corresponda.
4. En `%TEMP%/NexSpace-Floating-PID-*/drop.jsonl`, buscar `enter`, `drop`, cuatro respuestas exitosas `nexcore` y `persisted`. La traza registra la etiqueta de destino, el recuento, las dos coordenadas y la escala. Omite los nombres y rutas personales. Si hay `enter` sin `drop`, comprobar si el puntero salió del destino antes de soltar. Si hay `drop` sin persistencia, consultar el código de NexCore.

**Arrastre físico pendiente.** El control disponible usa coordenadas relativas a una sola ventana para los dos extremos de un gesto. Las pruebas por IPC no cuentan como evidencia de arrastre.

## Iconos y pantallas

- Shell devuelve el icono de carpeta o aplicación mediante `IShellItemImageFactory` y el de documento o acceso mediante `SHGetFileInfo`. Se pide la superposición Shell para accesos directos. El sidecar libera los recursos nativos y conserva PNG en una caché acotada. Se comprobó que carpeta, aplicación, acceso directo, TXT y PDF entregan PNG con las tres resoluciones de petición. Las asociaciones y superposiciones usan la resolución que devuelve Shell. Las entradas sin icono usan un glifo. [Iconos de Shell](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)
- La geometría se restaura por dispositivo y se ajusta a su área de trabajo física. En un traslado responsivo a otra escala, el host conserva el tamaño lógico en relación con el punto de agarre. Si falta el monitor, la ventana vuelve al área primaria. La topología se comprueba en segundo plano cada tres segundos. La comprobación solo recoloca superficies si detecta un cambio. Un monitor restaurado recupera su geometría original mientras no se guarde otro emplazamiento. El dock lee su monitor desde la configuración del perfil. [Coordenadas físicas de Tauri](https://docs.rs/tauri/2.12.7/tauri/webview/struct.WebviewWindow.html)
- La geometría guardada sigue en píxeles físicos de SQLite v3. Al cambiar resolución, el ajuste mantiene visible el panel y conserva las dimensiones guardadas donde caben. La posición no se guarda como porcentaje de la pantalla.

## Matriz de aceptación

| Prueba | Estado | Evidencia |
|---|---|---|
| Contrato NexCore y originales intactos | PASS | `Test-Floating.py --shell-icons` comprueba adición, tipos, persistencia, perfil obsoleto, referencias ausentes e iconos. |
| Compilación y pruebas | PASS | Debug y Release x64 de la solución. Pasaron 92 unitarias y 35 de integración en cada configuración. Vite, Rust y sidecar autocontenido Release compilados. Las dos pruebas Rust cubren ajuste del rectángulo. El enlazador GNU advierte sobre manifiestos de recursos múltiples. |
| Perfiles, popovers y recuperación nativa | PASS | El host ejecutó 20 rondas entre dos perfiles, con apertura de un popover, retirada de las ventanas del perfil saliente y regreso al inicial. Terminó el sidecar propio, observó la desconexión, restauró el manifiesto y mostró el dock desde el recuperador nativo. |
| Arrastre físico | BLOCKED | El gesto entre aplicaciones queda pendiente. El flujo corregido y la traza pueden probarse con el procedimiento anterior. |
| Geometría fuera de pantalla | PASS | El ejecutable reubicó un contenedor guardado en (−65000, −65000) a un rectángulo visible. Las pruebas Rust cubren origen negativo y reducción del área de trabajo. |
| Arranque en segunda pantalla real | PASS | Windows situó Archivos en el rectángulo (−1870, 50)–(−1484, 322), dentro de la zona de trabajo del monitor no primario. |
| DPI mixto y desconexión física | NOT_TESTED | Ensayar con distintas escalas y desconectar físicamente el monitor que contiene un grupo. No cambiar configuraciones de pantalla para este ensayo. |
| Navegación, foco y nombres inspeccionados | PASS | Se comprobó la navegación con Tab, foco visible y nombres en UI Automation. El encabezado ofrece objetivos de 42 × 40 px CSS y el agarre de 24 × 24 px. Las categorías del dock tienen 54 × 52 px. Sobre una base blanca, el contraste calculado de texto y foco de la superficie base oscura es de 7,67:1 y 4,93:1. La estimación no cubre cada combinación de estados. |
| Narrador | NOT_TESTED | Abrir Narrador en una sesión autorizada y recorrer encabezado, opciones, cuadrícula, popover y aviso de referencia ausente. Comprobar nombre, rol, estado y que Escape vuelve a la categoría. |
| Bandeja de Windows | NOT_TESTED | El ensayo invoca el mismo recuperador nativo. La inspección del menú real del área de notificación queda pendiente. |
| Capturas por ventana | PASS | Capturas privadas de grupos expandidos, Juegos contraído, los dos docks, popover y error. `Explorer-Drop.py --demo` reproduce la escena de datos por IPC. |
| Captura conjunta sobre fondo de demostración | BLOCKED | Se verificaron capturas directas de cada ventana. El fondo de ensayo no se pudo seleccionar con el control disponible. |

## Recursos

Una misma copia SQLite, un grupo variable y una ventana de gestión residente por motor. Las esperas de cada fase son 100 ms antes y después del cierre. Tauri espera la confirmación React. WinUI comprueba que los HWND se destruyeron. Se esperan diez segundos en la serie de 100 ciclos y un minuto en la de 300. El contenido residente es distinto entre motores. Los JSON privados preservan tiempos, PID y recursos de cada muestra.

| Motor | Ciclos | MiB privados pico / reposo | CPU observada (s) | Handles al final | GDI | USER | Event | Composition |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| WinUI | 100 | 147 / 144 | 32,3 | 2356 | 77 | 40 | 1483 | 114 |
| Tauri | 100 | 263 / 207 | 42,3 | 4766 | 91 | 138 | 1743 | 0 |
| WinUI | 300 | 152 / 150 | 96,6 | 2827 | 109 | 71 | 1697 | 314 |
| Tauri | 300 | 279 / 216 | 116,8 | 4732 | 91 | 137 | 1726 | 0 |

La instantánea de entrada tuvo el mismo SHA-256 en las cuatro ejecuciones. Todas completaron los ciclos y salieron con código 0. El sondeo observó un máximo de 1 proceso de WinUI y 10 de Tauri. A tres segundos del cierre no encontró descendientes conocidos. El acumulado de CPU muestreado puede omitir procesos efímeros y un cero en Composition solo indica que no se observaron handles de ese tipo en esos procesos. WebView2 registró el aviso 1412 al desregistrar Chrome_WidgetWin_0 al cerrar.

En los últimos 49 segundos de la serie de 300, Tauri se mantuvo entre 216,0 y 217,4 MiB y entre 4730 y 4737 handles. WinUI se mantuvo entre 150,0 y 150,4 MiB y entre 2827 y 2847 handles. La caída desde el pico muestra retención temporal. El muestreo de un minuto no permite concluir que exista o se descarte una fuga a largo plazo. La serie WinUI incluyó parte de la inspección visual en la misma sesión y la carga de fondo no quedó controlada al nivel de un laboratorio dedicado.

**Recomendación.** Mantener Tauri como piloto y WinUI como interfaz de producción. Para aceptar el cambio, completar el arrastre físico, validar recuperación desde la bandeja y verificar DPI mixto y pérdida real de pantalla. Conviene repetir el perfilado con una biblioteca representativa de uso. Los 216 MiB privados de Tauri en reposo siguen por encima del objetivo orientativo de 150 MB del proyecto.
