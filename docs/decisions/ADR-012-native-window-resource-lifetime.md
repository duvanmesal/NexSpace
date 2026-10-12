# ADR-012 · Recursos de ventanas y mitigación de churn nativo

Fecha: 2026-10-12. Estado: implementada localmente en Fase 4.1; aceptación de Fase 4 pendiente. Complementa ADR-011, no habilita DESKTOP ni modifica el esquema de datos.

## Evidencia

La ruta de título extendido y colores de caption dispara crecimiento de GDI reproducible, incluso en ventanas vacías. DispatcherQueueTimer por ventana añade Timer/WaitCompletionPacket por ciclo. Retirar ambos patrones reduce drásticamente recursos, pero no elimina Composition/Event. Un EXE WinUI independiente sin referencias a NexSpace reproduce +100 Composition en 100 ventanas cerradas, incluso sin Content. Informe y límites: `docs/testing/PHASE41_PERFORMANCE.md`.

## Decisión

- ContainerWindow usa caption del sistema; no conservar fila de arrastre duplicada. NexThemeManager no escribe colores del caption en producción. Dock conserva su presenter sin caption, sometido a comparación propia.
- Debounce gestionado cancelable y callback validado por generación en el dispatcher. Dispose invalida trabajo pendiente y ya encolado. No GC forzado en producción.
- Limpieza completa de handlers/Closed/DataContext/Children al cerrar contenedor; limpieza de timers, AppWindow, Loaded/XamlRoot, contenido y backdrops propios. La mejora del conjunto no se atribuye sin evidencia a un único callback.
- Instrumentación solo opt-in con datos temporales, presupuestos verificables y supervisor externo del proceso de prueba. No consultar nombres de objetos ni manipular handles de otros procesos.
- No Dispose del compositor perteneciente a XAML ni cierre manual de handles cuyo dueño no se conoce. No adoptar pooling, actualización de dependencias, reinicio automático o terminaciones de sesiones normales como falsa solución.

## Consecuencias

Se sacrifica personalización del caption en favor de recursos GDI estables. El contenido mantiene NexGlass, MVVM y servicios compartidos; la geometría sigue persistente y el cierre del sistema sigue funcional. Se conserva la ruta antigua exclusivamente para experimentos aislados y acotados, nunca habilitada por defecto.

La reproducción mínima acota el residual al conjunto WinUI/runtime/OS probado, no a una función concreta. La mitigación operativa es guardar/cerrar/reabrir voluntariamente durante pruebas prolongadas. Abrir un issue público requiere revisión previa y no debe incluir evidencia privada. Lanzamiento 1.0 sigue bloqueado por crecimiento no estabilizado y pendientes humanos.
