# NexSpace — Especificación funcional y técnica

> **Versión:** 0.4 (baseline de planificación)  
> **Estado:** aprobado funcionalmente; viabilidad técnica del modo integrado pendiente de prueba  
> **Plataforma inicial:** Windows 11 x64  
> **Destino:** repositorio del proyecto, para orientar la implementación asistida por Codex  
> **Regla de ejecución:** comenzar **solo con la Fase 0**. No implementar las fases posteriores hasta revisar las evidencias de viabilidad.

## 1. Resumen ejecutivo

**NexSpace** es un organizador híbrido, modular, ligero y visualmente personalizable para el escritorio de Windows. Su propósito es mantener los accesos a aplicaciones, juegos, documentos y carpetas organizados sin saturar el escritorio. Contará con:

1. **NexContainers:** paneles con elementos organizados; modo integrado en escritorio y modo flotante.
2. **NexDock:** dock horizontal o vertical con categorías y paneles emergentes.
3. **NexManager:** configuración, edición visual, perfiles y recuperación.
4. **NexCore:** lógica del dominio, integración Shell/Windows, almacenamiento, manejo de errores y seguridad.

No reemplaza Explorer, la barra de tareas ni Windows Shell. Las funciones principales no requieren cuenta, servicio remoto ni conexión a Internet. No incluye telemetría por defecto.

### Principios innegociables

- **No perder archivos.** Organizar y arrastrar a un contenedor agrega una **referencia** por defecto, sin mover ni copiar el original.
- **Sin modificaciones ocultas.** Copiar, mover, renombrar o eliminar archivos reales solo ocurre mediante acciones explícitas y confirmaciones adecuadas.
- **Reversibilidad.** Ocultar iconos originales será opcional y reversible; fallar o cerrar NexSpace no debe inutilizar Explorer.
- **Integración progresiva.** El modo integrado depende de una prueba de viabilidad. No afirmar compatibilidad hasta probarla.
- **Interfaz configurable.** El usuario puede optar por una presentación discreta, plegada o expansiva.
- **Rendimiento.** Cargar recursos de forma diferida; no escanear discos sin necesidad; minimizar CPU en reposo.

## 2. Decisiones oficiales y alcance

| ID | Decisión | Estado |
|---|---|---|
| DEC-01 | Sistema **híbrido**: contenedores integrados al escritorio y flotantes, seleccionables individualmente | Aprobada funcionalmente |
| DEC-02 | Se admiten archivos reales, carpetas, aplicaciones y accesos directos | Aprobada |
| DEC-03 | Arrastrar y soltar agrega **referencia** por defecto | Aprobada |
| DEC-04 | Ocultar iconos tradicionales de escritorio es **opcional**, reversible y global | Aprobada funcionalmente |
| DEC-05 | Perfiles con **cambio manual** (NexDock/NexManager/atajo) | Aprobada |
| DEC-06 | Windows 11 x64 primero; Windows 10 no es requisito de la v1 | Aprobada |
| DEC-07 | C# + .NET LTS compatible + WinUI 3/Windows App SDK; Win32/Windows Shell cuando corresponda | Propuesta técnica base, validar versiones concretas |
| DEC-08 | Arquitectura **monolito modular**, MVVM y servicios por interfaces | Aprobada como dirección |
| DEC-09 | SQLite local; JSON para importación/exportación/temas | Aprobada como dirección |
| DEC-10 | Método para integrar ventanas detrás de otras aplicaciones | **Pendiente de Fase 0** |
| DEC-11 | Windows se inicia normalmente aunque NexSpace falle | Obligatorio |

### No incluido en la primera versión

- Reemplazo de Explorer o de la barra de tareas.
- Cuentas de usuario, nube, marketplace de extensiones, sincronización multi-PC.
- Automatización de perfiles basada en aplicaciones abiertas (solo cambio manual).
- Widgets avanzados y extensiones (post-v1 o fase evolutiva).
- Soporte oficial de Windows 10.

## 3. Escenarios de uso

