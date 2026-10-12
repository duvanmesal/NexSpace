# Fase 4.1 · Investigación de recursos nativos

Fecha: 2026-10-12. Investigación local; **Fase 4 abierta, lanzamiento 1.0 bloqueado**. No se tocó el worktree de diseño ni se integraron sus commits.

## Entorno y método

Windows 11 Pro x64 **10.0.26300.0**, comprobado mediante CIM/Environment y coincidente con los informes WER. La etiqueta anterior «26100» confundía el target de compilación con el sistema probado. Target `net10.0-windows10.0.26100.0`, SDK .NET 10.0.401/runtime 10.0.12, Windows App SDK 2.5.1, paquete WinUI resuelto 2.3.9, Microsoft.UI.Xaml.dll 3.2.3.0 y Microsoft.UI.Windowing.Core.dll 10.0.27200.1054. No se actualizó el SDK ni se cambiaron ajustes globales.

Datos propios en directorios temporales nuevos, configuración y SQLite persistentes entre los reinicios indicados. Censos de tipos mediante NtQueryObject de solo lectura del proceso actual, acotados/no atómicos; HandleCount sigue siendo el total de referencia. GDI/USER mediante GetGuiResources. Sin GC forzado en las comparaciones descritas. Cerrar un HWND no demuestra liberar todos los objetos de Composition.

Harness interno: máximo 5500 handles, 512 MiB privados, 768 MiB WS, 4000 GDI, 2000 USER y cuatro minutos; interrupción controlada y cierre normal. `tools/Invoke-NativeResourceProbe.ps1` añade supervisión externa (6000 handles/576 MiB privados/832 MiB WS/240 s), capaz de terminar únicamente el proceso de prueba recién creado si el dispatcher no responde. No se aplican estos límites ni terminaciones al uso normal del producto.

Las ejecuciones finales son secuenciales entre sí. Algunas compilaciones y el segundo reproductor independiente coincidieron con parte de las pruebas: no interpretar CPU/tiempos como benchmark de máquina ociosa. El primer ensayo completo con volcado tiene WS perturbado; no mezclarlo con medidas pasivas.

Evidencia privada: `docs/testing/evidence/private/phase41-20261012/` (ignorada por Git), experimentos, informes WER históricos, volcado gestionado, raíces SOS, TRX y capturas auténticas. Los ensayos tempranos que usaban el directorio raíz como NEXSPACE_DATA_DIR compartían `%TEMP%/logs`; atribuir esos eventos por PID/fechas. El runner posterior usa siempre un subdirectorio `data` y logs aislados.

## Causa demostrada A: ruta de caption nativo/GDI

Antes: 20 ContainerWindow con personalización anterior, handles 1937→3318, GDI 49→1261 y privados 102989824→144338944 bytes. Quitar únicamente tiles/lista, tooltips, eventos, bindings, contenido o tema no eliminó el crecimiento. Quitar simultáneamente la extensión de título y las escrituras de colores lo redujo a GDI 51→49 y handles 1934→2023. Una ventana vacía a la que solo se escriben colores de caption también creció de GDI 53→933 en 20 ciclos.

Corrección: ContainerWindow usa título/movimiento/redimensionamiento/cierre del sistema y oculta la fila XAML de arrastre que ya no se utiliza. NexThemeManager conserva el tema del contenido pero no escribe colores del caption. La ruta antigua queda exclusivamente bajo opt-in diagnóstico aislado `legacy-titlebar`.

