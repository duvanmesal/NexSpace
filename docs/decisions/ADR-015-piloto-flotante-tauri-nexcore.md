# ADR-015 · Capa flotante experimental con NexCore

23 de noviembre de 2026. Decisión circunscrita a la rama experimental.

## Decisión

NexContainers y NexDock comparten un host Tauri 2.12.7 y un proceso hijo .NET 10 por instancia. Cada superficie tiene un WebView con identidad de perfil y de grupo o categoría. NexCore conserva las reglas de contenedores, perfiles, categorías, persistencia SQLite v3 y apertura mediante las asociaciones de Windows. El selector de archivos y carpetas usa rfd 0.18.0. La recuperación por teclado usa global-hotkey 0.10.0.

El punto de entrada ordinario restaura la biblioteca del piloto. La opción explícita `--legacy` conserva el laboratorio histórico. El piloto usa una biblioteca preparada en su carpeta privada y permite importar una instantánea de demostración mediante SQLite Backup. La decisión de producción continúa en ADR-014.

## Motivos y costes

La separación permite cambiar de perfil, añadir una referencia o reordenar una categoría mediante los servicios del producto. Un popover enlazado consulta las referencias del mismo contenedor que muestra la cuadrícula. El proceso nativo posee las ventanas, la bandeja y el atajo para poder recuperar el dock cuando el contenido web pierde el foco.

WebView2 añade procesos, memoria y handles. El protocolo necesita identificar respuestas, rechazar escrituras de perfiles antiguos y reconciliar las ventanas después de una mutación. Perder la respuesta de una escritura exige releer el estado antes de decidir si se repite. La evidencia de 100 y 300 ciclos y las condiciones pendientes están en la [matriz del piloto](../../prototypes/web-shell/PARIDAD.md).

## Seguimiento de validación

La rama experimental procesa el evento de arrastre nativo de la ventana, conserva geometría en píxeles físicos por dispositivo y solicita iconos a Shell a través del sidecar. Se mantiene SQLite v3 para las bibliotecas. El ajuste al área de trabajo del monitor es reversible mientras no se guarde una ubicación nueva. La [validación de producto](../../prototypes/web-shell/VALIDACION_PRODUCTO.md) documenta el bloqueo que corrigió este cambio y los ensayos pendientes para promover la interfaz.

## Procedencia del piloto y aislamiento de ensayos

El piloto se compila en Windows x64 con Node 24.22.0, Vite 7.3.8, Tauri 2.12.7, Rust/Cargo 1.100.0 GNU y .NET SDK 10.0.401 en el equipo ensayado. La salida versionada incluye una huella de entrada y hashes del ejecutable, cargador y sidecar. Cada arranque del lanzador asigna una base SQLite y una identidad de sesión independientes. El host acepta el cierre ordinario mediante un archivo local que solo actúa sobre el ID de la sesión. Las trazas de OLE se cotejan con esa identidad y con las filas reales en SQLite. [Guía del entorno](../../prototypes/web-shell/ENTORNO_TAURI.md)