**U-01.** Abrir Windows y recuperar el perfil y la distribución guardados.  
**U-02.** Mantener carpetas/documentos en un panel a la izquierda, juegos arriba a la derecha y utilidades abajo a la derecha.  
**U-03.** Contraer paneles para mantener visible el fondo de pantalla.  
**U-04.** Abrir una categoría del dock, seleccionar una aplicación y lanzarla.  
**U-05.** Arrastrar `Trabajo.docx` a un contenedor sin alterar su ruta original.  
**U-06.** Mover un panel entre modo escritorio y modo flotante conservando contenido y apariencia.  
**U-07.** Ocultar/restaurar iconos originales sin modificar archivos.  
**U-08.** Cambiar manualmente entre perfiles Gaming, Desarrollo y Universidad.  
**U-09.** Desconectar un monitor y recuperar paneles que estuvieran fuera de pantalla.  
**U-10.** Cerrar/desinstalar NexSpace sin perder accesos ni archivos originales.

## 4. Requisitos funcionales

**Prioridades:** P0 crítica, P1 alta, P2 media, P3 futura. **Estado:** previsto; no implementado.

### 4.1 NexContainers

| ID | Prioridad | Requisito |
|---|---|---|
| RF-001 | P0 | Crear contenedor con identificador estable, nombre y perfil propietario. |
| RF-002 | P0 | Renombrar, editar y eliminar contenedor sin eliminar archivos reales. |
| RF-003 | P0 | Mover y redimensionar panel. |
| RF-004 | P1 | Contraer, expandir y opcionalmente ocultar panel. |
| RF-005 | P0 | Seleccionar por contenedor el modo `Desktop` o `Floating`, tras validar capacidades. |
| RF-006 | P1 | Vista en cuadrícula y lista. |
| RF-007 | P1 | Reordenar elementos manualmente y conservar el orden. |
| RF-008 | P2 | Bloquear posición. |
| RF-009 | P1 | Aspecto personalizable por contenedor. |
| RF-010 | P0 | Evitar paneles irrecuperables fuera de pantalla. |
| RF-011 | P1 | Contextual de contenedor: añadir, cambiar modo, personalizar, duplicar visualmente, quitar. |

### 4.2 Elementos, Shell y archivos

| ID | Prioridad | Requisito |
|---|---|---|
| RF-020 | P0 | Añadir archivo/carpeta/acceso/aplicación como referencia sin moverlo. |
| RF-021 | P0 | Resolver el destino y abrirlo con la acción adecuada del Shell. |
| RF-022 | P0 | Quitar una referencia sin borrar el archivo real. |
| RF-023 | P1 | Mostrar icono/nombre/estado y aviso si destino ya no existe. |
| RF-024 | P1 | Elemento compartido entre múltiples contenedores sin duplicar archivo. |
| RF-025 | P1 | Menú contextual: abrir, ubicación, renombrar **referencia**, quitar. |
| RF-026 | P1 | Acciones explícitas para copiar/mover/renombrar/eliminar **archivo real**. |
| RF-027 | P0 | Distinguir claramente `Quitar del contenedor` de `Eliminar archivo real`. |
| RF-028 | P1 | Gestionar errores, permisos, colisiones de nombre y operaciones canceladas. |
| RF-029 | P1 | Confirmación inequívoca y preferencia por Papelera cuando sea aplicable. |
| RF-030 | P1 | Arrastrar y soltar no inicia operaciones físicas implícitas. |

**Semántica de arrastrar:** por defecto se persiste una referencia. Copiar/mover físicamente solo desde menú/flujo independiente. Los datos del Shell pueden contener rutas, identificadores Shell y formatos de arrastre distintos; admitir primero un subconjunto seguro y ampliar por pruebas. No asumir que toda app instalada tenga un `.exe` accesible o que todo destino tenga ruta tradicional.

### 4.3 NexDock

| ID | Prioridad | Requisito |
|---|---|---|
| RF-040 | P1 | Dock horizontal/vertical en borde configurable. |
| RF-041 | P1 | Auto-ocultación opcional. |
| RF-042 | P1 | Categorías con panel emergente. |
| RF-043 | P1 | Categoría enlazable a un contenedor existente sin replicar su contenido. |
| RF-044 | P1 | Orden configurable, iconos, efectos y tamaños. |
| RF-045 | P2 | Indicador visual de selección y comportamiento de teclado accesible. |

### 4.4 NexManager, perfiles y temas

