# Fase 3 · Perfilado inicial

2026-10-11 · Release unpackaged Windows 11 x64; 12 procesadores lógicos. Medidas de Get-Process, bytes convertidos a MiB (2^20). Evidencia privada: phase3-astra-20261011/performance*.json. No ETW ni heap dump; no atribución causal de memoria por módulo.

## Muestras exploratorias

- PID 6608, un contenedor: Working Set 197,18 MiB, privados 117,11 MiB, 2135 handles.
- PID 26944, dos contenedores/tres categorías: Working Set 210,75 MiB, privados 126,46 MiB, 2361 handles.
- PID 16388, tras cambios de perfil/tema/orientación: Working Set 230,62 MiB, privados 140,86 MiB, 2397 handles.

Son procesos/builds y tiempos diferentes: **no restar estas cifras para afirmar coste por contenedor/categoría**. El Working Set supera el objetivo orientativo de 150 MB; no se oculta ni se sacrifica estabilidad para alcanzarlo.

## Reposo controlado

PID 14928, Manager + dock vertical + dos contenedores/tres categorías. Intervalo 20,003 s sin interacción; CPU acumulada capturada como escalar antes y después: delta 0,234375 s. Equivale aproximadamente a 1,17% de un núcleo o 0,098% de la capacidad lógica total (12), solo durante esa muestra. Working Set final 200,63 MiB; privados 117,07 MiB; 2222 handles.

La muestra anterior performance-idle.json no es la referencia: retenía el objeto Process en lugar del contador escalar y podía producir delta 0 por actualización diferida. Se repitió y se conserva el archivo como historial, no como evidencia de CPU nula.

## Costes y optimizaciones

La aplicación tiene runtime WinUI/Windows App SDK, compositor/materiales, árboles XAML por HWND, SQLite y caché de iconos Shell. Son candidatos por arquitectura, **no consumidores principales medidos con heap**. NexThemeManager usa referencias débiles y retira destinos al cerrar; ShellIconLoader vacía la caché al salir; tray/subclass y temporizadores se disponen. La lectura inicial SQLite se prepara fuera del hilo UI. Se evitó consultar HWND destruido y se añadió flush de geometría.

Instrumentación marca startup-begin y manager-settings-loaded; ese intervalo no incluye todo el tiempo hasta primer frame interactivo. El fallo nativo histórico de Fase 2 no tiene causa demostrada. El fallo COM de ResourceDictionary de esta fase sí se aisló y corrigió, sin equipararlos.

## Pendientes

Series repetidas/estabilizadas por contenedor/categoría, carga grande, latencia de primer frame, trazas ETW/heap/GPU y soak prolongado. Ninguna de estas muestras acredita ausencia de fugas.

## Serie incremental real, mismo proceso

PID 27904, build Release final anterior al ajuste exclusivo del color de caption. Muestras performance-series-0 a 4, pausa de 3 s tras cierre/apertura/creación; no se forzó GC ni se vació Working Set.

- Manager + dock + dos contenedores, tres categorías: 201,73 MiB WS / 116,99 MiB privados / 2339 handles.
- Tras cerrar Lectura sin eliminar metadatos: 205,77 / 118,66 MiB / 2263 handles.
- Reabierto Lectura desde biblioteca: 211,82 / 123,18 MiB / 2342 handles.
- Antes de crear cuarta categoría (sección Dock ya cargada): 212,89 / 124,23 MiB / 2284 handles.
- Después de crear Medición mediante diálogo real: 223,43 / 132,93 MiB / 2300 handles.

Reabrir observa +6,04 MiB WS, +4,52 MiB privados y +79 handles respecto al cierre; incluye navegación/cachés y no permite atribuir todo al contenedor. Cerrar redujo 76 handles aunque la memoria no bajó. Añadir categoría observa +10,54 MiB WS/+8,70 MiB privados/+16 handles, incluyendo primera materialización del diálogo y refresco de controles; no es un coste constante por categoría. El retorno aproximado de handles tras cerrar/reabrir es alentador, pero no descarta crecimiento a largo plazo.

Arranque de PID 3440: startup-begin 22:41:48.442 UTC → manager-settings-loaded 22:41:49.137 UTC, **0,695 s**. Es tiempo de preparación medido en logs, no latencia de primer frame ni lanzamiento frío del sistema operativo. Cierres normales 14928/27904 con exit code 0 y ausencia de procesos antes de relanzar.
