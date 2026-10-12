# Integración futura de diseño · análisis de solo lectura

Fecha: 2026-10-12. **No se hizo merge, cherry-pick ni edición del worktree de Claude.** Se consultaron objetos Git del repositorio compartido. Main conserva la estabilización 4.1; `design/nexspace-ux-refresh` conservaba HEAD 1446c1a al inspeccionarlo.

## Cambios revisados y solapamientos

- `cb6beb4`: ContainerWindow.xaml.cs y NexThemeManager.cs. Coincide en eliminar caption personalizado y ocultar la fila de arrastre. Conflicto semántico principal: su base todavía usa DispatcherQueueTimer y cierre anterior. Conservar de main DeferredAction, limpieza completa, guardas diagnósticas y caption del sistema; no reemplazar el archivo completo por ninguna de las dos versiones.
- `790d24a`: ThemePreset.Auto, resolver, selector/ayuda y pruebas. El comportamiento de sistema aparece en NexThemeManager del conjunto de commits revisado. No está integrado en main. Revisar enum persistido, validación, snapshots anteriores, fallback y rollback de bibliotecas que contengan Auto.
- `9d627a5`: AccentPalette y RGB real de Windows con contraste; solapa NexThemeManager. Aplicar, si se autoriza, la derivación de color solo al contenido XAML, sin reintroducir escrituras de caption. Conservar pruebas de contraste y persistencia, distinguiéndolas de certificación de accesibilidad.
- `9227b09`: recursos NexGlass, layout del header, menús del dock y jerarquía de Manager. Los cambios de DockWindow están en contenido de flyouts, mientras main cambia debounce/cierre: integrar manualmente por responsabilidad, preservar ClearButtons y liberación de eventos. El header 36px requiere repetir contracción, mínimos, resize y captura; la fila DragRegion debe seguir colapsada.
- `1446c1a`: estados vacíos/selección/acciones y UpdateEmptyStates en MainPage. Menor solapamiento de líneas con 4.1; comprobar que refrescar/cambiar perfil no vuelva a construir ventanas o menús innecesariamente.

Riesgo de revisión, **no defecto reproducido en este trabajo**: ColorValuesChanged llega en hilo no UI y el código consultado enumera Targets.ToArray antes de encolar. La lista Targets también cambia durante Register/Closed en UI. Serializar lectura/aplicación en el dispatcher propietario, comprobar que la ventana sigue registrada y evitar callbacks que capturen ventanas cerradas. Definir ownership de la suscripción estática de UISettings y su retirada durante cierre de aplicación. Esto exige prueba de cambios de preferencia de Windows autorizados o eventos simulados, no alterar ajustes globales en esta investigación.

## Secuencia propuesta, pendiente de autorización

1. Partir del commit de estabilidad 4.1 y respaldar biblioteca de prueba; no integrar en caliente con una sesión personal abierta.
2. Resolver primero caption/ciclo de vida manteniendo las correcciones de main; incorporar recursos/layout sin reemplazar componentes completos.
3. Integrar Auto/AccentPalette juntos con sus tests y contrato de persistencia, eliminando el resolver de acentos obsoleto cuando corresponda.
4. Incorporar estados vacíos/selección; repetir recorrido de Manager, Dock, contenedores, cierre, perfiles, mínimos y hotkey.
5. Restore, Debug/Release x64, unitarias/integración y mismos ensayos nativos de 100/300. Comparar GDI/Timer/WaitCompletionPacket estables y residual Composition/Event contra 4.1; rechazar incremento nuevo o reaparición del crash.
6. Mantener pendiente el residual nativo: una mejora visual no habilita 1.0. Guardar nuevas capturas en evidencia privada y someter el conjunto a revisión antes de publicar.

No se autoriza esta integración mediante el presente documento. No se modifica el historial de ninguna rama.
