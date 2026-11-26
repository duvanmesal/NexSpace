# NexSpace Tauri · Aceptación técnica para beta privada

24 de noviembre de 2026 · Windows 11 x64 26H2, compilación 26300.9974 · Base `c594359`

La revisión del 25 de noviembre de 2026 incorpora la aceptación de un gesto físico revalidado. El detalle de los lotes OLE y el procedimiento con una carpeta de origen por sesión están en [Entorno Tauri](ENTORNO_TAURI.md).

**Dictamen.** El piloto sirve para una evaluación interna supervisada con una biblioteca aislada. La aceptación de una beta privada de uso diario queda pendiente de la validación física del cierre de menús entre ventanas. Para incluir equipos con escalas distintas o monitores que se desconectan durante el uso, también debe completarse el recorrido de pantallas.

## Matriz de aceptación

| Caso | Estado | Evidencia y condición de cierre |
|---|---|---|
| Arrastre físico de TXT, PDF, carpeta y acceso directo | PASS | Gesto humano desde Explorer al grupo Archivos de la sesión gestionada. Cuatro altas OLE, cuatro referencias en SQLite, hashes intactos y cero altas exitosas por selector o IPC de prueba en el grupo. [Alcance y trazabilidad](#arrastre-físico-aceptado) |
| Referencias, SQLite e iconos Shell | PASS | El contrato comprueba TXT, PDF, carpeta, acceso directo y ejecutable de prueba, PNG del Shell en tres tamaños solicitados, errores tipados y conservación de originales. |
| Reinicio frío de la aplicación | PASS | Dos procesos nuevos de Tauri abrieron la misma copia tras cierres abruptos controlados. Restauraron un perfil duplicado activo, el dock vertical y el grupo contraído con su geometría guardada. Volver a abrir NexCore devolvió las mismas referencias y PNG. `PRAGMA quick_check` devolvió `ok`. |
| Cambio de perfiles y reconexión | PASS | La validación de producto anterior ejecutó 20 rondas con popovers, terminó el sidecar propio y comprobó la reconexión. El ensayo actual confirma aislamiento de perfiles y restauración del perfil duplicado entre procesos. [Validación de producto](VALIDACION_PRODUCTO.md) |
| Registro del icono en la bandeja | PASS | `Shell_NotifyIconGetRect` devolvió `S_OK` para el icono del PID de ensayo. El rectángulo devuelto fue (1666, 1032)–(1698, 1080). [API de Windows](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyicongetrect) |
| Menú de la bandeja y recuperación por clic | PASS | La sesión `9af64c4660b041afb197de508c855aa7`, PID `1912`, confirmó que NexDock se ocultó, el operador eligió la recuperación desde el menú nativo y la misma solicitud dejó visible el dock. [Verificación guiada](../../tools/test-center/README.md#recuperación-desde-la-bandeja) |
| Convivencia FLOATING con Explorer | PASS | La sesión `931e1f1f80324b9b9ee027359c8ddc50` del build `e76b8ebb-75ea8199bf48480692b8970d0acaa931` confirmó observaciones durante y después, continuidad de Explorer, cierre nativo y hashes intactos. [Recorrido de Explorer](../../tools/test-center/README.md#convivencia-con-explorer) |
| Cierre de menús de isla al cambiar de ventana | BLOCKED | La regresión `panel-menus` cubre los menús React y los avisos de foco recibidos por el frontend. Queda pendiente la comprobación física de clic exterior, Escape y selección tanto en Opciones como en el menú de archivos del build corregido. [Recorrido de menús](ENTORNO_TAURI.md#cierre-de-los-menús-de-las-islas) |
| Colocación y traslado a monitor secundario real | PASS | Archivos inició en (−1870, 50)–(−1484, 322). En la interfaz, «Mover a otra pantalla» trasladó Aplicaciones a (−1153, 380)–(−767, 652). SQLite guardó el dispositivo secundario y las mismas coordenadas. Windows informó conciencia DPI por monitor y 96 DPI en la sesión. [API de Windows](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdpiforwindow) |
| Arranque con un dispositivo guardado ausente | PASS | La prueba asignó un identificador de monitor ausente en SQLite de ensayo. Archivos inició visible en el primario en (0, 0)–(386, 272). La ubicación histórica se conservó en la copia. |
| Monitores al 100 % y reconexión | PASS | Sesión `5f4f1ddb4eb64d93b22f09cfde5c4e36`, build `77fb99f0-458bb07401724084a1de8b356e5e2bae`. Traslado, retorno, integridad y cierre acreditados. [Evidencia y cronología](#monitores-al-100--aceptados) |
| DPI mixto y desconexión/reconexión en caliente | BLOCKED | El preflight del 26 de noviembre de 2026 midió dos pantallas a 96 DPI (100 %). El escenario físico requiere escalas distintas y permanece pendiente. Se conservan las comprobaciones previas de arranque en secundario y dispositivo guardado ausente. [Recorrido guiado](../../tools/test-center/README.md#dpi-mixto-y-reconexión) |
| Teclado, foco visible y UI Automation | PASS | Dos Tab mostraron el foco en Organizar y Archivos. Enter abrió el popover y Escape lo cerró. Enter volvió a abrir la misma categoría. UIA expuso los nombres de las referencias y el botón de cierre. |
| Narrador | PASS | En el Resumen de voz, el lector anunció «Archivos, botón de activación» en NexDock y «Opciones», «Cerrar categoría» y «Proyectos» en el popover. Se comprobó un recorrido de teclado. [Resumen de voz de Narrador](https://support.microsoft.com/es-es/accessibility/windows/narrator/complete-guide-to-narrator) |
| Accesibilidad física del build candidato | PASS | `windows-accessibility`, sesión `8d806d8bd82e4b74bf6469ab6c65c3c4`, build `7dd907bc-54c3ec43cb3c4a7394d2481044030650`, SHA `7dd907bc8da6be93da0b41e39ce6c02cd7bbf0b3`, host 26236 y NexCore 11144. Test Center aprobó 4/4 criterios con 0 fallos y 0 bloqueos. El operador confirmó las nueve comprobaciones de teclado y Narrador. La evidencia de UI Automation se conserva con el resultado original. Salida ordinaria con cero procesos residuales entre los 13 observados. |
| Compilación y regresión | PASS | Build Release de Tauri, Vite y sidecar autocontenido. Pasaron 92 pruebas unitarias y 35 de integración de .NET tanto en Debug como en Release, y dos pruebas Rust de geometría. Persiste el aviso del enlazador GNU por manifiestos de recursos múltiples. |
| Reposo con topología fija | PASS | 604 segundos y 117 muestras con las mismas cuatro ventanas y los mismos 12 procesos. Memoria privada de 304,56 a 273,69 MiB. GDI permaneció en 109. La CPU acumulada creció 0,64 s. El resultado cubre la duración y topología del ensayo. |
| Reposo prolongado y respuesta al finalizar | PASS | El 26 de noviembre de 2026, la sesión `4decbf47178a4be089d05db09e42dd7c` del build `bbff8671-a21c6b50db874902b4002737f73868c1` (commit `bbff8671eedd5af8bd0ffd6b012cc26bae91b47e`) aprobó 3/3 criterios, con cero fallos y bloqueos. Duración total: 12.485,433 s. Reposo: 10.860 s con 12 procesos y cuatro ventanas. 197 muestras totales, 182 de reposo. Variación de las medianas entre el primer y último bloque: memoria privada +1,63 MB, conjunto de trabajo −118,4 MB, handles +22, GDI y USER sin variación. CPU estable según el criterio documentado. Respuesta física de NexDock y las islas confirmada. Salida ordinaria con cero procesos residuales entre los 12 observados. [Criterio de reposo](../../tools/test-center/README.md#reposo-prolongado) |
| Cierre del árbol de procesos | PASS | El reposo acabó con terminación controlada por el ensayo y cero descendientes conocidos a los 3 s. La validación de producto anterior comprobó la salida normal de los ciclos. El comando de salida del menú físico permanece sin verificar. |

Los resultados de pruebas automatizadas y de teclado no sustituyen las comprobaciones físicas. La matriz conserva la distinción entre una prueba bloqueada por el control de escritorio y un defecto del piloto. El perfilado en reposo mide una sola sesión y una sola biblioteca.

## Arrastre físico aceptado

El operador arrastró **Proyecto**, **Acceso.lnk**, **Muestra.pdf** y **Notas de prueba.txt** desde Explorer hasta Archivos el 25 de noviembre de 2026 a las 10:31:54 en Bogotá. El verificador volvió a leer la sesión original y devolvió **PASS**.

- Sesión `bcbf8d92f0094c6cb2dbc4c4be989051`, PID `4200`, lote OLE `1`.
- Perfil Principal `00000000-0000-0000-0000-000000000001`, grupo `702dc3e4-bdf8-411c-81e1-1229db5d2a2d`.
- Build `95fae67d-729f32df981141d192153c3eca8bab90`, SHA base `95fae67d1a92290a04b7b2c7fc6a93b9c51bab44` con cambios locales de herramientas al compilar.
- Cuatro rutas correctas con su clase persistida, cuatro altas de NexCore con origen OLE, cero rutas ajenas en Archivos y hashes de origen intactos.

El PASS cubre este escenario con biblioteca aislada en Windows 11. Los resultados para otros DPI, topologías o builds requieren su propia evidencia. El [resumen sanitizado](../../tools/test-center/accepted-scenarios.json) queda disponible desde Test Center. Las trazas y rutas completas permanecen en evidencias locales.

Se midió el ejecutable `c594359` con tres contenedores y NexDock, el flujo ordinario de arranque y sin registrar entradas en la sesión medida. El ejecutable de aceptación añade trazas solo con la variable de diagnóstico. El primer intento del ensayo incluyó una ventana auxiliar sin título y rechazó la topología. La repetición contó solo ventanas con título del PID y exigió que se conservaran los mismos HWND y PID en cada muestra. Los 12 procesos incluyen el host, NexCore, WebView2 y un proceso de consola. La memoria privada osciló entre 272,68 y 307,51 MiB. El working set sumado incluye páginas compartidas. Los 60 handles adicionales de tipo Event se incluyen en el aumento total de 176 handles. El comportamiento observado aún no determina si existe una fuga.

## Monitores al 100 % aceptados

El 26 de noviembre de 2026, `windows-monitors-100` aprobó 3/3 criterios en 147,111 s. El build `77fb99f0-458bb07401724084a1de8b356e5e2bae` corresponde a `77fb99f090fb90af64c3f3c6704f7536b27e8180`. Se verificaron el traslado y posición en SQLite, la recuperación automática al desconectar, el retorno al reconectar, NexDock operativo, cuatro ventanas accesibles e integridad de perfiles, referencias y archivos. La salida ordinaria dejó cero procesos residuales entre 12 observados.

- `0b67d57abde84fd3a3774eabe10b5d13` · **FAIL**. Archivos requirió intervención al desconectar. El host se corrigió para recuperar la visibilidad sin tomar el foco, preservando la posición guardada.
- `13a246419d69497187091794887cac63` · **BLOCKED**. La observación de traslado tenía una topología distinta de la inicial porque se registró tras desconectar.
- `5f4f1ddb4eb64d93b22f09cfde5c4e36` · **PASS**. El recorrido completo confirmó la corrección. La evidencia privada incluye el veredicto, preflight, cuatro puntos de control, cierre y `monitors.jsonl`. [Índice de aceptación](../../tools/test-center/accepted-scenarios.json)

## Repetir las comprobaciones

- **Arrastre y bandeja.** Preparar cada ensayo mediante [Test Center](../../tools/test-center/README.md). Para la bandeja, ejecutar `-Test windows-tray` y conservar el ID para `-Verify -Session ID`. La opción «Cerrar NexSpace experimental» emite `tray-quit` en la traza habilitada y queda fuera de los criterios de recuperación.
- **Pantallas y persistencia.** Ejecutar `python bench/Test-Offscreen.py RUTA_DEL_PILOTO --secondary`, repetir con `--missing-monitor` y ejecutar `python bench/Test-Floating.py --pilot RUTA_DEL_PILOTO --shell-icons --host-restart`. Las pruebas crean copias aisladas.
- **Reposo.** Ejecutar `./bench/Compare-Floating.ps1 -Engine Tauri -SoakMinutes 10 -PilotDirectory RUTA_DEL_PILOTO -EvidenceDirectory RUTA_PRIVADA`. Se ejecuta el modo flotante ordinario con tres contenedores y un dock. El ensayo exige los mismos procesos y cuatro ventanas con título, y espera a que el arranque se estabilice antes de medir. Al final el propio ensayo termina el árbol que lanzó. El cierre de producto se mide aparte. Los JSON privados de esta sesión están en `artifacts/tauri-nexcore/evidence/beta`.