| ID | Prioridad | Requisito |
|---|---|---|
| RF-050 | P1 | Configurar paneles, dock, apariencia y preferencias globales. |
| RF-051 | P1 | Temas oscuro/claro y editor de opacidad, bordes, sombra, iconos y animaciones. |
| RF-052 | P1 | Aplicar preferencias visuales sin reiniciar cuando sea viable. |
| RF-053 | P1 | Crear, renombrar, duplicar y cambiar perfil **manualmente**. |
| RF-054 | P1 | Perfiles conservan sus contenedores, dock y ubicaciones. |
| RF-055 | P1 | Tray icon, abrir Manager y salir completamente. |
| RF-056 | P1 | Inicio automático opcional y con consentimiento, desactivable. |
| RF-057 | P1 | Ocultar/restaurar iconos originales mediante ajuste global reversible. |
| RF-058 | P1 | Exportar/importar ajustes; validación del archivo antes de aplicar. |
| RF-059 | P1 | Restablecer configuración sin tocar archivos reales. |

### 4.5 Persistencia y recuperación

| ID | Prioridad | Requisito |
|---|---|---|
| RF-060 | P0 | Guardar cambios de estado con transacciones y migraciones versionadas. |
| RF-061 | P0 | Restaurar el último perfil activo después de reiniciar NexSpace. |
| RF-062 | P0 | Manejar configuraciones inválidas sin impedir el inicio de Windows. |
| RF-063 | P1 | Backups automáticos y restauración de **configuración**. |
| RF-064 | P1 | Adaptar paneles a cambios de monitor, resolución y DPI. |
| RF-065 | P0 | Cierre completo y recuperación tras reinicio de Explorer. |

## 5. Requisitos no funcionales

- **RNF-01 — Compatibilidad:** Windows 11 x64 como plataforma de pruebas; otras plataformas no cubiertas inicialmente.
- **RNF-02 — Rendimiento:** objetivo orientativo menor a **150 MB RAM en reposo** para perfil pequeño y CPU ociosa; **no es garantía**. Medir en escenarios y ajustar tras prototipo.
- **RNF-03 — Arranque:** proceso no crítico del sistema; no debe impedir inicio de sesión ni Explorer. Autoarranque opcional.
- **RNF-04 — Estabilidad:** fallos aislados y registrados; controles de recuperación documentados.
- **RNF-05 — Privacidad:** todos los datos locales; registros no contienen contenido de archivos ni secretos.
- **RNF-06 — Seguridad:** sin privilegios administrativos para uso normal, salvo una necesidad justificada y aprobada.
- **RNF-07 — Accesibilidad:** foco visible, control por teclado, tamaños y contraste adecuados, respeto a reducción de animaciones.
- **RNF-08 — Pantallas:** gestionar escalado DPI, resolución y monitores múltiples sin perder paneles.
- **RNF-09 — Compatibilidad con aplicaciones:** no bloquear juegos de pantalla completa, menús del sistema ni entrada de otros programas.
- **RNF-10 — Mantenibilidad:** interfaces públicas y dependencias unidireccionales; pruebas unitarias sobre lógica crítica.
- **RNF-11 — Recuperación:** mecanismo documentado y probado para desactivar integración, mostrar iconos y restablecer posiciones.

## 6. Experiencia visual y comportamiento

### 6.1 Concepto híbrido (opción C)

- Paneles opcionales integrados en el fondo del escritorio.
- Dock flotante configurable, con categorías que abren popovers.
- El centro de pantalla queda libre si el usuario así lo desea.
- Los paneles tienen encabezado compacto, icono, título, controles de contraer y menú de personalización.
- Aspecto inicial **NexGlass**: superficies oscuras translúcidas, esquinas suaves, sombras discretas, iconografía coherente y animaciones moderadas.
- Tema alternativo **NexLight**; futuro soporte para temas propios.
- No mostrar controles innecesarios permanentemente; ofrecer estados hover/foco.

### 6.2 Estados de panel

- `Expanded`, `Collapsed`, `Hidden` (presentación).
- `Desktop`, `Floating` (integración).
- `Normal`, `AlwaysOnTop` solo para ventanas flotantes si es técnicamente viable y configurable.
- Los cambios de modo conservan contenido, identidad y opciones, pero las posiciones se almacenan **por modo**.

