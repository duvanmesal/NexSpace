# Decisión del laboratorio de ventanas web

**22 de noviembre de 2026. Recomiendo B (Tauri 2) para un piloto con un contenedor conectado a NexCore por IPC privado.** Mantener la candidata WinUI como producto funcional. La apariencia es equivalente y el árbol de B tuvo menor coste en esta prueba. B debe repetir la comparación incluyendo el proceso .NET, SQLite, Shell y recuperación antes de decidir una migración.

## Comparación frente al Concepto C

| Elemento | Evidencia de ejecución |
|---|---|
| Distribución | Tres ventanas flotantes, dos en el borde izquierdo y una en el derecho. Centro libre y dock inferior centrado de 530 × 76 unidades lógicas. |
| Contenedores | Encabezado integrado, cuadrícula de tres columnas y seis fichas por grupo. El contorno redondeado deja ver el escritorio alrededor de las esquinas. |
| Material | La textura real del escritorio se distingue a través de la superficie. Falta el desenfoque y la profundidad de sombra de la ilustración. La translucidez procede del alfa del documento, del WebView y de la ventana. |
| Popover | Ventana de 228 × 266, lista de seis fichas y cola dirigida a la categoría seleccionada. Cada apertura crea un WebView. |
| Diferencias visuales | Los iconos son fixtures de Fluent Emoji. La referencia original muestra iconos Shell de aplicaciones y una tercera fila en Aplicaciones. |
| Movimiento y recuperación | En A y B el arrastre movió Archivos de (24,24) a (96,71). El tirador redujo 386 × 272 a 347 × 240. Ctrl+Mayús+Inicio restauró (24,24) conservando el tamaño. |
| Apertura y accesibilidad | Un clic en Documentos abrió reference-0.txt en A. Proyectos abrió reference-1.txt en B. Se verificaron ambas fichas en Bloc de notas. UI Automation expuso el encabezado, los seis botones con sus nombres, opciones, contracción y tirador. |

La pareja privada `artifacts/web-lab/evidence/desktop-pass/{A,B}-popover.png` muestra el mismo escritorio real con Juegos contraído y Aplicaciones agrandado. Las tomas idle-start documentan los tres grupos expandidos. Se conservan también capturas de foco, pérdida de foco y redimensionamiento. [Referencia aprobada](../../assets/references/concepts/03_C_sistema_hibrido_APROBADO.png).

## Matriz de arquitectura

| Criterio | A · C# y WebView2 | B · Tauri 2 |
|---|---|---|
| Fidelidad y composición | Alta para disposición y controles. WPF con ventana transparente y CompositionControl. | Equivalente, con wry y transparencia nativa. |
| Reutilización | Adaptador Shell real de NexCore probado en el mismo proceso. | Apertura controlada desde Rust. El sidecar .NET queda pendiente. |
| Recursos | Mayor coste en la implementación medida. Conviene probar un único popover reutilizable. | Menor coste medido. El sidecar elevará el total. |
| Estabilidad | Serie corta y cierre observado. Falta reproducir 100/300 ciclos con perfiles reales. | Mismo límite de evidencia. Añadir desconexión y reinicio del sidecar. |
| Accesibilidad | Controles del documento accesibles en UIA. Narrador y DPI mixto pendientes. | Mismos controles y pendientes. |
| Distribución | .NET autocontenido, Evergreen y un solo lenguaje nativo mantenido por NexSpace. | Ejecutable, DLL del cargador y Evergreen. La distribución completa añadiría el motor .NET. |
| Mantenimiento y ampliaciones | Menos fronteras de proceso. Las ventanas adicionales incrementan el coste. | React se puede compartir con futuras islas y widgets. Se añaden IPC, coordinación y diagnóstico distribuido. |

La evaluación de escritorio se limitó a FLOATING, un monitor y la asociación Windows para abrir archivos de prueba. La recuperación está disponible desde una superficie enfocada. Quedan pendientes el atajo global independiente, desconexión de monitor, DPI mixto, arrastre desde Explorer, Narrador, integración de perfiles y estrés prolongado. Las comprobaciones de Acrylic de la candidata WinUI corresponden a otro material y otra arquitectura.

## Protocolo

Windows x64 build 26300, Ryzen 5 3600, pantalla principal 1920 × 1080, WebView2 Evergreen 157.0.4341.60. React 19.3.1, TypeScript 5.9.3, Vite 7.3.8 y Tailwind 4.3.5. A usa .NET 10 y SDK WebView2 1.0.3719.77. B usa Tauri 2.12.7, Rust 1.100.0 y GCC 16.2. Ambos ejecutan el mismo dist con la preferencia Windows de transparencia desactivada.

