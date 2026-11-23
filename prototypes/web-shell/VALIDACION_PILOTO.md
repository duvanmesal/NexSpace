# Validación del piloto NexCore · 23 de noviembre de 2026

**Decisión recomendada:** conservar WinUI como producto y mantener Tauri en investigación. Archivos demuestra reutilización efectiva de NexCore con persistencia, apertura Shell y recuperación independiente del foco. El piloto conservó una ventaja de memoria privada frente al host A en las tres escenas históricas comparables. Solo un grupo está conectado. Aún falta comparar con WinUI bajo idéntica carga funcional.

## Pruebas

| Estado | Ensayo y evidencia |
|---|---|
| PASS | Build Release de React/TypeScript/Vite, Tauri y sidecar .NET x64 autocontenido. El enlazador GNU advierte sobre manifiestos no predeterminados duplicados. Las pruebas usaron los ejecutables unpackaged. |
| PASS | La base de demostración leída en modo de solo lectura tenía esquema 3 y cuatro contenedores. La instantánea privada preparada tiene esquema 3 y cinco. La prueba de protocolo verificó catálogo, pertenencia, icono Shell, orden, nombre y geometría después de reiniciar el sidecar. El hash de los TXT coincidió al terminar. |
| PASS | La apertura por ID se delegó a Windows. Inspección visual del Bloc de notas con ficha-0.txt y el contenido de demostración. La referencia ausente devolvió `missing`. Versión incorrecta, operación desconocida e ID ajeno devolvieron errores diferenciados. |
| PASS | Ventana real movida a (120,84) y ajustada a 427 × 305. Después de contraer y recrear su WebView, React restauró 427 × 52 y la posición guardada. Expandir recuperó 305. El controlador de recuperación volvió a mostrar Archivos con todas las ventanas previamente ocultas. |
| PASS | SQLite bloqueada exclusivamente en la copia de prueba. El host detectó el timeout. Cancelar la siguiente petición interrumpió el intercambio. Tras liberar la base y terminar deliberadamente el sidecar, el host notificó la pérdida de conexión y pudo recuperar el mismo estado en otra solicitud. La terminación abrupta de un host dedicado también hizo salir a su hijo .NET. |
| PASS | Diez arranques independientes con Archivos conectado. Mediana hasta recibir la confirmación de React con datos de 0,99 s (0,93–1,04). Diez cierres normales. Ningún descendiente conocido a los 3 s en la serie final. |
| PASS | 100 cierres y reaperturas de Archivos y 100 ciclos del popover conectado, con confirmación de React por apertura. Código 0 y ningún proceso observado superviviente al control de cierre. |
| NOT_TESTED | Cambios repetidos de perfil desde React. Juegos y Aplicaciones siguen siendo fixtures del laboratorio. El piloto carece de selector de perfiles y NexManager permanece en WinUI. |
| NOT_TESTED | Activación física desde el menú de bandeja con Narrador, DPI mixto y desconexión de monitor. Se ejercitó el mismo controlador de recuperación desde el ensayo nativo. La distribución firmada, la latencia física clic-a-píxel y la paridad de accesibilidad quedan pendientes. |

En la base local de Fase 4.1, la solución WinUI compiló Release x64 y pasaron 43 pruebas unitarias y 33 de integración. La solución heredada de la candidata integrada compiló Debug x64 en el checkout experimental y pasaron 92 unitarias y 35 de integración. Ambos builds de la solución terminaron con cero advertencias y errores.

## Recursos del árbol completo

Medición del 23 de noviembre con el censo de procesos del laboratorio. Incluye el host Tauri, WebView2, conhost y un único NexCore. La primera disponibilidad puede corresponder a otra ventana. Archivos solo emite su confirmación tras leer la biblioteca. Los registros crudos se conservan bajo `artifacts/tauri-nexcore/evidence/` fuera de Git. Las series se tomaron antes de añadir el escenario nativo adicional `--validate`, que solo se activa por argumento.