### 6.3 Acciones básicas

- Clic o doble clic para abrir elemento (establecer preferencia, inicial: doble clic para ítems de contenedor).
- Clic derecho: menú contextual propio, separado de acciones sobre archivo real.
- Arrastrar desde Explorer: crear referencia (nunca `Move` por defecto).
- Ordenamiento por drag-and-drop dentro de contenedor.
- `Esc` cierra popovers; el foco se devuelve al control invocador.
- Transiciones y efectos desactivables.

### 6.4 Iconos originales

La preferencia `hideDesktopIcons` es **global** y reversible. Registrar la preferencia del usuario y distinguirla del estado real observado de Explorer. No usar destrucción de vistas, borrado de `.lnk`, cambio de permisos, reemplazo de Explorer ni alteraciones ocultas del registro. Antes de activarla implementar una recuperación fácil y probada.

## 7. Arquitectura de software

### 7.1 Stack de referencia

- C# y versión de .NET LTS compatible con versión estable seleccionada de WinUI 3/Windows App SDK.
- WinUI 3 + XAML + MVVM (`CommunityToolkit.Mvvm` candidato).
- Win32/Windows Shell encapsulados detrás de interfaces de integración.
- SQLite mediante `Microsoft.Data.Sqlite` (candidato).
- `Microsoft.Extensions.Logging` y DI estándar.
- xUnit para pruebas unitarias y pruebas de integración Windows.

**Regla para Codex:** verificar en documentación oficial y en el entorno las versiones compatibles antes de fijarlas. No inventar paquetes, APIs o firmas.

### 7.2 Módulos

```text
NexSpace/
├── src/
│   ├── NexSpace.UI/                # WinUI 3, XAML, ViewModels, ventanas
│   ├── NexSpace.Application/       # casos de uso, orquestación, comandos
│   ├── NexSpace.Domain/            # entidades, valores, validaciones, contratos del dominio
│   ├── NexSpace.Infrastructure/    # SQLite, configuración, logging y backups
│   └── NexSpace.Windows/           # Win32, Shell, monitores, ventanas e iconos
├── tests/
│   ├── NexSpace.UnitTests/
│   └── NexSpace.IntegrationTests/
├── docs/
│   ├── requirements/
│   ├── architecture/
│   ├── decisions/
│   └── testing/
├── assets/
│   ├── icons/
│   └── themes/
└── NexSpace.sln
```

### 7.3 Regla de dependencias

- `Domain` no depende de Windows, UI ni almacenamiento.
- `Application` depende de `Domain` y contratos abstractos.
- `Infrastructure` implementa contratos de persistencia.
- `Windows` implementa contratos específicos del sistema operativo.
- `UI` consume casos de uso, modelos de presentación e interfaces; composición en raíz de aplicación.
- Sin dependencias circulares; sin lógica de Shell en ViewModels.
- Usar eventos/observables para cambios de contenedor, perfil y configuración donde sea necesario, evitando una infraestructura de mensajería excesiva.

### 7.4 Servicios previstos

`IWorkspaceService`, `IContainerService`, `IItemService`, `IDockService`, `IThemeService`, `ISettingsService`, `IWindowHost`, `IDesktopIntegrationService`, `IDesktopIconService`, `IDisplayService`, `IShellItemService`, `IFileOperationService`, `IRecoveryService`.

La Fase 0 solo requiere implementaciones mínimas de las interfaces asociadas a pruebas de ventanas, Explorer, pantallas y recuperación.

## 8. Modelo de datos (objetivo v1)

El esquema es **conceptual y sujeto a migraciones**; no crear todas las tablas en Fase 0. Los identificadores de entidades son UUID/GUID; fechas UTC ISO 8601; claves foráneas activadas.

