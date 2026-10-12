# Arquitectura · Fase 3

Domain agrega NexProfile, ProfileState, ProfileSnapshot, ThemeSettings y DockOrientation. Application define IProfileRepository/ProfileService e IStartupRegistration. Infrastructure implementa SqliteProfileRepository y migra a v3 con backup previo. Presentation agrega ProfilesViewModel. Windows adapta bandeja/inicio Windows. UI compone todos los servicios sobre una única base y coordina ventanas existentes, sin apps independientes.

NexThemeManager y Resources/NexGlass.xaml son reutilizables para todos los módulos; ReferenceTile mantiene iconos reales, texto/errores accesibles, plantillas de lista/cuadrícula. Manager, dock y contenedores reciben el mismo tema activo; los popovers consumen pinceles semánticos compartidos. Los perfiles guardan la configuración visual, dock, preferencias de sesión y referencias; el perfil predeterminado/restaurar último es global. El registro de inicio pertenece al adaptador Windows, no se activa por copiar un perfil.

La proyección SQLite activa usa containers/items/container_items/workspace_state. profiles archiva snapshots completos al cambiar; profile_state determina el activo. Guardar cada comando no necesita reserializar todos los perfiles. Duplicar captura la proyección actual, incluidos cambios ya confirmados. Enlaces a contenedores se resuelven siempre contra el espacio activo; no se mantienen objetos de otra biblioteca en ventanas cerradas.

WorkspaceWindowCoordinator detiene y guarda geometría antes del cambio; el guardado al cerrar se realiza antes de destruir el HWND, no desde un Closed tardío. Cierra únicamente ventanas que posee. Manager y hotkey sobreviven al cambio. Shutdown dispone bandeja, temporizadores, materiales y cache. El mutex por directorio rechaza una segunda instancia; datos aislados distintos pueden tener procesos diferentes, pero compiten por el atajo global (se informa el resultado).

La primera lectura se prepara fuera del hilo UI. Instrumentación registra hitos de arranque, HRESULT y nombres de métodos, sin contenido de archivos ni rutas de stack. El fallo nativo histórico de Fase 2 no se considera resuelto solo por varios arranques correctos.

## Recuperar un respaldo sin sobrescribir

1. Cerrar NexSpace mediante Salir y comprobar ausencia de NexSpace.UI.
2. Conservar la base actual intacta. Crear una carpeta nueva de recuperación.
3. Copiar únicamente un respaldo SQLite propio a esa carpeta con nombre nexspace.db. No copiar recursos referenciados ni usar una carpeta con base existente.
4. Lanzar unpackaged con NEXSPACE_DATA_DIR apuntando a esa carpeta. Revisar perfiles, referencias y preferencias. Si se rechaza/corrompe, no resetear ni reemplazar la base original.
5. Decidir manualmente qué biblioteca usar después de verificar la copia. No hay botón que sobrescriba silenciosamente datos activos.

La prueba automatizada verifica respaldos únicos/reabribles; el procedimiento humano completo de recuperación es una comprobación diferente.

Detalles y límites: [ADR-010](../decisions/ADR-010-profiles-themes-shell.md). Evidencias: [PHASE3_VALIDATION](../testing/PHASE3_VALIDATION.md).
