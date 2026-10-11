# Validación de Fase 1 · NexContainers

## Cierre condicionado aprobado · 2026-10-11

El usuario acepta FLOATING y NexContainers como base funcional y autoriza Fase 2. Esta aprobación no transforma los pendientes en PASS: drag-and-drop físico sigue BLOCKED; accesibilidad, DPI/multimonitor, translucidez activa, animaciones y perfilado de memoria continúan pendientes. El resto del documento conserva la evidencia histórica, incluidas las restricciones vigentes durante esas sesiones.

Reverificación antes de publicar: restore y solución Debug/Release x64 PASS, 0 errores/advertencias; 15 unitarias y 15 de integración Debug PASS. Revisión de los árboles de 5d076a6 y 8f7b873 sin capturas privadas, bases, archivos generados ni patrones de secretos. El usuario autorizó explícitamente conservar los metadatos de autor de Git. Fetch mostró origin/main antecesor compatible (0 commits remotos exclusivos, 2 locales). Push normal de los dos commits completado y remoto comprobado en 8f7b873e457f6b03750ae92288b02cae3585559a. Sin force ni reescritura. Fase 2 permanece local hasta revisión.

**Fecha:** 2026-10-10. **Resultado:** validación funcional real amplia, pero aceptación integral pendiente. FLOATING es la única presentación activa; DESKTOP sigue experimental y deshabilitado. No se inició Fase 2.

**Consolidación final:** NexGlass, iconos Shell, ViewModel portable, procesamiento seguro de drop y refuerzo de recursos/permisos implementados. Restore, Debug/Release x64 y 15 unitarias + 15 de integración por configuración PASS. Pruebas nativas iniciales y consolidación se distinguen abajo. DnD físico permanece BLOCKED; no declarar aceptación integral ni iniciar Fase 2.

## Método, entorno y evidencia

Esta revisión utilizó interacción nativa real con WinUI, Explorer, selectores y Bloc de notas: clics, menús, teclado, arrastre de ventanas, redimensionamiento y capturas del equipo. No se sustituyeron estos escenarios por pruebas unitarias. Las comprobaciones reportadas anteriormente por el usuario para Fase 0 no se contabilizan como evidencia nueva de Fase 1.

- Windows 11, sesión observada de 1920 × 1080; SDK .NET 10.0.401 de `%USERPROFILE%\.dotnet`.
- Arranque unpackaged explícito: `dotnet build src/NexSpace.UI/NexSpace.UI.csproj -c Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false`.
- Datos persistentes aislados: `%TEMP%\NexSpace-Astra-20261010\data\nexspace.db`, seleccionados mediante `NEXSPACE_DATA_DIR` antes de iniciar el EXE. No se usó la biblioteca personal.
- Fuentes aisladas: `%TEMP%\NexSpace-Astra-20261010\fixtures`: dos documentos, una carpeta con documento y un acceso directo al documento de prueba. La apertura de una aplicación utilizó el ejecutable existente de Bloc de notas sin modificarlo.
- Capturas auténticas y trazas con hora UTC: `docs/testing/evidence/private/phase1-astra-20261010/`. Carpeta excluida de Git; contiene imágenes, `trace.json`, comparaciones de persistencia y semilla/resultado de recuperación. Algunas capturas de Explorer/selector/Bloc de notas muestran contexto personal incidental: mantenerlas privadas.
- No se ejecutó HWND_BOTTOM, no se reinició Explorer, no se cambiaron sus opciones/iconos, no se activó Developer Mode ni se alteró resolución/DPI.

Los estados siguientes distinguen PASS observado, FAIL inicial corregido, BLOCKED por herramienta y NOT_TESTED. Un PASS tiene el alcance exacto descrito, no certifica combinaciones ilimitadas.

## Compilación y pruebas automatizadas del recorrido inicial

Después de las correcciones iniciales (supersedido por los resultados de consolidación al final):