| Tabla | Columnas principales | Reglas |
|---|---|---|
| `workspaces` | `id PK`, `name`, `is_active`, `created_at`, `updated_at` | Solo un activo; índice único parcial para `is_active=1` o estado activo centralizado en settings |
| `containers` | `id PK`, `workspace_id FK`, `name`, `display_mode`, `visibility_state`, `style_id`, `created_at`, `updated_at` | Eliminación solo quita configuración, no archivos |
| `items` | `id PK`, `kind`, `display_name`, `target_path`, `shell_identity`, `icon_override`, `created_at`, `updated_at` | Distinguir referencia de objeto físico; rutas pueden ser nulas |
| `container_items` | `container_id FK`, `item_id FK`, `sort_order` | PK compuesta; mismo `item` en múltiples contenedores |
| `placements` | `id PK`, `container_id FK`, `mode`, `monitor_key`, `x`, `y`, `width`, `height`, `last_seen_at` | UNIQUE(`container_id`, `mode`) como base; ajustar para multimonitor si procede |
| `docks` | `id PK`, `workspace_id FK`, `edge`, `auto_hide`, `style_id` | Inicialmente 1 dock por perfil |
| `dock_entries` | `id PK`, `dock_id FK`, `item_id FK NULL`, `container_id FK NULL`, `sort_order` | CHECK: exactamente uno de los destinos no nulo |
| `themes` | `id PK`, `name`, `definition_json`, `is_builtin` | Validar schema JSON antes de aplicar |
| `settings` | `key PK`, `value_json`, `updated_at` | Preferencias globales y estado de recuperación |
| `file_operations` | `id PK`, `operation_type`, `status`, `source`, `destination`, `created_at`, `finished_at` | Registro mínimo, no guardar contenido de archivos; definir política de retención |

### 8.1 Tipos y enums

- `ContainerDisplayMode`: `Desktop`, `Floating`.
- `ContainerVisibility`: `Expanded`, `Collapsed`, `Hidden`.
- `ItemKind`: `File`, `Folder`, `Shortcut`, `Application`, `ShellItem` (ajustable tras pruebas).
- `DockOrientation`: `Horizontal`, `Vertical`.
- `DockEdge`: `Top`, `Bottom`, `Left`, `Right`.
- `FileAction`: `Copy`, `Move`, `Rename`, `Delete`.
- `OperationStatus`: `Pending`, `Running`, `Succeeded`, `Failed`, `Canceled`.

### 8.2 Rutas y DPI

Persistir geometría de usuario en unidades lógicas (DIP); conversiones de pantalla y posicionamiento HWND se harán en adaptador Windows con escalado por monitor. No depender solo de nombres de pantalla; definir una clave robusta con fallback cuando cambie el hardware.

### 8.3 Persistencia Fase 0

Para la prueba técnica basta con configuración local versionada (`settings.json` seguro y atómico **o** SQLite mínimo), resultados de probes en archivos JSON y logs. Evitar construir el ORM/esquema final antes de validar viabilidad.

## 9. Contratos de servicios

> Fragmentos orientativos para precisar responsabilidades. **No asumir que son archivos compilables sin completar DTO, errores y dependencias**.

```csharp
public enum ContainerDisplayMode { Desktop, Floating }

public sealed record WindowHandle(Guid Id);

public sealed record WindowDescriptor(
    string Title,
    double Width,
    double Height,
    ContainerDisplayMode PreferredMode);

public sealed record WindowPlacement(
    Guid ContainerId,
    ContainerDisplayMode Mode,
    string MonitorKey,
    double X,
    double Y,
    double Width,
    double Height);

public interface IWindowHost
{
    Task<WindowHandle> CreateAsync(WindowDescriptor descriptor, CancellationToken ct = default);
    Task<OperationResult> SetModeAsync(WindowHandle handle, ContainerDisplayMode mode, CancellationToken ct = default);
    Task<OperationResult> SetPlacementAsync(WindowHandle handle, WindowPlacement placement, CancellationToken ct = default);
    Task<OperationResult> CloseAsync(WindowHandle handle, CancellationToken ct = default);
}

public interface IDesktopIntegrationService
{
    Task<DesktopCapabilities> ProbeAsync(CancellationToken ct = default);
    Task<IntegrationResult> AttachAsync(WindowHandle handle, CancellationToken ct = default);
    Task<IntegrationResult> DetachAsync(WindowHandle handle, CancellationToken ct = default);
}

public interface IDesktopIconService
{
    Task<bool?> GetVisibilityAsync(CancellationToken ct = default);
    Task<OperationResult> SetVisibilityAsync(bool visible, CancellationToken ct = default);
}

public interface IDisplayService
{
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default);
}

public interface IShellItemService
{
    Task<ShellItemResolution> ResolveAsync(string input, CancellationToken ct = default);
    Task<OperationResult> OpenAsync(string itemId, CancellationToken ct = default);
}

public interface IFileOperationService
{
    Task<FileOperationResult> ExecuteAsync(FileOperationRequest request, CancellationToken ct = default);
}
```

