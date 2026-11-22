# Validación de la candidata Concepto C + Fase 4.1

Fecha local 2026-11-15. Rama `integration/concept-c-phase41`. Código `6ce2ac3` con padres `2627a6a` y `d2def4f`.

## Compilación y regresión

- **PASS** Restore, Debug y Release x64. Ambas configuraciones se compilaron con cero errores y cero advertencias.
- **PASS** 81 unitarias y 35 de integración por configuración. La nueva prueba recorre dos perfiles SQLite independientes, con Automático y Contraste, y comprueba que se recupera el vínculo al contenedor correcto.
- **PASS** Publicación portable autocontenida x64 con XBF y PRI. Windows 11 build 26300, .NET SDK 10.0.401, runtime 10.0.12 y Windows App SDK 2.5.1.
- **FAIL inicial, corregido**. `AccessibilitySettings.HighContrastChanged` devolvió `0x80070490` al registrar la notificación en unpackaged. WinUI terminó con `0xC000027B`. La candidata registra el fallo opcional y sigue leyendo la preferencia actual al aplicar el tema. El código de salida histórico de Fase 2 sigue sin causa demostrada.

## Procedimiento de recursos

Se usa `tools/Invoke-NativeResourceProbe.ps1` con `-HandleTypes`, binario portable y un directorio temporal de datos nuevo por ejecución. El escenario de 300 ciclos conserva 60 segundos de reposo. El contador de tipos es un censo acotado del proceso propio. Los límites internos y externos de la Fase 4.1 siguen activos. El runner usa `NEXSPACE_PROBE_FORCE_GC=0`.

La línea base sale de los JSON archivados de Fase 4.1, descritos en [PHASE41_PERFORMANCE.md](PHASE41_PERFORMANCE.md). Las medidas de memoria y handles cambian con el calentamiento, la actividad del entorno y las versiones del sistema. La comparación principal usa la diferencia entre la muestra inicial y la muestra después del reposo.

La transparencia del sistema estaba desactivada. La serie mide el camino sólido. La evidencia del material acrílico activo pertenece al prototipo previo y no certifica el consumo de la candidata integrada con Acrylic activado.


## Resultados de recursos

**PASS de ejecución** en las cinco series. Cada prueba completó los ciclos solicitados y salió con código 0. Los límites de seguridad no se activaron. La evidencia se conserva bajo `artifacts/concept-c-phase41/final-validation/`, en el worktree de integración.

Cada celda muestra Fase 4.1 / candidata. GDI y USER indican inicio y fin. Las otras columnas muestran el cambio entre la primera muestra y el final del reposo.

| Escenario | GDI | USER | Timer | WaitCompletionPacket | Composition | Event | Handles | Privada (MiB) | WS (MiB) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| empty-100 | 43→43 / 51→51 | 38→38 / 38→38 | +0 / +0 | +0 / +0 | +99 / +99 | +116 / +120 | +218 / +221 | +4.2 / +3.8 | +10.3 / +10.0 |
| container-100 | 49→49 / 49→49 | 33→33 / 32→32 | +0 / +0 | +0 / +0 | +99 / +99 | +113 / +116 | +213 / +218 | +22.7 / +21.7 | +32.3 / +30.5 |
| dock-100 | 53→41 / 49→49 | 32→32 / 32→32 | +0 / +0 | +0 / +0 | +99 / +99 | +117 / +117 | +209 / +218 | +25.6 / +16.1 | +34.1 / +25.3 |
| profiles-100 | 53→41 / 97→93 | 42→42 / 42→42 | +0 / +0 | +0 / +0 | +199 / +199 | +225 / +230 | +421 / +432 | +35.7 / +24.7 | +43.5 / +35.1 |
| container-300 | 53→41 / 49→49 | 33→34 / 32→34 | +0 / +0 | +0 / +0 | +299 / +299 | +318 / +322 | +609 / +628 | +30.6 / +31.7 | +39.7 / +41.6 |

- **GDI y temporizadores.** Los 300 contenedores mantuvieron GDI en 49, Timer en 4 y WaitCompletionPacket en 56. El escenario de perfiles mantiene un nivel absoluto superior en dos repeticiones, con 97 al comenzar, 93 al final y un intervalo de 77 a 97 al considerar ambas ejecuciones. USER termina en 42 en la serie final, igual que en 4.1.
- **Retención residual.** En 300 ciclos, Composition sube de 9 a 308 y Event de 1328 a 1650. Los handles pasan por 2170, 2379 y 2576 en los ciclos 100, 200 y 300 y quedan en 2581 tras el reposo. El incremento de 628 se compara con 609 en 4.1. La memoria privada sube de 96,0 a 127,7 MiB y el WS de 165,0 a 206,5 MiB. En bloques de 100 los contadores siguen creciendo. La evidencia de Fase 4.1 reproduce el residual fuera de NexSpace y la serie integrada conserva una pendiente comparable.