- Restore de solución x64: **PASS**.
- Solución Debug x64 y Release x64: **PASS**, cada una con 0 errores y 0 advertencias.
- Unitarias Debug y Release: **PASS, 9/9 por configuración**.
- Integración Debug y Release: **PASS, 9/9 por configuración**.
- Build unpackaged Debug x64 posterior: **PASS**, 0 errores y 0 advertencias; este binario se abrió y se probó nuevamente.
- Cobertura técnica: nombres/destinos, referencias compartidas, orden, SQLite tras reapertura, conservación de originales, bloqueo con redimensionamiento y excepción de recuperación, geometría contraída/expandida y tamaños mínimos, recuperación completamente visible, clasificación de recursos y análisis del selector multiselección/.lnk.

Las suites no equivalen a validación de drag-and-drop, DPI ni permisos reales.

## Escenarios funcionales y visuales del recorrido inicial

1. **Crear varios contenedores — PASS real.** Creación desde consola, coexistencia de dos ventanas y estado vacío. Evidencias 02, 13, 47.
2. **Renombrar contenedor — FAIL inicial → PASS repetido.** La consola cambiaba y la ventana abierta conservaba el nombre anterior. Ahora título y encabezado se actualizan inmediatamente. Evidencia 28; nombre final “Astra Trabajo Validado”.
3. **Abrir desde biblioteca — FAIL inicial → PASS repetido.** El clic de fila no reconocía el contenido devuelto por WinUI. Corregido; reapertura mediante un clic en ambos contenedores y en reinicios.
4. **Mover — PASS real.** Arrastre de región de título, coordenadas modificadas y conservadas. Tras desbloquear se repitió movimiento (+100/+82 en el recorrido observado). Evidencias 06, 34.
5. **Redimensionar — PASS real en tamaños probados.** Bordes/esquina, maximización, mínimo expandido 320 × 220 y scroll. Encabezado y botones accesibles; nombres largos se recortan con elipsis. Evidencias 08, 37, 42, 53, 55, 61. No es prueba de estrés con cientos de referencias.
6. **Contraer/expandir — FAIL inicial → PASS repetido.** Antes se ocultaba el contenido pero quedaba una ventana vacía grande. Ahora cambia el tamaño físico a 114 px de alto (AppWindow), conserva altura expandida y la restaura. Repetido tras dos reinicios. Evidencias 12 antes; 16, 17, 48–54 después.
7. **Bloquear/desbloquear posición — PASS real después de reforzar el bloqueo.** El arrastre bloqueado conserva coordenadas; desbloqueado permite movimiento; el borde permite redimensionar sin mover el origen bloqueado. Evidencias 18–19, 34, 55–56.
8. **Cuadrícula/lista y tres acentos — PASS real.** Alternancia, verde agua/cobre/azul y persistencia de configuración. Evidencias 20–21, 35, 38, 50, 52.
9. **Menús contextuales — PASS real para comandos ejercitados.** Abrir, renombrar referencia, mover arriba/abajo, quitar referencia, opciones de posición y presentación; menús de consola para renombrar/eliminar. Evidencias 10–11, 24–26, 44.
10. **Eliminar contenedor con confirmación — PASS real.** Cancelar lo conserva; confirmar lo elimina de la biblioteca sin cerrar la consola. Evidencias 44–46. La conservación física de fuentes al borrar un contenedor con referencias se cubre además en integración; el contenedor borrado en UI no tenía referencias.
11. **Selector de archivo — PASS real.** Documento aislado agregado y abierto con asociación a Bloc de notas. Selector cancelable; nombres con espacios admitidos. Evidencias 04–05.
12. **Selector de carpeta — PASS real.** Añadida carpeta aislada y abierta en Explorer; se observó su documento interior. Evidencias 22–23.
13. **Acceso directo .lnk — FAIL inicial → PASS repetido.** El selector anterior resolvía el destino en vez del .lnk, que aparecía como duplicado. Selector nativo con NODEREFERENCELINKS conserva el enlace; se añadió como referencia distinta y se abrió el texto correcto. Evidencias 30–31.
14. **Aplicación — PASS real para Bloc de notas.** Añadido notepad.exe por selector; doble clic abrió una ventana nueva vacía. Evidencias 39–40. No se certifican todos los ejecutables/asociaciones.
15. **Recurso del sistema — PASS real para AppsFolder.** Añadir “Aplicaciones instaladas” y abrirlo mostró Explorer en Applications. Evidencias 41–43. Otros destinos allowlist no se abrieron en esta revisión.
16. **Renombrar/reordenar referencias — PASS real.** Etiqueta “Nota organizada”, movimientos contextuales y orden conservado en dos reinicios. Evidencias 10–11, 24, 50, 52.
17. **Quitar referencia conservando original — PASS real + hashes.** Se quitó una referencia al documento aislado y luego se volvió a agregar. Los cuatro archivos de prueba conservaron SHA-256 inicial. Evidencia 26 y comprobación de archivos; no se modificaron originales personales.
18. **Referencia inexistente — PASS real.** Se renombró temporalmente solo el segundo documento aislado; al abrir la referencia apareció “No se encuentra el recurso.” sin terminar NexSpace. Se restauró el nombre y contenido original. Evidencia 25.
19. **Referencia inaccesible/acceso denegado — NOT_TESTED real.** No se alteraron ACL ni recursos privados. Hay manejo técnico de errores, no evidencia visual de denegación.
20. **Drag-and-drop de archivo desde Explorer — BLOCKED.** Hubo un gesto real documentado (32–33), pero la herramienta activa la ventana fuente y oculta el destino antes de arrastrar. No se añadió referencia; el gesto quedó dentro del Explorer de pruebas y no alteró fuentes. No permite concluir fallo o éxito del producto.
21. **Drag-and-drop de carpeta — BLOCKED por la misma limitación.** No se declara cubierto por el selector ni por tests. El drop asíncrono obtuvo deferral/finally como fortalecimiento técnico, sin PASS funcional.
22. **Persistencia — PASS real en dos ciclos consecutivos.** Cierre normal, relanzamiento con el mismo directorio y reapertura desde biblioteca de dos contenedores. Comparaciones de SQLite antes/ciclo1/ciclo2 idénticas excluyendo únicamente updated_utc: identidad, nombres, seis referencias, orden, posición, tamaño expandido, contracción, vista y acento. Capturas 49–52 y JSON privados. La biblioteca se restaura al iniciar; las ventanas se abren desde sus filas, no se reabren todas automáticamente.
23. **Varias ventanas — PASS real en dos contenedores.** Interacción independiente, consola disponible y cierre conjunto por Salir. No constituye prueba de carga masiva.
24. **Recuperación global sin foco — PASS real.** Ctrl+Alt+Shift+F12 enviado con Explorer en primer plano. Log floating-emergency-restore/global-hotkey sin error, manteniendo bloqueo y contracción. Repetido con binario final. Evidencias 57–60.
25. **Ventana fuera de pantalla — PASS real para recuperación al abrir.** Con NexSpace cerrado se sembraron x=y=100000 y monitor inexistente únicamente en SQLite aislado. Al abrir la fila, el contenedor bloqueado/contraído apareció enteramente accesible; coordenadas persistidas x=1579/y=781, monitor DISPLAY1, bloqueo/contracción conservados. Evidencia 59 y recovery-result.json. No es prueba de desconexión física de monitor.
26. **Cierre completo — PASS observado.** Varios cierres por Salir con dos contenedores; ciclos monitorizados de lanzamiento terminaron con exit code 0 (PID 26912 y 2424), recuento residual 0. Tras último ajuste, PID 6312 también terminó y recuento residual fue 0; esa observación no devolvió código de salida, por lo que no se atribuye un código. Se relanzó deliberadamente para inspección final.
27. **Explorer continúa operativo — PASS observado en el recorrido.** Navegación/ventanas de carpeta y AppsFolder funcionaron; mismo PID 4692 y misma hora de inicio durante comprobaciones. No certifica estabilidad prolongada fuera de la sesión.
28. **Cambios de resolución/DPI, desconexión de monitor — NOT_TESTED.** Se omitieron cambios de sesión por no disponer de una reversión visual garantizada. La consulta técnica de monitores y política de límites pasan.
29. **Lector de pantalla, navegación completa sin ratón y estrés prolongado — NOT_TESTED.** Se añadieron nombres accesibles a botones, pero no se confunde eso con auditoría de accesibilidad.