### 9.1 Contrato de resultados

Todos los adaptadores deben devolver resultados tipados cuando una operación pueda fallar de forma ordinaria: éxito/fallo, código estructurado, mensaje legible y causa técnica para logs. No capturar silenciosamente excepciones. Cancelación no es fallo. Validar handles inexistentes, destinos inválidos, permisos y estados no compatibles.

### 9.2 Política de capacidades

`DesktopCapabilities` deberá exponer al menos: modo flotante soportado, modo integrado soportado/experimental, ocultación de iconos soportada/experimental, límites conocidos, explicación y evidencia de prueba. La UI no habilitará una función experimental sin informar su estado.

## 10. Integración con Windows: riesgos y condiciones

### 10.1 Ventanas WinUI / Win32

WinUI 3 + `AppWindow` y `HWND` permiten crear y configurar ventanas. Un orden Z bajo mediante `SetWindowPos` no demuestra integración real con el escritorio: debe validarse contra Explorer y el comportamiento de iconos.

### 10.2 Ventana integrada

Evaluar mecanismos de escritorio **sin depender ciegamente de `WorkerW`, `Progman` u otras jerarquías internas no garantizadas**. Nunca cerrar, destruir ni sustituir ventanas de Explorer; no inyectar código en procesos ajenos. Si la implementación estable no se puede demostrar, registrar limitación y emplear el modo flotante como fallback.

### 10.3 Visibilidad de iconos

Usar preferentemente mecanismos soportados/documentados del Shell; si se necesita una alternativa, evaluar riesgo y reversibilidad. Probar consulta de estado, ocultar, volver a mostrar, reiniciar Explorer y cerrar NexSpace. No tratar `GetVisibility()` desconocido como `false`.

### 10.4 Shell y archivos

Distinguir `.lnk`, rutas locales, accesos a apps empaquetadas y objetos del namespace Shell. No asumir que todo es una ruta `C:\...`. Futuros movimientos/eliminaciones se canalizarán mediante APIs apropiadas del Shell (por ejemplo `IFileOperation` cuando aplique), manteniendo un flujo de consentimiento claro.

### 10.5 Seguridad operativa

Durante Fase 0 no borrar, mover, renombrar ni ocultar archivos reales. Probar ocultación de iconos solo con consentimiento explícito del operador y restauración garantizada. Preferir perfil/VM de pruebas para exploración de integración no documentada.

## 11. Fase 0 — Diseño de prueba de viabilidad (**única fase autorizada para comenzar**)

### 11.1 Objetivo

Demostrar, con pruebas y evidencia reproducible, qué métodos de integración en Windows 11 x64 funcionan de forma estable y cómo cambiaremos entre modo `Desktop` y `Floating` sin dañar el entorno.

### 11.2 Entregables

1. Solución .NET/WinUI compilable con estructura modular mínima y versiones justificadas.
2. Ventana de prueba con encabezado y controles: crear/cerrar, cambiar entre modos, mover, redimensionar.
3. Adaptador de ventanas e integración con Explorer; modo integrado sujeto a viabilidad.
4. Prueba reversible de visibilidad de iconos originales.
5. Registro de capacidades y matriz de pruebas con evidencias, límites y decisión de arquitectura.
6. Instrucciones de compilación, ejecución, recuperación y limpieza.

### 11.3 Pruebas y aceptación

