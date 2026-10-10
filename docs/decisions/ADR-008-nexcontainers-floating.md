# ADR-008 · NexContainers sobre ventanas FLOATING

- **Estado:** aceptada para Fase 1.
- **Fecha:** 2026-10-10.
- **Contexto:** Fase 0 validó funcionalidad básica FLOATING; DESKTOP continúa experimental y no es requisito.

## Decisión

NexContainers se presenta como ventanas WinUI 3 independientes en modo FLOATING. Cada contenedor conserva identidad GUID, orden, preferencias visuales y geometría; al iniciar, su ventana se limita al área de trabajo de un monitor disponible. No se invoca `IWindowHost.SetMode(Desktop)`, `HWND_BOTTOM`, `SetParent`, `WorkerW` ni otra operación de integración con Explorer para abrir contenedores.

La persistencia local usa `Microsoft.Data.Sqlite` 10.0.12 y un esquema SQLite versionado mediante `PRAGMA user_version`. El catálogo de destinos guarda metadatos, no contenido. Los recursos se comparten por clave de destino, mientras que nombre visible y orden viven en la relación contenedor-referencia para permitir presentaciones independientes.

Agregar por selector o drag-and-drop crea una referencia. El drop acepta solamente elementos de almacenamiento y solicita una única operación `Copy` como señal de intercambio (no Move), pero NexSpace no copia, mueve ni borra el destino. El texto de intercambio aclara “referencias; conservar originales”. `ReferenceDropProcessor` procesa metadatos, y no se reporta Move al origen. Quitar una referencia elimina su relación; solo borra metadatos huérfanos dentro de SQLite. La acción explícita Abrir delega a Shell mediante `UseShellExecute`; recursos del sistema se restringen a una lista fija de destinos conocidos. Recursos ausentes, no válidos o sin acceso producen estados legibles y no detienen la aplicación.

## Consecuencias

- FLOATING sigue siendo la única presentación activa para NexContainers.
- DESKTOP queda desacoplado, experimental y fuera de los criterios de aceptación de Fase 1.
- Las rutas pueden quedar rotas al mover o renombrar archivos externamente; NexSpace permite quitar o volver a crear la referencia, pero todavía no ofrece relocalización guiada ni identidad resistente a movimientos.
- La persistencia es local a `%LOCALAPPDATA%\NexSpace\data`; la aplicación permite `NEXSPACE_DATA_DIR` para pruebas aisladas.
- No hay operaciones físicas sobre recursos ni telemetría remota.
- El cierre de una ventana no elimina el contenedor ni sus referencias.

## Verificación y límites

Las pruebas de integración ejecutan SQLite en un directorio temporal, verifican compartir/ordenar/quitar y comparan el contenido del archivo original antes y después. Consolidación del 2026-10-10: restore y builds Debug/Release x64 pasan sin advertencias; **15 unitarias y 15 de integración por configuración**. Se añadieron payload real WinRT StorageItems incluso con RequestedOperation.Move (procesamiento solo de referencias), duplicados/mezclas/extensiones mayúsculas, comandos ViewModel, diagnóstico fallido, permisos reales denegados sobre un archivo propio, fallo/reapertura de esquema y carga de 12 contenedores/240 referencias/20 reaperturas. Esto último no es estrés visual ni sustituto del gesto Explorer→WinUI.

Además hubo interacción nativa real unpackaged inicial y consolidada: varias ventanas, selectores/aperturas, contracción, bloqueo, referencias, reinicios, recuperación y cierre sin procesos residuales. Las capturas auténticas privadas y el alcance exacto de cada recorrido constan en `docs/testing/PHASE1_VALIDATION.md`.

Drag-and-drop desde Explorer queda BLOCKED: la API de automatización expone un solo HWND por drag, activa la fuente antes del gesto y no ofrece transferencia del puntero entre ventanas. El intento inicial no alcanzó NexSpace; no demuestra un defecto del producto ni un PASS. Denegación real pasa en integración aislada, pero su mensaje visual, DPI/multimonitor y accesibilidad integral siguen sin certificación. No se declara Fase 1 aceptada integralmente.

Para preservar referencias .lnk se adopta selector nativo con OFN_NODEREFERENCELINKS, multiselección y propietario HWND; la prueba real distinguió enlace de destino y abrió el recurso esperado. Véase [documentación oficial OPENFILENAMEW](https://learn.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamew). La geometría contraída conserva su alto expandido; el bloqueo no impide recuperación explícita completamente visible. Estos ajustes no cambian la decisión FLOATING ni habilitan DESKTOP.

Como antecedente, iniciar directamente el EXE empaquetado sin identidad falló en el auto-inicializador del Windows App SDK; el comando unpackaged explícito documentado pasó. No se activó Developer Mode.

## Consolidación visual y reutilización

Se adopta NexGlass como núcleo de presentación WinUI compartido, no como módulo de Fase 2: diccionario semántico `Resources/NexGlass.xaml`, plantillas de grid/lista, `ReferenceTile`, cargador acotado de iconos del Shell y backdrop por ventana. Tipografía Segoe UI Variable, bordes/radios coherentes, etiquetas bajo iconos en cuadrícula, menú de encabezado debajo del botón y botón directo de contracción acercan el producto al concepto C sin simular controles con imágenes.

Se solicita **DesktopAcrylicKind.Thin** mediante SystemBackdrop/Windows App SDK y se conserva su configuración predeterminada y política de Windows. Si no está disponible o falla, se aplica superficie sólida; el controlador se dispone al cerrar. No se fuerza transparencia, no se cambia la política del equipo y no se depende de Explorer/WorkerW. En la sesión observada `AdvancedEffectsEnabled=False`, `AnimationsEnabled=False`: la alternativa sólida e iconos reales fueron comprobados; translucidez/animación activas quedan NOT_TESTED. Recursos de alto contraste implementados, no certificados visualmente. Véase [guía oficial de materiales](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops).

`NexSpace.Presentation` aporta estado observable y comandos de referencias, independiente de WinUI/SQLite/Shell. La UI enlaza el ViewModel y conserva wiring nativo de ventana/pickers/diálogos. La biblioteca principal todavía tiene coordinación en code-behind; no se afirma MVVM completo. Reutilización y límites en `docs/architecture/PHASE1_CONTAINERS.md`. Los futuros NexDock/NexManager pueden consumir tokens/tiles sin duplicarlos, pero **no se implementan en esta fase**.

La carga de iconos limita concurrencia/cache y dispone streams/cancela actualizaciones tardías. Se corrige disposición de conexiones SQLite ante fallo y de objetos Process retornados por Shell, sin terminar apps asociadas. El sondeo de acceso de solo lectura corrige falsos “disponible” al denegar lectura. No se certifican proveedores remotos/cloud ni uso sostenido; la medición local de memoria supera el objetivo orientativo de 150 MB de RNF-02.

## Alternativas descartadas

- **Embebido en el escritorio:** no se necesita para Fase 1 y depende de hipótesis no certificadas de z-order/Explorer.
- **Copiar el destino a la carpeta de NexSpace:** contradice el modelo de referencias y duplica datos.
- **Guardar una fila independiente por contenedor sin identidad compartible:** duplica metadatos y complica referencias compartidas; se adopta catálogo de recurso + relación individual.