## Correcciones y repetición

- Región de arrastre separada de comandos/caption; encabezado y estado legibles, mínimos de tamaño y alto contraído real. Repetidos movimiento, tamaños, contracción/expansión y reinicios.
- Bloqueo real de coordenadas con redimensionamiento permitido; recuperación explícita puede cambiar coordenadas sin quitar el bloqueo. Repetidos arrastre bloqueado/desbloqueado, borde y recuperación.
- Guardado coherente del tamaño expandido antes de cambiar presentación y actualización del estado local después de persistir. Dos comparaciones completas de reinicio pasan.
- Refresco de título/nombre/conteos y revisión de consultas asíncronas para no aplicar listas antiguas. Renombre abierto y biblioteca repetidos.
- Apertura por fila corregida para Content/Tag retornado por WinUI. Repetida en cada relanzamiento.
- Selector nativo asociado al HWND, multiselección y no resolución de .lnk; FolderPicker con filtro requerido. Archivo, carpeta, enlace y ejecutable añadidos/abiertos de nuevo.
- Recuperación completamente dentro del área de trabajo, no solo una franja agarrable: evita ventanas bloqueadas con controles fuera de pantalla. Prueba unitaria nueva y semilla/arranque real pasan.
- Mensaje específico de referencia duplicada y deferral de drop: fortalecimiento técnico compilado; no se atribuye repetición visual del duplicado ni éxito de DnD.