Reintroducirla con límite seguro detuvo el ensayo alrededor de 48 ciclos: handles 1997→5521, GDI 49→3114, completed=false/BLOCKED_SAFETY_LIMIT, salida normal 0. No se provoca deliberadamente otro crash. El resultado demuestra el desencadenante y la eficacia de evitar esa ruta; **no** identifica una asignación/font nativa concreta. [Reporte de Microsoft Windows App SDK relacionado](https://github.com/microsoft/WindowsAppSDK/discussions/6697): corroboración externa, no nuestra pila de ejecución.

## Causa demostrada B: temporizadores por ventana

Tras corregir el caption pero todavía con DispatcherQueueTimer, 100 ciclos dieron Timer 5→106 y WaitCompletionPacket 59→159. Sin timer, Timer 5→6 y WaitCompletionPacket 59→59, manteniendo el crecimiento Composition 9→108. Se demuestra una contribución distinta de los temporizadores, no una solución de toda la memoria.

Corrección: `DeferredAction`, debounce cancelable basado en Task.Delay, cancelación y generación, envío de la escritura al dispatcher y descarte de callbacks encolados después de Stop/Dispose. ContainerWindow y DockWindow lo destruyen al cerrar. Tests: coalescencia de 100 reinicios, cancelación, callback ya encolado, Dispose idempotente, errores de callback/dispatch y prohibición de reinicio después de Dispose.

## Limpieza de XAML/eventos: mejora comprobada, causalidad limitada

Antes de la limpieza completa, 100 contenedores: privados 111386624→180101120 con limpieza en Closing; handles +217. El volcado de otra repetición mostraba 100 ContainerWindow y 101 DeferredAction, pero SOS encontró **cero raíces** para tres contenedores cerrados examinados. Objetos presentes no equivalen a objetos vivos retenidos; el volcado no prueba una raíz del coordinador ni explica la retención nativa.

La variante `detach-all` retira su propio Closed, handlers de botones/lista, DataContext y Children al recibir Closed: privados 112848896→132763648, handles +214. Retirar solo Closed dio 116301824→148905984, handles +214; no aislar todo el beneficio en ese único handler. La limpieza completa se incorpora como conjunto, sin atribuir la mejora a una raíz individual. La repetición de producción dio 116117504→139878400 (+23760896 bytes), managed 3051040→3539120, GDI 49 estable, USER 33 estable. El censo Composition/Event no mejoró con esa limpieza.

DockWindow también usa Closed nombrado y se desuscribe, cancela debounce, retira AppWindow/Loaded/XamlRoot, cierra flyouts y libera Content/Backdrop. El coordinador retira ventanas de su colección. ShellIconLoader mantiene caché acotada a 128, dispone streams y ReferenceTile cancela al Unloaded. No se demostró que iconos, hooks de recuperación o bandeja causen el síntoma mínimo: aparece sin ninguno de ellos. Los ensayos de contenedor vacío no ejercitan el cargador de iconos; no certifican su comportamiento prolongado bajo miles de recursos.

## Residual independiente de NexSpace

Se conserva el comparador interno de ventanas vacías y se añade `tools/WinUiWindowLifetimeRepro`, sin referencias a proyectos NexSpace. No tiene servicios, perfiles, XAML de producto, bindings, eventos en hijas, ShellIconLoader, backdrop, títulos personalizados ni DispatcherQueueTimer.

- 100 Window + Grid: handles 1782→1993; Composition 5→105; Event 1269→1376; privados 95326208→94674944; WS 153812992→156168192. GDI 9 y USER 29 estables después de 30 s de reposo.
- 100 Window sin Content: handles 1780→1995; mismos incrementos Composition/Event; privados 65515520→65544192; WS 118439936→122081280; GDI 9 y USER 29 estables.
- Ambas ejecuciones: todos los HWND hijos destruidos, completed=true, salida 0, sin GC forzado. El síntoma de handles se reproduce fuera del producto, incluso sin árbol visual hijo; no atribuirlo a una referencia gestionada específica de NexSpace.

Limitación demostrada del conjunto runtime/OS probado, **sin identificar todavía el propietario/stack nativo exacto**. No cerrar handles de Composition/Event ni Dispose del compositor de XAML: no existe evidencia de que esa propiedad pertenezca a nuestra aplicación. Tampoco sustituir la solución por GC forzado. [Incidencia histórica WinUI de ventanas múltiples](https://github.com/microsoft/microsoft-ui-xaml/issues/9063) usa otra versión: no demuestra identidad causal.

## Fallo nativo histórico

Se conservan seis Report.wer del 0xE0464645: Microsoft.UI.Windowing.Core.dll 10.0.27200.1054, offset 0x9955, con OutOfMemoryException previa al abrir dock y último ciclo muestreado 70/12916 handles. Sin dump nativo de aquel fallo, no se puede afirmar una función/pila exacta ni que el offset corresponda al caption. Las mejoras evitan su reproducción en los ensayos finales, pero **ausencia de crash no equivale a fuga eliminada**.

El 0xC000027B histórico de Fase 2 sigue sin causa demostrada. El fallo distinto por publicación sin PRI/XBF sí se corrigió previamente. No confundir códigos coincidentes con una causa compartida.

## Repetición final y tendencia

Todos los siguientes ensayos terminaron con código 0, completed=true, sin límite de seguridad ni GC forzado, y verificaron destrucción de HWND propios. PASS se refiere a ejecución/cierre; **FAIL de estabilización de handles** permanece.

- 100 ContainerWindow: handles 1972→2185, privados 116117504→139878400, WS 188514304→222351360; GDI 49/USER 33 estables. Composition 9→108, Event 1326→1439, Timer 4 y WaitCompletionPacket 56 estables. Runner `313f60c8c22447cdbad0e2bfa46efb48`.
- 100 DockWindow: handles 1971→2180, privados 115933184→142798848, WS 187981824→223756288; final GDI 41/USER 32. Runner `1fc92f3fd1814615aaf724d85bfe8bf8`.
- 100 cambios completos de perfil (fixture nueva, un contenedor+dock): handles 1994→2415, privados 116260864→153665536, WS 188968960→234602496; GDI 53→41/USER 42 estable; Composition 17→216, Event 1331→1556, Timer 4/WaitCompletionPacket 58 estables. Runner `56c369da38d84e6da396f09c3fbb8d25`. El ensayo anterior con dos contenedores+dock completó 100 pero corresponde a la versión antes de limpieza completa; no intercambiar esas métricas.
- 300 ContainerWindow + 60 s de reposo: handles 1976→2585, privados 116396032→148463616, WS 188497920→230092800. GDI 53→41; USER 33→34. Composition 9→308 y Event 1326→1644; Timer 4 y WaitCompletionPacket 56 sin incremento. Runner `d8c6c6af2eb540f0b9486b8742c3804e`.
- Tendencia del ensayo de 300: privados al ciclo 100/200/300 = 138842112/144314368/148586496; handles 2179/2384/2582. Después del calentamiento, la pendiente de memoria disminuye, pero todavía aumenta ~4–5 MiB por bloque de 100 y los handles ~200 por bloque. No hay meseta demostrada. Reposar 60 s solo reduce privados 122880 bytes; no libera Composition.
- Antes de debounce/limpieza completa, el ensayo de 300 con caption ya corregido terminaba con privados 292380672 y handles 3190 tras 60 s. Es comparación orientativa entre revisiones, no benchmark idéntico: también cambia el cleanup. La repetición final baja privados ~49%, pero conserva la pendiente nativa residual.

Los comparadores vacíos internos completaron 100 con y sin Content (handles +218/+216), GDI/USER estables. Se conservan junto con los dos reproductores independientes de 100 descritos arriba.

## Compilación y regresión funcional

PASS: restore con Platform=x64/Configuration coherentes, builds Debug/Release x64, 0 errores/advertencias; **43 unitarias y 33 integración por configuración, 152 ejecuciones PASS**, cuatro TRX en `final-tests/`. El primer comando restore sin Platform seguido de build x64 --no-restore produjo NETSDK1047; repetir restore con las mismas propiedades lo resolvió, sin cambio de código.

PASS: publicación Release autocontenida/portable con PRI/XBF y Debug framework-dependent unpackaged. Tres arranques sobre la misma SQLite aislada: framework-dependent PID 19156, portable PID 14192 y 25240; dos cambios de perfil por arranque, verificación del número de ventanas restauradas, cierre normal, salida 0. `final-reopen.json`. El framework-dependent requiere DOTNET_ROOT/DOTNET_ROOT_X64 del runtime local **solo en el entorno hijo**, no instalación global ni Developer Mode.

PASS de interacción real en candidata final: creación «Regresión 4.1», estado vacío y cierre de su ventana sin borrar metadatos; movimiento Estudio (80,140)→(130,170), redimensionamiento ancho 420→385; contracción 333→107 píxeles capturados y expansión; bloqueo impide arrastre (origen capturado 137,170 estable), desbloqueo confirmado en SQLite. El recorte visual excluye bordes invisibles de Windows, por eso ancho capturado 371 difiere del ancho AppWindow 385.

PASS: Ctrl+Alt+Shift+F12 desde un contenedor, registro `workspace-recovered failures=0` y `floating-emergency-restore global-hotkey` a 19:38:46Z. No prueba multimonitor ni un panel realmente fuera de pantalla. PASS: aplicación de tema Dark desde Light, cambio físico Universidad→Programación→Universidad, restauración de contenedores/dock/tema por perfil y SQLite integrity_check=ok. No se modificaron opciones de Windows para probar translucidez/animaciones.

Capturas reales finales: `final-container.jpg`, `final-collapsed.jpg`, `final-resized.jpg`, `final-created.jpg`, `final-manager.jpg`, `final-dark-manager.jpg`, `final-profile-programacion.jpg`, `final-profile-universidad.jpg`. Las capturas iniciales anteriores a cleanup se conservan como antecedentes; `container-recovered.jpg` estaba recortada por foreground y no acredita la distribución del contenedor. Algunos glifos de fallback se muestran como cuadros: pendiente visual preexistente, no resuelto por esta intervención.

NOT_TESTED: soak de horas, arranque en máquina limpia, actualización de SDK/Windows para comprobar el residual, pila de asignación nativa, todos los temas con Narrador/alto contraste, DPI/multimonitor, iconos bajo miles de referencias y drag-and-drop físico. La prueba prolongada acotada fue 300 ciclos y 60 s de reposo, no horas de uso.

Salir desde Manager terminó PID 24460 con código 0. Antes de reabrir no había procesos NexSpace.UI residuales. Demostración final PID 16852, `artifacts/phase41/cleanup-final`, misma biblioteca aislada: Estudio (130,170,385,340), Recursos contraído, tercer contenedor conservado como metadatos y tema Dark; integrity_check=ok. SHA256 de ambos originales fixture coincide con Fase 3 (`final-fixture-hashes.json`).

Reposo pasivo final 60,09 s, sin profiler ni entrada: handles 2194→2198, privados 125779968→125718528, WS 210370560→210739200, CPU acumulada 1,625→1,671875 s (~0,078% de un núcleo). `final-demo-idle.json` contiene 13 muestras. Estabilidad de un minuto sin churn no elimina el fallo de recreación ni prueba consumo permanente.

Plan separado de integración visual: `docs/phase-4/PLAN_INTEGRACION_DISENO.md`. Solo lectura de commits 9227b09/790d24a/cb6beb4/1446c1a/9d627a5; ningún merge/cherry-pick ni cambio al worktree de diseño. `.claude/` apareció sin seguimiento y se conserva sin incluirlo en los commits de estabilización.

Reapertura final comprobada también por captura: Estudio conserva origen capturado (137,170), ancho 371 y alto 333, equivalentes a AppWindow (130,170,385,340); mantiene tema Dark. `final-reopened-container.jpg` y `final-dock-0.jpg`. El menú Archivos del dock expone por UIA las tres referencias esperadas y se cierra con Escape; su captura transitoria incluyó fondo ajeno, por lo que **no se guarda ni se usa como PASS de layout del popover**.

Herramientas nativas: WinDbg/cdb no disponibles en PATH ni cdb en la ruta estándar de Windows Kits; no se instalaron ni configuraron WER/registro. Se preservan Report.wer y el volcado gestionado, sin pretender reemplazar una pila nativa ausente. Hay 102 JSON de experimentos/progreso/runner copiados a evidencia privada; `final-session-events.jsonl` conserva los eventos de esta sesión.

## Git y validación manual pendiente

Commit técnico local de esta revisión: `a47bbdd`; complementa ce096d5/95c87b8/2d63241. Auditoría de archivos preparados: solo código, tests y herramientas fuente; sin imágenes, dump, SQLite, binarios, TRX, certificados ni patrones habituales de secretos. Documentación en commit local separado. No push, release, mezcla de ramas ni cambios al worktree de Claude.

Para aceptación humana: usar solamente la demostración aislada abierta; comprobar glifos, popovers y los cuatro temas, teclado/Narrador, arrastre OLE físico desde Explorer con un fixture y hashes antes/después. DPI/multimonitor requiere escenario externo reversible, aún no probado. No repetir churn ilimitado ni confundir esta guía con corrección del runtime. Mantener el reproductor mínimo como regresión para cualquier futuro cambio de SDK/OS; no habilitar DESKTOP ni inicio con Windows.

## Política de mitigación y aceptación

Mantener caption del sistema, debounce gestionado, limpieza explícita y diagnóstico acotado. No ejecutar recreaciones automáticas ilimitadas. Si una sesión de pruebas acumula recursos, guardar/cerrar normalmente y reabrir: terminar el proceso libera su tabla de handles, pero esto es mitigación operativa, no corrección del runtime. No reiniciar automáticamente ni matar una sesión del usuario.

Una futura reutilización acotada de ventanas podría reducir churn, pero necesita decisión explícita, pruebas de estado/foco/recursos y no debe ocultar la fuga. Otra vía es reproducir el proyecto mínimo en otra build estable de Windows y versión de SDK, y presentar un issue con el reproductor sin adjuntar dumps privados. No se publica automáticamente ningún informe.

**Recomendación: mantener Fase 4 abierta y bloquear 1.0** mientras los handles no se estabilicen en recreación sostenida. Mantener también pendientes de drag-and-drop físico, accesibilidad, DPI/multimonitor, translucidez, animaciones, memoria prolongada y distribución en máquina limpia.
