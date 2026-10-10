# Arquitectura de NexContainers · Fase 1

## Dependencias

```text
NexSpace.UI ── Application ── Domain
      ├────── Presentation ─── Application
      ├────── Infrastructure ── Application
      └────── Windows ───────── Application
```

- **Domain:** agregados y reglas de nombres, destinos, vistas, acentos y geometría; no depende de WinUI, SQLite o Shell.
- **Application:** `ContainerService`, `IContainerRepository`, `IContainerItemPlatformService` y resultados de operación. Valida el tipo y la ruta antes de persistir.
- **Presentation:** proyecto .NET portable; `ContainerViewModel` expone nombre, estado, colección observable y comandos tipados de abrir/quitar/reordenar. `ContainerItemViewModel` conserva estado del destino y nombre accesible. No referencia WinUI, SQLite ni Shell; recibe contratos y registro opcional.
- **Infrastructure:** `SqliteContainerRepository`; SQLite local con esquema v1, transacciones para creación, referencias y borrado, claves foráneas y pooling desactivado para cierre/aislamiento predecibles.
- **Windows:** `WindowsContainerItemPlatformService`; consulta estado y delega apertura explícita al Shell. `WindowsReferenceFilePicker` devuelve rutas sin resolver accesos directos. No mueve ni copia recursos.
- **UI:** consola WinUI, una ventana top-level por contenedor, pickers asociados al HWND, drop de `StorageItems`, cuadrícula/lista, menús y estados.

El contenedor enlaza su DataContext/ItemsSource al ViewModel probado; los comandos contextuales de referencias pasan por esa capa. La ventana mantiene exclusivamente coordinación nativa, geometría, selectores/diálogos y llamada a servicios de presentación. La consola aún construye filas y coordina su biblioteca en code-behind: extraer su ViewModel queda como deuda acotada, no se afirma MVVM completo. Los eventos no incorporan operaciones físicas sobre originales.

## Esquema

- `containers`: identidad, nombre único insensible a mayúsculas, presentación y geometría.
- `items`: catálogo compartible de destino, tipo y fecha de adición.
- `container_items`: relación con nombre visible y orden propios por contenedor; cascadas solo dentro de la base local.

`target` contiene una ruta absoluta o un recurso de sistema allowlist. La clave normalizada evita duplicar el mismo destino. La eliminación limpia metadatos huérfanos de SQLite, nunca el archivo apuntado.

## Estados y seguridad

- Destino existente: disponible; inexistente o inválido: estado en la fila, sin bloquear el contenedor.
- Acceso denegado al consultar/abrir: mensaje controlado; errores Shell: resultado de operación y registro sin ruta de usuario.
- Drag-and-drop: solo `StorageItems`; se acepta una única operación `Copy` como señal de intercambio, nunca `Move`. Es una referencia, no una copia física. `ReferenceDropProcessor` procesa metadatos y clasifica archivos/carpetas/.lnk/.exe; no se ejecuta `StorageFile.CopyAsync`, `MoveAsync`, `DeleteAsync` ni APIs equivalentes. El UI muestra explícitamente que conserva originales. No se reporta operación Move al origen.
- La consulta usa atributos y una comprobación de acceso de solo lectura (handle de archivo sin leer contenido; enumerador de carpeta dispuesto). Esto permite detectar denegación que GetAttributes por sí solo no detectaba. Destinos remotos/proveedores cloud pueden bloquear o generar I/O: no se certificaron y el sondeo síncrono necesita evaluación antes de bibliotecas masivas.
- Posición inicial: usa monitor guardado o primario; `KeepFullyVisible` coloca toda la ventana dentro del área de trabajo. La política histórica `KeepVisible` conserva su comportamiento de agarre para otros clientes.
- Recursos del sistema permitidos: AppsFolder, Downloads, Desktop, This PC, Settings y Control Panel.

## Geometría y recuperación verificadas

- `ContainerGeometry` distingue alto expandido persistente de alto contraído visible (114 px); mínimos expandidos 320 × 220, máximo defensivo 16384 por dimensión antes de persistir. Contraer no destruye el tamaño expandido. La recuperación aplica también los límites del monitor disponible.
- Bloquear posición conserva coordenadas reales de AppWindow, pero permite tamaño. La recuperación explícita usa `SavePlacement(..., recoverPosition: true)` para normalizar coordenadas aun bloqueadas, sin desactivar el bloqueo.
- Ctrl+Alt+Shift+F12 y recuperación de consola actúan sobre ventanas propias, sin depender del foco del contenedor y sin integración DESKTOP. La recuperación conserva la contracción.
- Debounce de geometría de 350 ms más guardado al cerrar/cambiar presentación; resultado persistido actualiza el estado local. Refresco de ventana y evento hacia biblioteca mantienen nombre/conteos sincronizados. Una revisión de carga impide aplicar consultas de biblioteca antiguas.
- La biblioteca se carga al iniciar; las ventanas se abren desde sus filas. No se promete restauración automática de todas las ventanas abiertas.