## Integridad de fuentes

SHA-256 al terminar coincide con el inicial para:

- Nota de prueba.txt: 9F8E0367BF36D9A5D46A4BB347D8B8F0491EE7793293C136001669D04BD48436
- Segundo documento.txt: 2D1BB83981B2F34D70008147EE306D346E2D79A46BDDE71F1A6B05714D4F9FED
- Carpeta de prueba/Contenido.txt: 39AAAA05A9716B08219D3DD3B290D57E472BE29E9259826597BAF4A09B951160
- Nota acceso directo.lnk: 4748AB6C1F8FE8383BB22434C280BE825856FB4F63030CA54BCB94644850D4CD

No se borró la carpeta aislada: queda disponible para revisar evidencia y repetir reinicios.

## Evaluación frente al concepto C antes de consolidar

Inspección directa de la referencia aprobada y de ventanas reales: dirección oscura, acentos suaves, bordes finos, jerarquía de encabezado y espaciado funcional presentes. La revisión de diseño orientó la separación entre título nativo/acciones y la legibilidad; no se fabricó un render de evidencia.

**PARCIAL visual:** la superficie se percibe casi opaca, no ofrece el material translúcido/acrílico del concepto; los recursos usan glifos por categoría, no iconos nativos extraídos; la cuadrícula tiene celdas compactas con elipsis. La consola no es NexManager. El tamaño mínimo permite acciones y scroll, pero no mostrar todos los recursos a la vez. Menús y confirmaciones son funcionales en los escenarios probados. La aceptación estética final corresponde al usuario; no se afirma fidelidad completa al concepto.

## Guía de repetición del recorrido inicial

Para volver a abrir el mismo entorno de pruebas en PowerShell (cerrar primero la instancia actual mediante Salir):

```powershell
$env:NEXSPACE_DATA_DIR = Join-Path $env:TEMP 'NexSpace-Astra-20261010/data'
& "$env:USERPROFILE/.dotnet/dotnet.exe" build src/NexSpace.UI/NexSpace.UI.csproj -c Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
if ($LASTEXITCODE -eq 0) {
    Start-Process './src/NexSpace.UI/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/NexSpace.UI.exe'
}
```

