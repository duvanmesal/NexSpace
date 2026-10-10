# Propuesta técnica · Fase 1 — NexContainers sobre FLOATING

**Estado:** propuesta para revisión; no autoriza ni inicia implementación.  
**Base:** decisiones de producto aprobadas, arquitectura modular actual y validación funcional básica de FLOATING en Fase 0.

## Objetivo y límites

Entregar el primer flujo útil de NexSpace: crear y renombrar contenedores, añadir referencias a archivos/carpetas/accesos directos, ordenarlas, abrir su destino y restaurar el estado al reiniciar. Cada contenedor se presenta inicialmente como una ventana FLOATING independiente. El objetivo no incluye declarar ni ofrecer integración DESKTOP.

La regla de seguridad central es que arrastrar un archivo agrega una referencia por defecto. Quitar una referencia o borrar un contenedor nunca borra, mueve ni copia el archivo original. Operaciones destructivas sobre el sistema de archivos quedan fuera de esta primera entrega.

## Arquitectura propuesta

- **Domain:** `Container` y `ContainerItemReference` con identificadores estables; nombre, orden y `WindowPlacement`; referencias con tipo y ruta/identidad de destino. No guardar contenido del archivo ni mantener una copia del original.
- **Application:** casos de uso pequeños (`CreateContainer`, `RenameContainer`, `AddReference`, `RemoveReference`, `ReorderReferences`, `OpenReference`). Interfaces para repositorio, apertura del destino y host de ventanas. Validaciones y resultados sin dependencia de WinUI.
- **Infrastructure:** SQLite local con migraciones versionadas y transacciones; almacenamiento en `%LOCALAPPDATA%\NexSpace`. Política de backup/recuperación de base de datos y comportamiento ante rutas ausentes antes de declarar estable la persistencia.
- **Windows:** abrir destinos mediante Shell con consentimiento explícito del usuario; resolución de iconos/metadatos como servicio sustituible. El módulo no realiza operaciones de mover/copiar/eliminar.
- **UI:** WinUI 3 + MVVM. Una `ContainerWindow` top-level por contenedor; todas las ventanas usan `IWindowHost` y el modo predeterminado/fase inicial es FLOATING. Persistir y restaurar tamaño/posición dentro de un monitor válido.
- **Integración futura desacoplada:** representar preferencia/capacidad mediante una interfaz de presentación independiente (por ejemplo, `IContainerPresentationHost`), con implementación FLOATING como única activa. Un adaptador DESKTOP futuro debe poder omitirse o reemplazarse sin alterar Domain, persistencia ni casos de uso; permanece deshabilitado y experimental hasta una decisión/validación aparte.

## Modelo inicial de datos

- `containers`: `id`, `name`, `sort_order`, `created_at`, `updated_at`.
- `container_items`: `id`, `container_id`, `kind`, `target`, `display_name`, `sort_order`, `created_at`.
- `container_window_state`: `container_id`, `monitor_key`, `x`, `y`, `width`, `height`.

`target` es una referencia, no propiedad del contenido. El esquema puede comenzar con rutas absolutas de Windows para archivos/carpetas y una estrategia explícita para `.lnk`; identidad robusta a renombres/movimientos y Shell items debe evaluarse antes de ampliar formatos. Una ruta inaccesible se muestra como referencia rota y permite reubicar o quitar la referencia sin tocar el destino.

## Interacciones seguras

1. Crear un contenedor vacío y guardarlo antes de mostrarlo.
2. Arrastrar archivo/carpeta: ejecutar solo la semántica de “añadir referencia”; evitar `Move`/`Copy` de OLE drag-and-drop y mostrar confirmación/resumen del destino añadido.
3. Quitar ítem: eliminar únicamente la fila de relación/referencia. Etiquetar claramente cualquier operación futura de archivo real, no implementarla ahora.
4. Abrir ítem: acción deliberada (doble clic o comando) y errores claros para ruta inexistente o acceso denegado.
5. Cerrar/reiniciar: guardar cambios de forma transaccional y restaurar FLOATING sin ocultar ni reparentar ventanas del Shell.

## Secuencia de entrega propuesta

1. Acordar alcance de MVP y modelo de identidad de referencias; registrar cualquier nueva decisión en ADR.
2. Implementar modelo, SQLite/migraciones y pruebas de invariantes sin UI.
3. Crear/renombrar/quitar contenedores y referencias desde una vista WinUI flotante.
4. Añadir ordenamiento y drag-and-drop con pruebas que fallen si se invoca una operación física de archivo.
5. Implementar abrir destino, rutas rotas y manejo de permisos; probar con archivos temporales aislados.
6. Persistir/restaurar ventanas y verificar reinicio/cierre; probar instalación local y recuperación de datos.
7. Revisión manual de accesibilidad, DPI y multimonitor. Evaluar DESKTOP solo como trabajo separado con evidencia y aprobación propias.

## Puertas de aceptación para el MVP

- Crear, renombrar y quitar contenedores persiste tras reiniciar la app.
- Añadir/reordenar/quitar referencias no modifica los archivos apuntados; pruebas automatizadas verifican esta invariante.
- Una ruta rota se identifica y puede corregirse sin pérdida silenciosa.
- Abrir un destino requiere acción explícita y no crea operaciones de archivo ocultas.
- Cada ventana abre y se recupera en FLOATING dentro del área visible; cerrar NexSpace no deja procesos propios residuales.
- No se habilita DESKTOP ni se infiere compatibilidad con Explorer por el hecho de usar ventanas flotantes.

## Fuera de alcance

NexDock, categorías compartidas, perfiles, temas avanzados, operaciones físicas de archivos, ocultamiento de iconos y DESKTOP estable. Esta propuesta requiere revisión y autorización antes de comenzar Fase 1.