| Escena | Primera / todas listas (s) | Procesos | Privada / WS agregados (MiB) | Handles | GDI / USER | Composition / Event | CPU reposo (% de un núcleo) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Archivos | 1,09 / 1,19 | 9 | 179,2 / 419,0 | 4498 | 91 / 135 | 0 / 1700 | 0,0 |
| Tres grupos | 0,99 / 1,44 | 11 | 266,3 / 556,9 | 5260 | 119 / 167 | 0 / 1955 | 1,2 |
| Tres grupos, dock y popover | 1,06 / 1,46 | 13 | 336,1 / 699,7 | 5986 | 150 / 197 | 0 / 2205 | 1,2 |

| Memoria privada (MiB) | A histórico (WPF/WebView2) | B histórico (Tauri) | Tauri + NexCore |
|---|---:|---:|---:|
| Archivos | 291,3 | 155,9 | 179,2 |
| Tres grupos | 487,3 | 253,1 | 266,3 |
| Tres grupos, dock y popover | 746,4 | 309,9 | 336,1 |

Entre uno y tres grupos, el incremento medio fue 43,6 MiB por ventana añadida. Las dos ventanas añadidas eran fixtures. En el ensayo completo, la privada pasó de 329,4 MiB en reposo inicial con cuatro superficies a 336,1 con el popover visible. Durante ese tramo también se contrajo Juegos y se redimensionó Aplicaciones. La diferencia de 6,7 MiB incluye esos cambios. Tras nueve cargas confirmadas de popover y su cierre, el árbol registró 311,6 MiB, 5826 handles y 2130 Event, frente a 5621 handles y 2080 Event antes de abrirlo. La CPU durante la serie rápida fue del 30,0 % de un núcleo. Las tres escenas terminaron normalmente sin supervivientes conocidos a los 3 ni a los 10 s.

En el estrés largo la primera y última muestra registraron 306,7 → 386,6 MiB privados, 547,8 → 725,5 MiB de WS, 5006 → 5896 handles, 129 → 138 GDI, 142 → 189 USER, 0 → 0 Composition y 1904 → 2131 Event. Pico privado observado de 407,8 MiB. Las muestras incluían 11 y 12 procesos respectivamente. El aumento merece repetir el ensayo con la misma topología y reposo prolongado. El WS agregado puede contar páginas compartidas varias veces. El censo de handles no mide la memoria del compositor ni de la GPU.

## Fallos encontrados

- La primera serie paró tras dos ciclos porque Tauri aún retenía una etiqueta de WebView después de destruir la ventana. Se serializaron las transiciones del popover y se acotó la espera para reutilizar etiquetas retiradas. El escenario completo terminó después de la corrección.
- En un primer ensayo de arranques, el muestreador incluyó dos procesos de Bloc de notas creados antes del piloto. Windows había reutilizado su PID padre. El censo ahora exige parentesco y fecha de creación compatible. Se repitieron los arranques, las tres escenas y el estrés con el recuento corregido.

## Comparación visual y alcance de migración

Las capturas privadas de `comparacion/anterior.png` y `comparacion/conectado.png` muestran ambos ejecutables sobre el mismo fondo de Windows dibujado en una ventana de prueba. Son ventanas reales. El dock, las posiciones y la estructura de Archivos se conservaron. Antes, las fichas tenían ilustraciones por categoría. Ahora los cinco archivos existentes comparten el icono real de su asociación TXT, y la referencia ausente expone su estado. Hacen falta fixtures con tipos diversos para valorar la navegación por reconocimiento de iconos.

La publicación autocontenida de NexCore añade aproximadamente 145 MiB de archivos al paquete de laboratorio antes de optimización. Como estimación de ingeniería, reservar **6–11 semanas-persona adicionales** para ampliar iconos, selector, arrastre y tipos Shell (1–3), recuperación/perfiles/multimonitor (2–4) y accesibilidad, perfilado y distribución (3–4). El margen presupone conservar NexManager WinUI y los servicios actuales. Una migración total de NexManager requiere inventario y estimación aparte.

La siguiente decisión de producto requiere paridad de perfiles, una comparación de 300 ciclos con WinUI usando las mismas referencias y ventanas, y validación de recuperación con DPI mixto. El piloto justifica continuar ese ensayo. La aplicación de producción debe seguir en WinUI durante esa evaluación.