- Un lanzamiento independiente por celda de la serie final, con perfil Chromium nuevo y caché del SO sin purgar. Los tiempos miden desde el lanzamiento hasta el primer mensaje de React tras dos callbacks de renderizado y hasta la disponibilidad de todas las superficies iniciales. La latencia de una interacción física requiere instrumentación aparte.
- La memoria privada y los handles se suman sobre el árbol de procesos. La suma de working sets puede contar páginas compartidas varias veces. El compositor DWM compartido queda fuera. El censo acotado identifica Composition y Event por nombre de tipo. Cero objetos Composition observados no estima el coste de la GPU. [Modelo de procesos de WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model).
- CPU en porcentaje de un núcleo, con unos cuatro segundos de observación en reposo. La CPU de renderers que terminen entre dos muestras puede quedar fuera de la cifra de interacción. La memoria incremental por ventana se estima a partir de dos lanzamientos independientes.
- El ensayo completo incluye contracción, redimensionamiento, cambios de tema y foco, apertura inicial de popover y ocho reaperturas solicitadas. Se cuentan los mensajes de disponibilidad por ventana. Se verifican procesos conocidos a los 3 y 10 s del cierre. Se aplican límites de arranque, handles y memoria privada.
- La prueba WinUI de Fase 4.1 mide otro patrón con 100 y 300 ciclos y servicios adicionales. Estos resultados sirven para decidir qué integración merece un piloto. La comparación de estabilidad con el producto requiere una carga idéntica. [Matriz WinUI](../../docs/testing/CONCEPT_C_PHASE41_VALIDATION.md).

## Mediciones de los binarios de entrega

| Host y escena | Lista React inicial / todas (s) | Procesos | Privada / WS (MiB) | Handles | GDI / USER | Composition / Event | CPU en reposo (% de un núcleo) |
|---|---:|---:|---:|---:|---:|---:|---:|
| A · 1 contenedor | 2,46 / 2,50 | 7 | 291,3 / 546,4 | 7325 | 49 / 110 | 0 / 4738 | 1,6 |
| B · 1 contenedor | 0,69 / 0,75 | 7 | 155,9 / 364,5 | 3984 | 45 / 126 | 0 / 1591 | 0,0 |
| A · 3 contenedores | 1,60 / 2,74 | 9 | 487,3 / 790,4 | 11300 | 81 / 139 | 0 / 8102 | 0,4 |
| B · 3 contenedores | 0,85 / 1,07 | 9 | 253,1 / 512,6 | 4754 | 73 / 157 | 0 / 1848 | 0,4 |
| A · 3 + dock + popover | 1,59 / 2,55 | 11 | 746,4 / 1048,1 | 15261 | 118 / 167 | 0 / 11449 | 2,0 |
| B · 3 + dock + popover | 0,86 / 1,33 | 11 | 309,9 / 656,3 | 5467 | 104 / 184 | 0 / 2092 | 1,2 |

| Tras ocho solicitudes de reapertura | Cargas de popover confirmadas (incluye la inicial) | CPU de interacción (% de un núcleo) | Privada inicial → final (MiB) | Handles inicial → final | Composition / Event inicial → final | Procesos vivos a 3 / 10 s del cierre |
|---|---:|---:|---:|---:|---:|---:|
| A | 6 | 23,8 | 582,1 → 963,7 | 13247 → 18517 | 0 / 9770 → 0 / 14563 | 0 / 0 |
| B | 9 | 21,1 | 286,0 → 288,6 | 5086 → 5297 | 0 / 1964 → 0 / 2018 | 0 / 0 |

La diferencia de memoria privada entre uno y tres contenedores equivale a unos 98,0 MiB por contenedor adicional en A y 48,6 MiB en B. Con cinco ventanas, ambos árboles tenían once procesos. En la prueba rápida A confirmó seis de las nueve cargas de popover solicitadas antes de los cierres programados. B confirmó nueve. Este resultado acota la comparabilidad de la CPU de interacción. El cierre normal terminó los seis ensayos sin procesos conocidos supervivientes a los 10 s.

El aumento de memoria privada entre reposo inicial y reposo final del ensayo completo fue 381,6 MiB en A y 2,6 MiB en B. Los handles también aumentaron en ambos. El ensayo finaliza pocos segundos después del último cierre del popover. Falta una serie larga con minutos de reposo para determinar cuánto se libera después. En B hay que sumar el coste futuro de NexCore antes de extrapolar al producto. La carpeta autocontenida de A ocupa 167,9 MiB y la de B 26,6 MiB, excluyendo el runtime Evergreen común.

El perfil claro se propagó desde el dock a los contenedores en ambos ejecutables en la inspección de la captura light. Abrir una ventana normal delante de los grupos permitió verificar oclusión y pérdida de foco. Los prototipos quedan cerrados tras las mediciones. Para reproducir o continuar el piloto, consultar [instrucciones, mapa de reutilización y estimación por módulo](README.md).
