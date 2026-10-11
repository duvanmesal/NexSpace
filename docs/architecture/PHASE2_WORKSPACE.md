# Arquitectura · Workspace integrado de Fase 2

Fecha: 2026-10-11. Decisión: [ADR-009](../decisions/ADR-009-integrated-workspace.md). No autoriza Fase 3 ni activa DESKTOP.

## Un proceso, una biblioteca

NexManager es la ventana principal del proceso WinUI existente, no una aplicación nueva. MainPage compone ContainerService, WorkspaceService y WindowsContainerItemPlatformService compartidos. WorkspaceWindowCoordinator posee el dock y las ventanas de contenedores por GUID; abrir una ventana existente la activa, no la duplica.

Domain define NexWorkspace, DockSettings, SessionPreferences y DockCategory. Application define IWorkspaceRepository, WorkspaceService y DockPlacementPolicy. Presentation aporta WorkspaceViewModel observable y reutiliza ContainerItemViewModel. Infrastructure implementa SQLite; Windows conserva inspección/apertura Shell y monitores. UI adapta eventos, diálogos/pickers y AppWindow. No hay dependencias circulares ni WinUI en Domain/Application/Presentation.

Las reglas de negocio permanecen en Application/Domain. El wiring nativo y la construcción de menús todavía residen en code-behind; no se afirma separación MVVM exhaustiva ni se introduce otro framework.

## Persistencia y migración

Se mantiene `%LOCALAPPDATA%\NexSpace\data\nexspace.db` o la carpeta `NEXSPACE_DATA_DIR`. La migración transaccional v1→v2 agrega solamente workspace_state: id fijo 1, revision y definition JSON. Las tablas containers, items y container_items mantienen identidad y relaciones. Una prueba crea biblioteca v1 aislada, migra y comprueba sus filas.

El JSON tipado persiste categorías ordenadas, GUID/nombre/glifo, ContainerId opcional y referencias propias; anclaje libre/inferior/superior/izquierdo/derecho, monitor, X/Y DIP relativas al área de trabajo, tamaño/acento/visibilidad inicial del dock; restauración de contenedores, visibilidad inicial de Manager e IDs abiertos.

WorkspaceRules valida nombres/identidades, duplicados, rutas absolutas, recursos Shell permitidos, números finitos y límites (100 categorías, 1000 referencias por categoría y 1000 IDs de sesión). Debe quedar Manager o Dock accesible al iniciar. Save compara revisión previa dentro de una transacción y rechaza escritores obsoletos. No hay reset silencioso de JSON corrupto/esquemas futuros ni pooling de conexiones. La versión anterior de Fase 1 rechaza v2: **no hacer downgrade sobre la biblioteca migrada**. Antes de migrar datos personales, respaldar la base con NexSpace cerrado; backup/restauración de corrupción automáticos no se incluyen.

Una categoría es propia o enlazada. La enlazada consulta el contenido actual del contenedor sin clonarlo. Cambios notifican a Manager y Dock; eliminar la categoría no elimina el contenedor, y un enlace ausente queda vacío/no disponible. No se permite enlazar una categoría propia con referencias descartándolas silenciosamente. Ningún agregado posee los originales físicos.

## Ventanas, recuperación y sesión

La primera apertura de almacenamiento se prepara en Task.Run antes de enlazar/restaurar ventanas. Las modificaciones posteriores siguen serializadas por eventos UI. Observadores y errores de registro no invalidan metadatos ya guardados.

Cerrar Manager guarda todos los IDs abiertos antes de cerrar las ventanas, detiene el temporizador del dock y libera popovers, backdrop y caché de iconos. Cerrar solo el dock muestra Manager; cerrar un contenedor individual actualiza sesión. Aplicaciones abiertas por Shell son independientes y no se terminan.

Recuperar ventanas y Ctrl+Alt+Shift+F12 usan el mismo coordinador: encuadran únicamente ventanas propias en el área de trabajo y muestran Manager. El dock convierte DIP a píxeles según el monitor consultado. Libre guarda posición con debounce de 450 ms; anclado conserva el borde. XamlRootChanged recalcula colocación. Esto no certifica transiciones físicas DPI/multimonitor pendientes.

## Presentación compartida y límites

NexGlass.xaml aporta pinceles, tipografía, superficies y estilos de selección/foco. ReferenceTile y NexReferenceTemplate/NexReferenceItemStyle se usan en contenedores, Manager y popovers; ShellIconLoader mantiene iconos reales. NexGlassWindow aplica material/fallback al dock igual que a NexContainers. Manager usa superficie sólida de los mismos tokens para legibilidad.

El dock tiene desplazamiento para muchas categorías, nombres truncados con tooltip/nombre accesible, controles visibles de Manager/recuperación y popovers fuera del alto compacto de su raíz. Administrar una categoría selecciona esa categoría en Manager. No hay imágenes estáticas simulando controles.

Auto-ocultamiento opcional se difiere hasta validar foco/popovers/recuperación. No se registra inicio Windows ni se implementan bandeja, perfiles múltiples o editor de temas. Preferencias de inicio solo afectan el lanzamiento explícito. La geometría de Manager no se persiste; su recuperación sí está integrada.

La inspección síncrona de destinos remotos y recarga amplia de biblioteca limitan rendimiento; no se certifican bibliotecas masivas/cloud. El objetivo orientativo de memoria sigue pendiente. Accesibilidad/alto contraste requieren validación manual. Materiales y animación respetan Windows, sin cambios globales. DESKTOP permanece experimental/deshabilitado.

Resultados y repetición: [PHASE2_VALIDATION](../testing/PHASE2_VALIDATION.md).
