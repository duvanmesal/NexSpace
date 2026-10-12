# ADR-011 · Estabilización y distribución local

Fecha: 2026-10-12. Estado: implementación de Fase 4, aceptación final pendiente. No reemplaza decisiones aprobadas de FLOATING, perfiles o seguridad.

## Decisiones

- Invalidar el layout del dock por sus controles, categorías y referencias, incluyendo orientación, etiquetas y espaciado. Cambiar solamente geometría o timestamps de contenedores no debe reconstruir menús.
- Evitar reaplicar un tema idéntico a todas las ventanas en cada escritura de posición. Forzar aplicación al crear contenido diferido en Manager; registrar ventanas nuevas con el tema vigente.
- Instrumentar entrada al constructor App, inicialización XAML y disponibilidad del dispatcher. Estos hitos no prueban por sí solos primer frame interactivo ni una causa nativa anterior al constructor.
- Diagnóstico de excepciones con tipo, HRESULT y nombres de métodos, sin mensajes ni argumentos. Los informes y trazas privadas nunca se versionan.
- Harness nativo opt-in mediante `NEXSPACE_DIAGNOSTIC_PROBE=1`, restringido a directorios temporales cuyo nombre empieza por `NexSpace-Phase4-`. Recrea ventanas y perfiles propios y cierra normalmente Manager. No abre recursos externos, no registra inicio automático ni cambia Windows. No sustituye interacción visual.
- La comparación `NEXSPACE_PROBE_FORCE_GC=1` es exclusivamente diagnóstica. No habilitarla en producción ni presentar un resultado con ella como corrección de fugas.
- Retirar manejadores de timers, AppWindow, XamlRoot y registro de temas al cerrar ventanas; despejar contenido y referencias visuales. Debe comprobarse bajo carga: el cambio por sí solo no demuestra liberación nativa.
- Publicar carpeta unpackaged sin trimming/single-file, con dependencias autocontenidas, antes de considerar una distribución firmada. La aceptación exige pruebas en máquina limpia, actualización, retirada y revisión humana.

## Hallazgos y consecuencias

El ensayo inicial de 100 ciclos terminó con un evento nativo `0xE0464645` en Microsoft.UI.Windowing.Core.dll y un evento previo `OutOfMemoryException` al abrir dock. Retirar solamente timers/handlers no resolvió la repetición; tampoco lo hizo forzar GC cada diez ciclos. Es un fallo de estrés reproducible, distinto del arranque histórico `0xC000027B`, cuya causa no está demostrada. Mantener ambos problemas separados y conservar evidencias anteriores.

La política de aceptación sigue siendo condicionada. Ninguna compilación, prueba de dominio ni traza sin análisis de retenciones certifica ausencia de fugas. Registrar el resultado de cada nueva repetición en PHASE4_VALIDATION.
