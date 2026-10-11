# ADR-009 · Workspace integrado FLOATING para NexDock y NexManager

Fecha: 2026-10-11. Estado: implementado dentro de la autorización de Fase 2; aceptación funcional sujeta a matriz de validación. No modifica DEC-01/12 ni certifica DESKTOP.

## Decisión

Un proceso NexSpace, un ContainerService y un WorkspaceService compartidos. NexManager reemplaza la consola; NexDock y NexContainers son ventanas del mismo coordinador. Domain y Application no referencian WinUI, SQLite ni Shell. Presentation mantiene estado observable portable; UI enlaza ese estado y adapta eventos nativos, diálogos, pickers y ciclo de vida. Windows conserva el adaptador Shell existente. No se crean aplicaciones separadas ni dependencias circulares.

Una categoría del dock contiene referencias propias **o** un GUID de contenedor. La categoría enlazada resuelve el contenido actual mediante ContainerService, sin copiarlo; si desaparece el contenedor muestra estado vacío/no disponible. Los recursos físicos nunca son propiedad de NexSpace. Las acciones de apertura son explícitas y delegadas al mismo adaptador Windows.

SQLite v2 agrega workspace_state al esquema v1 en una transacción sin alterar sus filas. Un documento JSON tipado reúne categorías, preferencias de dock y sesión; revision se compara al guardar para rechazar escritores obsoletos. Es un agregado pequeño acotado (100 categorías, 1000 referencias por categoría), no un esquema para perfiles múltiples. Se conservan las tablas normalizadas de NexContainers; referencias propias del dock son metadatos independientes, no copias físicas. No hay pooling, cada conexión se dispone. Esquemas futuros o JSON inválido se rechazan sin resetear datos. Backup automático de corrupción sigue pendiente; una versión Fase 1 anterior no puede abrir esquema v2 y no debe usarse sobre la biblioteca migrada.

La primera apertura de almacenamiento se hace fuera del hilo WinUI antes de enlazar/restaurar la biblioteca. En esta sesión se observó fallo nativo de arranque al inicializar almacenamiento sin esa preparación, eliminado en el recorrido posterior. No se atribuye una causa interna de WinUI que no se haya depurado; se registra el límite de diagnóstico.

WorkspaceWindowCoordinator posee las ventanas, impide duplicados y conserva IDs abiertos al cerrar la sesión. Cerrar NexManager cierra dock y contenedores; cerrar solo el dock muestra Manager. Recuperación de consola/atajo global pasa por el mismo coordinador y limita las ventanas propias al área de trabajo. Nunca ejecuta HWND_BOTTOM ni escribe configuración de Explorer.

Dock libre conserva coordenadas DIP relativas al área de trabajo del monitor. El adaptador convierte a píxeles con DPI consultado; anclajes utilizan área de trabajo, no invaden barra de tareas. Se reutilizan NexGlass y ReferenceTile con iconos Shell. Los popovers solicitan salir del XamlRoot para no recortarse al alto compacto del dock, mediante [ShouldConstrainToRootBounds](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.primitives.flyoutbase.shouldconstraintorootbounds). Respeto a política de efectos y fallback sólido existentes, no transparencia simulada.

## Límites deliberados

- Auto-ocultamiento no ofrecido: falta certificar interacción de foco/popovers/monitor y recuperación; controles visibles antes que una implementación no validada.
- No se registra autoarranque Windows. Preferencias de apertura/restore solo se aplican al iniciar NexSpace explícitamente; no hay Registry/StartupTask/tareas programadas.
- No se implementan perfiles múltiples, editor de temas, operaciones físicas, NexLight ni Fase 3.
- Los pendientes de Fase 1 (DnD físico, accesibilidad, DPI/multimonitor real, translucidez/animación, memoria) siguen abiertos. Pruebas de políticas puras no certifican hardware ni accesibilidad.
- La consulta síncrona de destinos puede tardar con proveedores remotos; no se certifican bibliotecas masivas o cloud.