| Caso | Procedimiento | Aceptación |
|---|---|---|
| P0-01 | Compilar y abrir ventana WinUI 3 | Build y arranque correctos |
| P0-02 | Crear ventana sin cromo convencional | Panel con cierre/arrastre accesible |
| P0-03 | Mover/redimensionar ventana flotante | Sin pérdida de interacción, coordenadas válidas |
| P0-04 | Cambiar a modo integrado | Si viable, detrás de ventanas normales y accesible desde escritorio; **si falla, no marcar completo** |
| P0-05 | Alternar modos repetidamente | Sin duplicación, cuelgues ni pérdida de estado |
| P0-06 | Operar Explorer e iconos | Clic, selección, contexto y barra de tareas sin bloqueos |
| P0-07 | Ocultar/restaurar iconos | Visibilidad reversible sin tocar archivos |
| P0-08 | Cambiar DPI/resolución | Ventana recuperable y geometría coherente |
| P0-09 | Reiniciar Explorer | Recuperación o degradación segura y documentada |
| P0-10 | Cerrar la aplicación | No queda una ventana fantasma ni bloqueo de Shell |
| P0-11 | Ejecutar aplicación o juego a pantalla completa | No aparece encima ni roba foco indebidamente |
| P0-12 | Simular excepción y estado inválido | Log, salida segura y mecanismo de recuperación |

**Criterio de salida de Fase 0:** P0-01, 02, 03, 05, 06, 08, 09, 10, 12 pasan; P0-04 debe tener evidencia de éxito o resolución documentada como **no viable por ahora**. P0-07 solo se considera aprobado después de confirmar restauración, no tras ocultar únicamente. P0-11 se verifica en un escenario seguro disponible.

### 11.4 Registro y evidencia

Guardar por caso: identificador, entorno (versión Windows y build, versión .NET/Windows App SDK, resolución, escala), fecha, pasos, resultado esperado, resultado observado, `PASS/FAIL/BLOCKED/NOT_TESTED`, captura opcional, log relevante y limitaciones. **No fabricar capturas ni marcar como probadas operaciones no ejecutadas.**

### 11.5 Fallback obligatorio

- Si el modo integrado es inestable, deshabilitarlo detrás de una bandera experimental y continuar con modo flotante.
- Si Explorer se reinicia, detectar que la capa de escritorio dejó de estar disponible y recuperar o desmontar limpiamente.
- Proporcionar una vía para salir de NexSpace y restaurar iconos tradicionales sin depender de un panel inaccesible.

## 12. Roadmap posterior (no ejecutar todavía)

### Fase 1 — NexContainers

Modelo de contenedores; referencias a archivos y carpetas; vistas; drag-and-drop de referencias; abrir destinos; SQLite; guardado/restauración; gestión de errores.

### Fase 2 — NexDock y NexManager

Dock, categorías enlazadas a contenedores, popovers, bandeja, inicio opcional, ajustes globales.

### Fase 3 — Personalización y experiencia completa

Editor visual, temas, perfiles manuales, multimonitor, recuperación robusta, operaciones **explícitas** de archivos reales, optimizaciones y accesibilidad.

### Fase 4 — Pruebas y lanzamiento 1.0

QA automatizada/manual, instalador, desinstalación segura, documentación, política de versión y release.

### Evolución futura

Widgets modulares, temas compartibles, extensiones, automatizaciones y evaluación de Windows 10. No incorporarlos sin aprobar una nueva planificación.

## 13. Plan de pruebas general y riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Jerarquía de ventanas Explorer cambia con actualizaciones | Alto | Fase 0, feature flag, fallback flotante |
| Ventanas roban foco o quedan encima de juegos | Alto | Pruebas de orden Z, foco y fullscreen |
| Usuario pierde acceso visual a iconos | Alto | Recuperación independiente, preferencia reversible |
| Drag-and-drop mueve un original por error | Crítico | Solo referencias por defecto, pruebas de invariantes |
| SQLite/configuración se corrompe | Alto | Transacciones, backups, recuperación y migraciones |
| Panel fuera de monitor | Medio/alto | Normalización de coordenadas, fallback a monitor principal |
| Alto consumo RAM por iconos y efectos | Medio | Virtualización, caching con límites, medición real |
| Permisos/archivos bloqueados | Alto | Errores claros, operaciones cancelables y sin efectos parciales silenciosos |

### Invariantes verificables

- Quitar una referencia jamás ejecuta eliminación del destino físico.
- Copiar/mover/borrar físico jamás se activa por drag-and-drop de referencia por defecto.
- Un cambio de presentación no modifica identidad ni relaciones del contenedor.
- Al cerrar NexSpace, Explorer sigue operativo.
- Cualquier preferencia que cambie visibilidad del escritorio tiene ruta documentada de reversión.
- Una prueba bloqueada no cuenta como aprobada.

