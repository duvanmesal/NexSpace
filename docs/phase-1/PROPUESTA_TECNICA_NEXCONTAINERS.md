# Propuesta técnica · Fase 1 — NexContainers sobre FLOATING

**Estado:** implementada y consolidada; recorrido funcional real y suites automatizadas satisfactorios en sus alcances. Aceptación condicionada a drag-and-drop físico, estética final y validaciones de entorno pendientes (ver `docs/testing/PHASE1_VALIDATION.md`).
**Base:** decisiones de producto aprobadas, arquitectura modular actual y validación funcional básica de FLOATING en Fase 0.

## Objetivo y límites

Entregar el primer flujo útil de NexSpace: crear y renombrar contenedores, añadir referencias a archivos/carpetas/accesos directos, ordenarlas, abrir su destino y restaurar el estado al reiniciar. Cada contenedor se presenta inicialmente como una ventana FLOATING independiente. El objetivo no incluye declarar ni ofrecer integración DESKTOP.

La regla de seguridad central es que arrastrar un archivo agrega una referencia por defecto. Quitar una referencia o borrar un contenedor nunca borra, mueve ni copia el archivo original. Operaciones destructivas sobre el sistema de archivos quedan fuera de esta primera entrega.

## Arquitectura propuesta

- **Domain:** `NexContainer`, `ContainerItemReference`, reglas y geometría con identificadores estables; no guarda contenido ni copias de originales.
- **Application:** casos de uso agrupados en `ContainerService`, contratos de repositorio y plataforma, resultados controlados; `ReferenceDropProcessor` procesa metadatos sin WinUI ni filesystem.
- **Presentation:** `ContainerViewModel` portable con estado observable y comandos, probado separadamente; ninguna dependencia de SQLite, WinUI o Shell.
- **Infrastructure:** SQLite local versionado v1 y transacciones, pooling desactivado y disposición también ante fallo. Esquemas futuros se rechazan sin modificar/resetear datos. Backup automático/recuperación de corrupción no implementados ni certificados.
- **Windows:** apertura Shell explícita, selector que conserva .lnk y sondeo de acceso de solo lectura, sin mover/copiar/eliminar originales. Iconos se adaptan desde StorageFile/Folder en presentación.
- **UI:** WinUI 3, `ContainerWindow` top-level FLOATING por contenedor. La coordinación usa AppWindow directamente; no reutiliza la ruta experimental `IWindowHost.SetMode(Desktop)`. ViewModel enlazado para referencias, recursos NexGlass compartidos; consola/pickers/geometría aún requieren wiring code-behind. Posición limitada a monitor válido.
- **Integración futura desacoplada:** representar preferencia/capacidad mediante una interfaz de presentación independiente (por ejemplo, `IContainerPresentationHost`), con implementación FLOATING como única activa. Un adaptador DESKTOP futuro debe poder omitirse o reemplazarse sin alterar Domain, persistencia ni casos de uso; permanece deshabilitado y experimental hasta una decisión/validación aparte.

## Modelo inicial de datos

- `containers`: identidad, nombre, orden, timestamps, preferencias y geometría de ventana.
- `items`: catálogo de destinos compartible, tipo y clave normalizada.
- `container_items`: relación individual con etiqueta visible y orden por contenedor.

`target` es referencia, no propiedad del contenido. Se usan rutas absolutas y .lnk sin resolver en el selector; identidad robusta a renombres/movimientos queda fuera del MVP. Una ruta ausente/inaccesible muestra estado y permite quitar/reagregar referencia. No existe relocalización guiada.

## Interacciones seguras

1. Crear un contenedor vacío y guardarlo antes de mostrarlo.
2. Arrastrar archivo/carpeta: ejecutar solo “añadir referencia”; nunca operaciones físicas Move/Copy. La señal OLE `Copy` no representa copia de contenido; el texto y resumen lo aclaran. El gesto físico desde Explorer aún necesita validación humana.
3. Quitar ítem: eliminar únicamente la fila de relación/referencia. Etiquetar claramente cualquier operación futura de archivo real, no implementarla ahora.
4. Abrir ítem: acción deliberada (doble clic o comando) y errores claros para ruta inexistente o acceso denegado.
5. Cerrar/reiniciar: guardar cambios de forma transaccional y restaurar FLOATING sin ocultar ni reparentar ventanas del Shell.

## Secuencia aplicada y diferencias

1. Se implementó catálogo SQLite compartible (`items`) más relación individual (`container_items`), guardando etiqueta y orden por contenedor. Esto permite identidad común del destino sin forzar iguales nombres visibles.
2. Se implementaron servicios de aplicación, persistencia SQLite v1 y pruebas en rutas aisladas de `%TEMP%`.
3. Se implementaron consola WinUI, ventana por contenedor, pickers con HWND, drop de StorageItems, acciones contextuales, persistencia de preferencias y geometría.
4. Drop acepta una única operación Copy (nunca Move), deferral/finally y procesamiento separado; tests comparan originales, payload StorageItems WinRT, duplicados/tipos y comandos MVVM. No se sustituye el gesto Explorer→WinUI por estos tests.
5. Asociaciones de archivos, carpetas, .lnk, Bloc de notas y AppsFolder se observaron en el recorrido nativo inicial; apertura de documento se repitió tras consolidar. ACL denegada se verificó técnicamente en un archivo temporal propio con restauración explícita de permisos; falta mensaje visual con ACL.
6. Recuperación visible y cierre conjunto se probaron realmente. Persistencia se comparó en reinicios consecutivos; las ventanas se abren desde la biblioteca restaurada, no todas automáticamente.
7. NexGlass comparte estilos/plantillas/tiles/iconos y backdrop con fallback. La sesión desactiva efectos avanzados/animaciones; alternativa sólida observada, translucidez activa pendiente. Accesibilidad integral/DPI/multimonitor continúan manuales. DESKTOP permanece deshabilitado.

## Puertas de aceptación para el MVP

- Crear, renombrar y quitar contenedores persiste tras reiniciar la app.
- Añadir/reordenar/quitar referencias no modifica los archivos apuntados; pruebas automatizadas verifican esta invariante.
- Una ruta rota se identifica y permite quitar/reagregar referencia sin modificar fuentes; relocalización guiada no forma parte de la entrega.
- Abrir un destino requiere acción explícita y no crea operaciones de archivo ocultas.
- Cada ventana abre y se recupera en FLOATING dentro del área visible; cerrar NexSpace no deja procesos propios residuales.
- No se habilita DESKTOP ni se infiere compatibilidad con Explorer por el hecho de usar ventanas flotantes.

## Fuera de alcance

NexDock/NexManager, categorías compartidas, perfiles, temas avanzados, operaciones físicas de archivos, ocultamiento de iconos y DESKTOP estable. Recursos reutilizables y deuda restante en `docs/architecture/PHASE1_CONTAINERS.md`. No iniciar Fase 2 sin aprobación explícita; el detalle verificable y pendientes están en `docs/testing/PHASE1_VALIDATION.md`.