La variante integrada `system-chrome` completó 30 cambios de perfil y 30 s de reposo. GDI pasó de 53 a 49. En la configuración flotante final ya había 91 GDI en la muestra `cycle-30` del ensayo largo y 93 después del reposo. La variante sustituye el marco del contenedor y omite el tema DWM de las ventanas flotantes. Esta comparación acota el coste adicional al conjunto que cambia la variante. No aísla una llamada ni un objeto nativo concreto.

## Inspección de la candidata en Windows

Binario `artifacts/concept-c-phase41/candidate-final/NexSpace.UI.exe`. Demostración en una biblioteca SQLite separada, con tres categorías y 14 referencias en Concepto C, y un contenedor en Lectura. La carpeta `artifacts/concept-c-phase41/captures/` contiene capturas auténticas tomadas en un monitor 1920×1080. El popover se capturó con NexManager de demostración por detrás.

- **PASS · Contenedores.** Archivos pasa de 406×303 px capturados a 406×53 px y vuelve al tamaño inicial. Se ven iconos de carpetas y aplicaciones reales en Archivos, Juegos y Aplicaciones. La segunda fila de Archivos queda parcialmente recortada al inicio con esa altura. Se comprobó que el desplazamiento permite ver todas las etiquetas.
- **PASS · Dock y popover.** El dock horizontal ocupa 530 px desde X=695, con centro en X=960. Se verificaron el indicador activo, la flecha bajo el popover de Archivos, las seis referencias y la restauración del foco tras Escape. La apertura de `Nota de demostración.txt` desde el popover mostró «Recurso abierto» y abrió el documento aislado en Bloc de notas. El SHA-256 del fixture coincide antes y después.
- **PASS · NexManager y perfiles.** Se abrió desde el dock. El cambio Concepto C → Lectura → Concepto C restauró la biblioteca y el tema de cada espacio. Se vieron tema claro en Lectura y Automático oscuro en Concepto C. Se cerró por «Salir» y, al reabrir, reaparecieron los tres contenedores y el dock. SQLite `integrity_check = ok` y ambos perfiles disponibles.
- **PASS · Recuperación.** Ctrl+Alt+Shift+F12 desde una ventana de contenedor. El log registró `workspace-recovered failures=0` y `floating-emergency-restore global-hotkey`. Alcance limitado a la disposición visible y un monitor.
- **PASS · Automático con las preferencias actuales.** `UISettings` devolvió fondo `#000000`, acento `#A1A1A1`, efectos avanzados desactivados y animación permitida. Se observó el tema oscuro y el acento de reserva azul `#7FB2EA` por la baja saturación del acento. Contraste calculado del acento de reserva sobre la superficie `#16181C` de 8,01:1. NexManager desactiva la paleta manual en Automático. Las pruebas de paleta cubren RGB cromáticos y extremos. La actualización en vivo tras cambiar las preferencias de Windows queda sin probar en esta candidata.
- **NOT_TESTED.** Acrylic activo, altos contrastes del sistema durante la ejecución, Narrador, DPI/multimonitor, movimiento reducido, arrastre físico desde Explorer, tirador de redimensionamiento en hardware y uso prolongado de varias horas. Un arrastre breve sobre el tirador en la sesión automatizada no produjo un cambio de tamaño visible. La comprobación del atajo y de Escape cubre esos flujos de teclado, con el resto de accesibilidad pendiente.

## Diferencias frente al Concepto C

Se conserva la composición de grupos flotantes, barra inferior centrada y popover ligado a la categoría. Los estados expandidos muestran iconos nativos. El contenedor tiene controles discretos dentro del encabezado. NexManager queda como superficie secundaria para administrar la biblioteca.

- La muestra integrada usa superficies sólidas más opacas que la [referencia aprobada](../../assets/references/concepts/03_C_sistema_hibrido_APROBADO.png). El sistema tenía efectos avanzados desactivados. La revisión conservó la configuración de Windows.
- El dock y los contenedores muestran un filo superior claro al tomar foco. La diferencia se aprecia en las capturas del popover abierto y de Archivos contraído. Sigue pendiente el ajuste visual del marco, dentro de los límites de recursos y sin reintroducir colores del caption.
- En la altura demostrada de Archivos se necesita un desplazamiento corto para leer la segunda fila completa. El popover de seis referencias resulta más alto que el del concepto. El contraste automatizado de pares de recursos tampoco acredita toda la interfaz. El texto claro del botón «Nuevo contenedor» sobre el azul de acento es un caso observado pendiente de corregir. El par de recursos `#F2F4F7` y `#7FB2EA` da aproximadamente 2,01:1 y requiere respetar el color semántico sobre acento.

## Decisión

La rama queda como candidata local para revisión. Antes de incorporar a `main`, validar el coste del marco con material Acrylic activo, contrastar los controles en alto contraste real y acordar la mitigación o el criterio de aceptación del crecimiento residual de Composition y Event. Mantener DESKTOP deshabilitado y 1.0 bloqueado. Se conservan los dos worktrees de origen. No se publica el binario ni se suben commits.

## Consolidación de la candidata 51366f1