Ejecutar desde la raíz del repositorio. El entorno se conserva entre inicios; no sustituir por un directorio nuevo para evaluar persistencia. La sesión se dejó abierta con consola y dos contenedores (capturas finales 63–65), después de comprobar salida sin residuos y relanzar deliberadamente.

1. En la instancia aislada abierta, abrir la carpeta fixtures. Calcular hashes de sus archivos; arrastrar un documento NUEVO para ese contenedor desde Explorer y después la carpeta hacia un contenedor vacío. Esperado: referencias añadidas, misma ubicación/contenido/cantidad de originales; comprobar apertura y quitar referencias. Repetir al menos un reinicio. No usar datos personales.
2. Probar acceso denegado solo con un recurso de prueba preparado por el usuario. Esperado: error legible, aplicación responde y ninguna modificación física. No alterar permisos del sistema.
3. Solo cuando el usuario pueda revertir el ajuste, probar escala/resolución y monitor secundario. Esperado: comandos accesibles, ventana recuperable mediante Ctrl+Alt+Shift+F12; restaurar ajustes al terminar. Esta ejecución no cambió ajustes.
4. Revisar nombres largos, listas grandes, teclado y lector de pantalla; comprobar controles/menús con el panel contraído y expandido. No se declara cubierto todo el producto por el recorrido de seis referencias.
5. Aprobar o pedir refinamiento de iconos/materiales/contraste frente al concepto C. FLOATING funcional no implica DESKTOP integrado.

## Consolidación final · 2026-10-10

### Entorno y distinción de evidencia

Se continuó sobre la misma biblioteca aislada persistente, sin usar la biblioteca personal. Tras la pausa se reenumeraron ventanas y procesos: Explorer tenía PID 2236 y hora de inicio 10:28 -05:00, diferente del recorrido inicial; **no se atribuye continuidad a PID 4692 entre sesiones**. PID 2236 permaneció sin reinicio durante la continuación. No se cambiaron sus opciones, iconos, Developer Mode, DPI/resolución ni se ejecutó HWND_BOTTOM.

La evidencia nueva está en `docs/testing/evidence/private/phase1-consolidation-20261010/`, ignorada por Git: PNG reales de ventanas/menús, trace.json con hora UTC/árbol nativo, binlogs de builds y cuatro TRX de pruebas, hashes y comparaciones SQLite. El set inicial de capturas permanece separado. Capturas de selector/Bloc de notas pueden mostrar nombres personales incidentales: no publicar esa carpeta. No se generaron renders ni composiciones como prueba.

Para ordenar la demostración se ajustaron únicamente geometría/preferencias en SQLite aislado **con la aplicación cerrada** (demo-layout-before.json). Esto es preparación de fixture, no prueba de movimiento por UI. Creación del tercer contenedor, adición de AppsFolder, cambio de vista, menús, reordenamiento, expansión/contracción y cierres sí se hicieron con interacción real. Se conservaron todos los originales y los cuatro SHA-256 iniciales.

### Mejoras implementadas y pruebas de regresión

