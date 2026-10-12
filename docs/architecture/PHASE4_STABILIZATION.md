# Arquitectura de estabilización · Fase 4

## Actualización Fase 4.1

ADR-012 y `docs/testing/PHASE41_PERFORMANCE.md` amplían el diagnóstico original. ContainerWindow utiliza caption del sistema y NexThemeManager deja los colores nativos a Windows. Container/Dock sustituyen DispatcherQueueTimer por DeferredAction cancelable, con invalidación de callbacks encolados al cerrar. Container retira handlers XAML, Closed, DataContext y Children al cierre; Dock retira su Closed nombrado además de AppWindow/Loaded/XamlRoot y flyouts.

DiagnosticProbe registra GDI/USER, privado/WS, generaciones GC y censo opt-in acotado de tipos de handles. Comprueba destrucción de HWND propios y presupuestos de seguridad. Los componentes se muestrean cada ciclo; perfiles/vacías cada diez, con muestras de reposo. `tools/Invoke-NativeResourceProbe.ps1` añade supervisor externo de su único proceso hijo. Las variantes no cambian el comportamiento normal sin opt-in y datos aislados.

El reproductor `tools/WinUiWindowLifetimeRepro` no referencia ningún módulo de producto. Reproduce incremento Composition/Event con Window sin Content: no permite atribuir ese residual a ViewModels, iconos, SQLite o coordinadores. No se fuerza liberación de recursos XAML sin ownership conocido. La recreación ilimitada sigue sin aprobación de estabilidad; no hay cambio de arquitectura a pooling ni publicación 1.0.

Las secciones siguientes conservan el contexto original de Fase 4; sus hipótesis pendientes se precisan en el informe 4.1, no sustituyen los resultados nuevos.

La dirección de dependencias no cambia: Domain independiente; Application contiene contratos y reglas; Infrastructure implementa SQLite/diagnóstico; Windows adapta Shell y monitores; UI es la raíz de composición; Presentation mantiene MVVM portable. Un único WorkspaceWindowCoordinator posee ventanas flotantes, restauración y cierre. No se crean aplicaciones independientes ni se habilita DESKTOP.

`DockLayoutSignature` limita reconstrucciones a cambios visuales o de referencias, excluyendo geometría y timestamps de contenedores. `NexThemeManager.Apply` evita recorridos repetidos de un tema idéntico, con aplicación forzada para contenido diferido. El registro de temas retira sus callbacks al cerrar la ventana. ContainerWindow/DockWindow desuscriben timers/AppWindow/XamlRoot y liberan contenido, items y backdrop.

El material se configura únicamente desde el tema, evitando crear acrílico en el constructor para destruirlo inmediatamente cuando Windows deshabilita efectos. Esto reduce trabajo innecesario; no resuelve por sí mismo el fallo de carga documentado.

`StartupDiagnostics` registra hitos y excepciones sanitizadas. `DiagnosticProbe` está fuera de la ruta normal salvo opt-in explícito; solo acepta datos temporales de Fase 4 y usa el mismo coordinador y servicios reales. `probe-result.json` empieza con completed=false y un PID; `probe-progress.json` conserva muestras cada diez ciclos. Correlacionar siempre PID, fechas, código de salida y eventos: un archivo antiguo no demuestra éxito del arranque actual. Completed significa finalizar el harness, no una certificación visual ni prueba de accesibilidad.

Variantes diagnósticas: `NEXSPACE_PROBE_EMPTY_WINDOWS=1` compara ventanas WinUI sin contenido de producto; `NEXSPACE_PROBE_COMPONENT=container` o `dock` aísla recreación sin cambios de perfil. `NEXSPACE_PROBE_DISABLE_DROP=1` desactiva recepción de drop solo cuando el harness se ha solicitado, para una comparación causal, no para validar drag-and-drop. En escenarios completos se comprueba que el número de ventanas restauradas coincide con la sesión solicitada; un fallo de apertura capturado por el coordinador no debe convertirse en PASS del harness.

No se cambia el esquema SQLite v3. Siguen vigentes respaldos previos a migración, transacciones y rechazo de formatos futuros/corruptos sin reset. Los nuevos tests inducen fallos mediante un trigger exclusivamente en una base fixture y restauran respaldos en otro archivo.

Publicación: el target `PublishCompiledWinUIResources` incluye el PRI generado por MSBuild como resources.pri y conserva las rutas relativas de XBF. Rechaza un PRI ausente. La CI comprueba presencia de recursos, pero esa comprobación no sustituye un arranque en Windows ni verifica máquina limpia. La generación MSIX sin firma es una capacidad separada de instalación/distribución aprobada.

Riesgo abierto: crecimiento de handles/recursos durante recreación repetida de ventanas. No se aplican GC forzados en producción, límites arbitrarios ni ocultamiento de fallos para presentar un PASS. La investigación siguiente debe aislar una reproducción mínima WinUI, comparar creación/cierre sin contenido, con backdrop y con tiles, y analizar recursos nativos con herramientas autorizadas. Conservar los fallos actuales como regresión antes de adoptar una actualización de SDK o estrategia de reutilización.