| Validación | Resultado | Evidencia |
| --- | --- | --- |
| Cuadrícula | PASS | A 406 × 303 px se leen seis referencias en dos filas. A 307 px de ancho pasan a dos columnas con desplazamiento. El nombre largo admite dos líneas. |
| Botón primario | PASS en los cinco temas de la aplicación | En el azul automático de reserva el texto pasa de 2,01 a 9,46:1. Con puntero y pulsado, los pares semánticos dan 10,35 y 11,22:1. Sobre azul claro de tema Light, texto blanco con 5,49, 6,46 y 7,54:1. Se inspeccionaron cinco estados normales, foco en Automático y puntero en Light. El estado pulsado se validó matemáticamente. |
| Filo al recibir foco | FAIL visual | El experimento sin redimensionamiento del presentador extendió el borde claro al contorno. Se descartó. Se conserva el marco integrado y sigue visible la banda superior activa. |
| Regresión | PASS en la demostración aislada | Creación, apertura explícita de un TXT en Bloc de notas con hash conservado, contracción, expansión, movimiento y tamaño con el menú del sistema, cambio de perfiles, recuperación global, cierre y reapertura con cuatro contenedores. SQLite integrity_check devolvió ok. |
| Arrastre del encabezado y tirador | NOT_TESTED en hardware | El gesto simulado no produjo un cambio medible. El menú del sistema sí cambió la geometría persistida. |
| Debug y Release x64 | PASS | Restore y compilación completados. En cada configuración pasaron 92 pruebas unitarias y 35 de integración. |

La evidencia privada se encuentra en `artifacts/concept-c-phase41/consolidation/`. Las parejas de capturas `before-archivos-focused` y `after-archivos-focused`, y `before-primary-auto` y `after-primary-auto`, permiten comparar el mismo escenario. El perfil de demostración reabierto contiene 44 referencias, incluidas 30 de prueba en Validación local.

### Recursos de la consolidación con efectos desactivados

| Escenario | GDI | USER | Handles | Privada MiB | Composition | Event | Timer | WaitCompletionPacket |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 100 cambios de perfil | 95→85 | 90→78 | 2092→2532 | 97,8→125,9 | 17→216 | 1365→1597 | 4→5 | 60→60 |
| 300 ciclos de contenedor | 51→41 | 69→37 | 2033→2619 | 97,1→128,7 | 9→308 | 1362→1661 | 4→4 | 58→60 |

En 300 ciclos, el incremento de handles fue +609 en Fase 4.1, +628 en la integración inicial y +586 en esta candidata. Composition creció +299 en las tres series. Event creció +318, +322 y +299 respectivamente. En 100 cambios de perfil los handles crecieron +421, +432 y +440. Ambas series finales terminaron con código 0. Sigue pendiente una decisión de aceptación del crecimiento residual.

### Acrylic activo en Windows 11

La preferencia original era 0. Con transparencia activada desde Configuración, el proceso detectó el cambio y registró Thin Acrylic con efectos avanzados disponibles. Se inspeccionaron Archivos enfocado y sin foco, la barra y el popover abierto. El popover conserva una tarjeta oscura. En la candidata previa, cambiar desde Concepto C a Lectura terminó el proceso en Microsoft.UI.Xaml.dll. El registro de la aplicación conserva 0x80070057 en OnDefaultSystemBackdropConfigurationChanged. La prueba de 100 cambios sintéticos había terminado con éxito en ese mismo binario.

En 30150f1 el controlador usa exclusivamente la configuración de NexSpace. Se comprobó la secuencia Lectura → Concepto C → Lectura → Concepto C en el mismo proceso. Se capturaron el tema claro y el retorno a Automático. El contraste puntual de píxeles del encabezado de ambas capturas fue 12,34:1 y 5,72:1 respectivamente, usando la fila 44, tinta extrema en X=48–89 y fondo en X=160. Son muestras de JPEG del resultado compuesto. El contraste en otras zonas y con otros fondos depende de su composición. Queda pendiente una comparación del mismo contenedor sobre dos fondos certificados en idéntica posición.

Debug y Release x64 de la corrección compilaron con cero errores y advertencias. En cada configuración pasaron 92 pruebas unitarias y 35 de integración. Evidencia de la transición original en `profile-switch-51366f1.jsonl`, pruebas en `tests/acrylic-*` y capturas en `captures/fixed-*`.

Con 100 cambios de perfil, Acrylic antes de la corrección registró +683 handles, +405 Composition y +237 Event. La repetición con la corrección alcanzó 2041→2708 handles (+667), 17→422 Composition (+405), 1333→1573 Event (+240), GDI 97→97, USER 45→63 y privada 101,7→125,3 MiB. Timer quedó en 4 y WaitCompletionPacket en 60. El proceso de prueba terminó normalmente tras 30 s de reposo. En la serie con efectos desactivados Composition creció +199. El cierre al alternar perfiles quedó corregido en la reproducción manual, pero persiste un coste de recursos superior con el material activo. Recomendación de aceptación limitada a revisión local.

Se restauró «Efectos de transparencia» a Desactivado y se verificó EnableTransparency=0. La candidata corregida quedó abierta sobre la biblioteca aislada.