- **NexGlass compartido:** paleta semántica oscura, colores de alto contraste, radios/bordes finos, Segoe UI Variable, títulos, botones discretos, estados nativos hover/selección/foco, plantillas de cuadrícula/lista y biblioteca. Estilos centralizados en Resources/NexGlass.xaml.
- **ReferenceTile:** iconos/miniaturas reales del Shell para archivos/carpetas/ejecutables/.lnk cuando están disponibles, glifo nativo de reserva; etiqueta debajo en grid y a la derecha en lista, tooltip/nombre accesible y estado inválido. El primer frame puede usar glifo mientras se carga el icono asíncrono.
- **Material real, no simulado:** DesktopAcrylicKind.Thin con configuración de SystemBackdrop y alternativa sólida. La sesión reportó AdvancedEffectsEnabled=False y AnimationsEnabled=False. **PASS: alternativa sólida legible; NOT_TESTED: acrílico con efectos activados y animación de entrada.** No se cambiaron ajustes para obtener una captura translúcida. Alto contraste implementado, prueba visual NOT_TESTED.
- **Encabezado compacto:** botón directo contraer/expandir, identidad/acento constantes, título con elipsis/tooltip y acciones accesibles. Menús de encabezado ahora se anclan debajo del botón: se repitieron menú de añadir, diálogo WinUI y cambio a lista en Release. Windows mantiene ajuste automático al área de trabajo.
- **MVVM del contenedor:** nuevo proyecto Presentation, estado observable/comandos tipados y bindings; tests de fallos, notificaciones y órdenes/quitar sin alterar originales. La consola mantiene coordinación code-behind como deuda acotada, no se afirma MVVM completo.
- **Recursos:** cache de iconos hasta 128 imágenes/4 cargas concurrentes, streams dispuestos y cancelación de actualizaciones tardías; controlador de material por ventana dispuesto al cierre. Shutdown no aplica refrescos tardíos, detiene timers y vacía cache. Esto no equivale a certificación de ausencia de fugas a largo plazo.
- **Corrección de permisos — FAIL inicial → PASS automatizado:** GetAttributes podía reportar disponible un archivo sin lectura. Se añade prueba de handle de solo lectura, sin leer contenido (carpetas: enumerador dispuesto). Test real aplica Deny solo al archivo temporal propio, verifica Inspect/Open como access-denied y restaura expresamente su DACL antes de comprobar contenido/limpiar. Mensaje de denegación en UI sigue NOT_TESTED.
- **SQLite:** disposición de conexión también si abrir/validar esquema falla. Diez fallos de esquema futuro liberan la base; reparación/reintento solo en fixture propio. No se resetean bases del usuario ni se implementa backup automático.
- **Geometría:** normalización defensiva 320 × 220 hasta 16384 por dimensión; tests de negativos/extremos y monitor de origen negativo, además de recuperación visible/bloqueada existente.

### Drag-and-drop: investigación y resultado

**BLOCKED físico (archivo y carpeta), no FAIL del producto.** La API disponible `drag` recibe una sola ventana y coordenadas relativas; cada entrada activa esa ventana. No expone origen/destino HWND separados ni down/move/up global. En el intento real inicial, activar Explorer ocultó el contenedor y el gesto terminó en Explorer sin añadir referencias. No se observó un drop entregado a WinUI que permita juzgar al manejador; no se manipularon ventanas ajenas para forzar el resultado.

También se observaron límites de automatización con menús fuera del rectángulo propietario y foco de diálogo nativo: algunas acciones por índice/coordenadas/teclado actuaban en la búsqueda del selector, no en el campo solicitado. El intento se canceló, sin agregar ni modificar archivos. No se cuenta como regresión funcional del selector ya probado en el recorrido inicial ni como PASS de su repetición final. El anclaje inferior mejoró el recorrido de los menús propios y se comprobó después.

**PASS técnico separado:** ReferenceDropProcessor clasifica metadatos de archivo/carpeta/.LNK/.EXE, rechaza duplicados/rutas relativas/URL/etiquetas vacías y conserva fuentes. Integración usa DataPackage/StorageItems reales de WinRT, incluso RequestedOperation.Move; solo persiste referencias. UI acepta una sola señal Copy, nunca Move, y completa deferral en finally. No se mueve/copia/borra el original, ni se informa Move al origen. Ningún test técnico convierte el escenario físico en PASS.

Procedimiento humano breve:

1. En la instancia aislada, crear un contenedor vacío y abrir `%TEMP%\NexSpace-Astra-20261010\fixtures` en Explorer. Anotar hashes/cantidad/ubicación de los cuatro archivos.
2. Arrastrar Nota de prueba.txt al contenedor; después arrastrar Carpeta de prueba. Esperado: se añaden dos referencias y el resumen aclara que conserva originales; los originales siguen en la misma carpeta, con contenido/hash iguales.
3. Abrir ambos desde NexSpace y quitar sus referencias; esperado: las asociaciones abren los recursos correctos y los originales siguen intactos. Repetir arrastre duplicado: omisión/error legible, sin duplicar ni mover fuentes. No probar con archivos personales.
4. Cerrar con Salir, relanzar con el mismo NEXSPACE_DATA_DIR y abrir la fila: referencias/orden/presentación persistidos. Registrar PASS/FAIL y captura real; hasta entonces, conservar BLOCKED.

### Compilaciones y suites finales

- Restore NexSpace.sln, Platform=x64: **PASS**.
- Solución Debug y Release x64, después del último ajuste de menús: **PASS, 0 errores y 0 advertencias** en ambas.
- Unitarias: **15/15 Debug y 15/15 Release, 0 omitidas**.
- Integración: **15/15 Debug y 15/15 Release, 0 omitidas**.
- Unpackaged Debug: **PASS**, abierto durante la consolidación. Unpackaged Release posterior al último cambio: **PASS, 0 errores/advertencias**, abierto y utilizado para los ciclos finales/demostración.
- Resultados privados: automated/unit-debug.trx, integration-debug.trx, unit-release.trx, integration-release.trx; debug.binlog, release.binlog y unpackaged-release.binlog.

Cobertura añadida: comandos VM y fallos de diagnóstico, payload StorageItems y semántica de referencia, mezclas/duplicados/extensiones mayúsculas, rutas ausentes/probe fallido y recuperación de lectura de repositorio, permiso denegado real, esquema rechazado y liberación, normalización de tamaño/posición, **12 contenedores × 20 referencias y 20 reaperturas** conservando preferencias/orden. Límite generoso de dos minutos es protección de regresión, no benchmark. Carga visual masiva NOT_TESTED.

### Recorrido nativo consolidado

- **PASS:** consola con recursos comunes, creación del tercer contenedor y estado vacío (01, 13–14).
- **PASS:** iconos nativos y fallback, cuadrícula de seis referencias, selección contrastada y foco nativo observado; no auditoría completa de accesibilidad (03–05, 16–18).
- **PASS:** lista, menú de acentos y visualización de azul/verde agua/cobre; acentos y geometría finales también preparados como fixture, no atribuidos todos a cambios UI nuevos (08–09, 23–24).
- **PASS:** reducción por bordes hasta ancho observado cercano al mínimo y alto mínimo 220 AppWindow, scroll/ellipsis sin perder acciones; contracción a 114 AppWindow y expansión (10–12, 25–27). Los screenshots excluyen parte del borde DWM y no se interpretan como el tamaño AppWindow.
- **PASS:** menú contextual y reordenamiento real repetido después de pasar a comandos VM (05–06, 17); adición de referencia AppsFolder mediante diálogo real y lista (20–24).
- **PASS:** apertura deliberada del documento aislado en Bloc de notas tras consolidar (18). Se cerró solo la pestaña de prueba; no se editaron otras pestañas restauradas por Windows.
- **PASS:** tres contenedores coexistiendo, uno bloqueado/contraído; recuperación desde consola con floating-emergency-restore/console sin error (28). Atajo global sin foco y recuperación fuera de pantalla conservan su evidencia real del recorrido inicial; no se repitió una semilla fuera de pantalla en esta consolidación.
- **PASS:** dos cierres y relanzamientos consecutivos del Release final, con reapertura real desde biblioteca y comparación SQLite de todas las preferencias, geometría, identidad, etiquetas/orden y siete relaciones entre tres contenedores. Se excluye únicamente updated_utc de containers; snapshots persistence-final-before/cycle1/reopened1/cycle2 y reopened2 sustentan la comparación. Las ventanas se abren por filas, no automáticamente.
- **PASS:** cierres Debug PID 11364 y 8592 con código 0; Release PID 24268 y 23248 con código 0, recuento NexSpace residual 0 después de ambos cierres Release. Tras comprobarlo se relanzó intencionalmente la demostración; su proceso abierto no es un residuo.
- **PASS limitado:** cuatro SHA-256 originales iguales (source-hashes-final.json); mismo Explorer PID 2236 desde la reanudación, sin reinicio causado por estas pruebas. No es certificación de estabilidad prolongada.
- **BLOCKED:** drop físico Explorer→NexSpace y repetición final del selector por límites de la herramienta, según detalle anterior. Los PASS de selectores iniciales se conservan como históricos.
- **NOT_TESTED:** cambio real DPI/resolución/monitor, alto contraste, efectos avanzados activados, lector de pantalla/navegación completa, denegación visual, proveedores cloud/remotos, pérdida de energía/corrupción de base y carga visual masiva.