## 14. Convenciones de desarrollo

- `PascalCase` para clases, interfaces con prefijo `I`; namespaces `NexSpace.*`.
- Código, identificadores y comentarios técnicos en inglés; documentación funcional y UI inicialmente en español (preparada para localización).
- No almacenar secretos en repositorio ni en logs.
- Nullable reference types activados, analizadores, `async` con `CancellationToken` en I/O.
- Separar DTO, dominio y ViewModels; no usar `HWND` como identificador de dominio persistido.
- Mantener PRs/cambios pequeños por propósito; incluir pruebas y actualizar documentación.
- Versionar migraciones y ADRs para decisiones de integración.
- Evitar dependencias de terceros innecesarias o sin mantenimiento.

## 15. Instrucciones operativas para Codex

**Rol:** actuar como ingeniero de escritorio Windows encargado de construir NexSpace a partir de esta especificación, priorizando estabilidad, reversibilidad y trazabilidad.

1. Leer íntegramente este documento y revisar la estructura existente del repositorio antes de modificarlo.
2. **No empezar con fases 1–4.** Proponer primero un plan compacto de Fase 0 y ejecutarlo incrementalmente.
3. Inspeccionar versiones instaladas de Visual Studio, .NET SDK, Windows App SDK y toolchain; fijar versiones compatibles con referencias oficiales. Informar dependencias faltantes.
4. Crear solución modular mínima. Compilar y ejecutar en Windows real si las herramientas dan acceso, sin asumir ejecución por tener compilación exitosa.
5. Implementar de forma mínima los contratos de ventana y Desktop Integration. No inventar métodos de WinUI/Win32; verificar firmas y compatibilidad.
6. Evaluar primero modo flotante documentado; después experimento integrado en un entorno reversible. Evitar APIs no documentadas como dependencia única.
7. No alterar datos reales del usuario. Para pruebas de iconos, pedir autorización antes de ocultarlos y demostrar cómo restaurarlos.
8. Escribir logs y reporte de pruebas; cada prueba debe indicar `PASS`, `FAIL`, `BLOCKED` o `NOT_TESTED` con evidencia.
9. Crear `docs/decisions/ADR-006-desktop-integration.md` con la estrategia, limitaciones y fallback.
10. Al concluir Fase 0, detenerse, resumir lo conseguido, lo pendiente y solicitar aprobación antes de pasar a Fase 1.

**Definición de hecho:** un archivo compilado sin comprobación real no equivale a una funcionalidad validada. Nunca declarar éxito sin pruebas pertinentes.

### Primer prompt sugerido para Codex

> Lee `NEXSPACE_ESPECIFICACION_COMPLETA.md` como fuente de verdad del proyecto. Quiero iniciar NexSpace desde cero, pero ejecuta **únicamente la Fase 0**. Primero inspecciona el entorno Windows y el repositorio, fija una combinación compatible de .NET + Windows App SDK/WinUI 3, y crea el esqueleto modular. Luego implementa el prototipo de ventana flotante y la investigación controlada del modo integrado. Documenta pruebas reales, capturas solo si se pueden tomar de verdad y el ADR de integración. No toques archivos personales ni cambies la visibilidad de iconos sin pedirme autorización. Si detectas una limitación o riesgo del Shell, para esa prueba y presenta alternativa. Al final informa PASS/FAIL/BLOCKED/NOT_TESTED y detente antes de Fase 1.

## 16. Pendientes de validación técnica

- Versiones concretas de runtime, SDK y empaquetado.
- Técnica exacta del modo de escritorio integrado; compatibilidad con builds de Windows 11.
- Estrategia de consulta y ajuste soportada de visibilidad de iconos.
- Distinción exacta de shell identities para apps empaquetadas y archivos tradicionales.
- Política final de restauración ante cierres inesperados y reinicios de Explorer.
- Modelo fino de DPI/coordenadas entre WinUI y Win32.
- Métricas reales de memoria, CPU y tiempos de arranque.

Estas incógnitas **no son errores de la especificación**: son hipótesis que se resolverán con resultados de la Fase 0.

---

**Estado final del documento:** planificación suficientemente detallada para iniciar el prototipo de Fase 0. No representa una certificación de compatibilidad ni una implementación terminada.
