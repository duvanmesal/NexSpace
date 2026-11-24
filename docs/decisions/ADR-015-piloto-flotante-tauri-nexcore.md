# ADR-015 · Capa flotante experimental con NexCore

23 de noviembre de 2026. Decisión circunscrita a la rama experimental.

## Decisión

NexContainers y NexDock comparten un host Tauri 2.12.7 y un proceso hijo .NET 10 por instancia. Cada superficie tiene un WebView con identidad de perfil y de grupo o categoría. NexCore conserva las reglas de contenedores, perfiles, categorías, persistencia SQLite v3 y apertura mediante las asociaciones de Windows. El selector de archivos y carpetas usa rfd 0.18.0. La recuperación por teclado usa global-hotkey 0.10.0.

El punto de entrada ordinario restaura la biblioteca del piloto. La opción explícita `--legacy` conserva el laboratorio histórico. El piloto usa una biblioteca preparada en su carpeta privada y permite importar una instantánea de demostración mediante SQLite Backup. La decisión de producción continúa en ADR-014.

## Motivos y costes

La separación permite cambiar de perfil, añadir una referencia o reordenar una categoría mediante los servicios del producto. Un popover enlazado consulta las referencias del mismo contenedor que muestra la cuadrícula. El proceso nativo posee las ventanas, la bandeja y el atajo para poder recuperar el dock cuando el contenido web pierde el foco.

WebView2 añade procesos, memoria y handles. El protocolo necesita identificar respuestas, rechazar escrituras de perfiles antiguos y reconciliar las ventanas después de una mutación. Perder la respuesta de una escritura exige releer el estado antes de decidir si se repite. La evidencia de 100 y 300 ciclos y las condiciones pendientes están en la [matriz del piloto](../../prototypes/web-shell/PARIDAD.md).