No se repitió eliminación por UI en esta consolidación: sigue la evidencia inicial de confirmar/cancelar y pruebas técnicas de borrado de metadatos; la política de computer-use exige confirmación nueva para borrar datos desde una app. Se omitió esa acción y se continuó con el resto, sin atribuirle un PASS nuevo.

### Medición orientativa y pendientes

En Release, consola + tres paneles pequeños: WorkingSet observado ~203 MiB (212.8 MB decimales), handles ~2314; dos muestras sin interacción durante ~54.7 s aumentaron CPU acumulada 1.72 s. Son muestras locales de proceso, no memoria privada ni benchmark completo, y no certifican CPU cero/ausencia de fugas. Tras recuperación hubo ~212 MiB. **Objetivo orientativo RNF-02 <150 MB no alcanzado en esta muestra**: hace falta perfilado y presupuesto por módulo antes de ampliar la carga. No se modificó configuración global para bajar cifras.

Para aceptación aún se necesita: gesto DnD humano, revisión estética del concepto C con/sin transparencia cuando el usuario ya tenga efectos habilitados, accesibilidad/monitores y revisión de memoria. Superficie sólida e iconos/tipografía/espaciado mejorados son funcionales; no se afirma fidelidad absoluta ni DESKTOP estable. MVVM de biblioteca, sondeo asíncrono para proveedores lentos y recuperación/backup de corrupción son deuda explícita.

### Reabrir la demostración persistente

Desde la raíz del repositorio, cerrar antes la instancia mediante Salir:

```powershell
$env:NEXSPACE_DATA_DIR = Join-Path $env:TEMP 'NexSpace-Astra-20261010/data'
& "$env:USERPROFILE/.dotnet/dotnet.exe" build src/NexSpace.UI/NexSpace.UI.csproj -c Release -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
if ($LASTEXITCODE -eq 0) {
    Start-Process './src/NexSpace.UI/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NexSpace.UI.exe'
}
```

Abrir las tres filas: Astra Trabajo Validado (cuadrícula azul, seis referencias), Astra Lectura (verde agua, bloqueado/contraído) y Herramientas · Demo (lista cobre, AppsFolder). Usar +, vista y chevron; Recuperar paneles o Ctrl+Alt+Shift+F12 permiten traer ventanas propias al área visible. No habilitar DESKTOP. Se deja esta demostración abierta para inspección, sin publicar datos.

## Git y conclusión final

Implementación y consolidación guardadas en commits locales descriptivos en main, incluyendo la implementación previa sin commit; documentación en commit separado. Sin push, force ni reescritura. Capturas, bases, TRX/binlogs y datos personales quedan excluidos; revisar los hashes de commit entregados en el informe y `git log -2 --oneline`. ADR-006/007 y decisiones de producto no se alteran.

**Base funcional FLOATING consolidada, aceptación integral condicionada.** Se completó lo técnicamente verificable, con límites explícitos. Recursos NexGlass y Presentation pueden reutilizarse en Fase 2, pero DnD físico y aceptación estética/entorno siguen pendientes. DESKTOP permanece experimental/deshabilitado. **No se inicia Fase 2; requiere autorización nueva tras revisar pendientes.**