## Selector y drop

El selector de archivos usa el diálogo nativo Explorer asociado al HWND propietario, multiselección y `OFN_NODEREFERENCELINKS`: devuelve el .lnk, no su destino. Utiliza FILEMUSTEXIST/PATHMUSTEXIST, NOCHANGEDIR y DONTADDTORECENT; libera el buffer en finally, distingue cancelación de error nativo. El selector de carpetas mantiene FolderPicker con filtro `*`. La semántica de estos indicadores se documenta en [OPENFILENAMEW de Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamew).

El drop toma deferral antes de leer StorageItems asíncronamente y lo completa en finally. Esto fortalece el ciclo de vida del evento, pero no sustituye una prueba real desde Explorer: la herramienta de interacción no pudo mantener accesible el destino al activar la fuente.

## Núcleo visual compartido · NexGlass

- `Resources/NexGlass.xaml`, fusionado desde App.xaml: paleta semántica, alto contraste mediante colores del sistema, radios, estilo de títulos/botones discretos/filas de biblioteca y ListViewItem, plantillas de cuadrícula/lista y referencia. No son imágenes estáticas.
- `Controls/ReferenceTile`: mismo control para cuadrícula vertical (icono/etiqueta) y lista horizontal, truncamiento, tooltip, estado ausente y nombre accesible. Los controles conservan interacción/foco/selección nativos de WinUI.
- `Visuals/NexGlassWindow` / `NexGlassBackdrop`: controlador DesktopAcrylicKind.Thin propiedad de cada ventana, configuración predeterminada de SystemBackdrop, tinte/luminosidad compartidos, alternativa sólida por política del sistema o fallo. No se fuerza transparencia ni activación permanente, ni se cambian ajustes de Windows. Se libera al desconectar/cerrar.
- `Visuals/ShellIconLoader`: thumbnails/iconos reales de StorageFile/StorageFolder, fallback Segoe Fluent por categoría; máximo 4 cargas simultáneas y 128 imágenes cacheadas. Streams se disponen, cargas tardías no actualizan tiles descargados, generación evita repoblar cache tras Shutdown.
- Transición de entrada discreta solo cuando `UISettings.AnimationsEnabled`; el encabezado mantiene la misma identidad al contraer. Menús de encabezado anclados debajo de sus botones. El contenido puede usar scroll en dimensiones pequeñas.

NexDock y NexManager podrán consumir estos recursos y el tile sin duplicar paleta/plantillas; no deben reutilizar `ContainerWindow` como clase base de negocio. Los futuros módulos proporcionarán su ViewModel y adaptarán sus acciones. **No se implementaron esos módulos.** Los iconos del catálogo siguen siendo detalles de UI/Shell, no de Domain.

La sesión actual tiene `AdvancedEffectsEnabled=False` y `AnimationsEnabled=False`: PASS para alternativa sólida y carga real de iconos; translucidez/animación habilitada NOT_TESTED. Alto contraste tiene recursos implementados, no prueba visual de cambio de ajustes. Documentación primaria: [materiales y políticas](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops), [thumbnails](https://learn.microsoft.com/en-us/windows/apps/develop/files/thumbnails).

## Cierre y recuperación de fallos

SQLite dispone conexiones también cuando abrir/validar esquema falla; pooling permanece desactivado. Un esquema futuro se rechaza sin resetear datos del usuario. La prueba que repara user_version actúa solo sobre una base temporal propia, no es una migración de producción. No se añade recuperación automática destructiva ni se afirma backup de bases corruptas.

Shutdown impide refrescos tardíos, detiene timers, cierra ventanas propias, descarga ItemsSource/backdrop y vacía cache de iconos. `Process` retornado por apertura Shell se dispone sin terminar la aplicación asociada. Diagnósticos fallidos no hacen fallar comandos. Suites prueban exclusividad del archivo SQLite tras éxito/fallo; la liberación WinUI se verifica además mediante salida real del proceso, no como prueba exhaustiva de fugas.

## Límites conocidos

Rutas absolutas no sobreviven automáticamente a mover/renombrar fuentes; no se resuelve identidad robusta ni metadatos avanzados de `.lnk`. Drop requiere verificación manual en Explorer; no se certificaron DPI/multimonitor, lector de pantalla, translucidez activa ni denegación visual. El control usa iconos reales donde Windows los proporciona y glifos donde no. El objetivo de memoria es orientativo y la observación de esta sesión lo supera; no se afirma rendimiento garantizado. NexDock, perfiles, agrupación avanzada, identidad por Shell item y DESKTOP están fuera de Fase 1.

La sesión nativa inicial y su consolidación de 2026-10-10 se registran por separado en `docs/testing/PHASE1_VALIDATION.md`; el conjunto no se deduce de los builds. Las pruebas técnicas cubren 12 contenedores/240 referencias/20 reaperturas de repositorio, no carga visual de 240 tiles.
