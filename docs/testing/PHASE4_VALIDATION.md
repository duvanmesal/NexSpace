# Fase 4 · Estabilización y preparación de distribución

Fecha: 2026-10-12. **En validación, no aceptada ni lista para lanzamiento 1.0.** Fase 3 aceptada condicionalmente; 0216de5/9521074 publicados por push normal tras auditoría y reverificación. Cambios de Fase 4 únicamente locales.

## Entorno y privacidad

Windows 11 x64 26100; SDK .NET 10.0.401/runtime 10.0.12; Windows App SDK 2.5.1; SQLite 10.0.12. Datos en `%TEMP%/NexSpace-Phase4-20261012/data`, copia de la biblioteca de demostración aislada de Fase 3. No se abrió la biblioteca personal ni se modificaron originales. Capturas, volcados, trazas y resultados: `docs/testing/evidence/private/phase4-20261012/`, ignorado por Git. Herramientas dotnet-trace/gcdump/dump 10.0.750501 instaladas solamente bajo `artifacts/phase4/tools`, sin instalación global.

DESKTOP sigue experimental y deshabilitado. No se ejecutaron HWND_BOTTOM, WorkerW, SetParent, cambios de Explorer/iconos, Developer Mode, certificados ni inicio con Windows.

## Resultados técnicos

- PASS: restore y reverificación final Debug/Release x64, cero errores y advertencias. 33 unitarias y 33 integración por configuración (132 ejecuciones PASS). Cuatro TRX privados de la revisión final.
- PASS: regresión de firma del dock: orientación, etiquetas y espaciado invalidan el layout; mover un contenedor o cambiar timestamps no lo reconstruye.
- PASS: fallo SQLite inducido por trigger en fixtures conserva perfil y contenedor activos y libera el archivo. No equivale a una prueba real de ACL denegada.
- PASS: cien cambios transaccionales entre perfiles con veinte contenedores, respaldo y restauración en otra base aislada. Es integración de persistencia, no cien cambios de UI.
- PASS: publicación autocontenida en carpeta tras corregir omisión de PRI/XBF. Cinco arranques reales, dos ciclos nativos por arranque, cierre normal y código 0; `portable-fixed-summary.json` y cinco informes individuales.
- FAIL anterior corregido: publicación inicial omitía recursos compilados. Cinco fallos con XamlParseException/802B000A y evento nativo C000027B; no usar `completed=true` del informe antiguo como evidencia de estos arranques fallidos. Es un archivo residual anterior. El harness ahora escribe estado inicial y progreso para evitar esa ambigüedad.
- PASS limitado: ensayo de diez ciclos nativos con cierre normal, código 0 y sesenta segundos de reposo. Incluye intervención del perfilador; no es una medición pasiva limpia.
- FAIL: ensayos de cien ciclos framework-dependent terminaron con 0xE0464645 en Microsoft.UI.Windowing.Core.dll, precedidos por OutOfMemoryException al abrir dock. Desuscribir callbacks, forzar GC solo como comparación y evitar material redundante no resolvieron esas repeticiones. No se ha demostrado la causa nativa exacta.
- FAIL repetido en carpeta autocontenida corregida: salida -532265403/0xE0464645. Última muestra en ciclo 70: WS 587.673.600 bytes (~560,45 MiB), privado 345.153.536 bytes (~329,16 MiB), 12.916 handles; inicio 2.098 handles. Informe final conserva completed=false y PID 12032. `portable-stress-progress.json` conserva tendencia cada diez ciclos. No se marcó PASS pese a las pruebas unitarias satisfactorias.
- PASS de generación, no instalación: MSIX x64 sin firma, 65.998.004 bytes en el primer ensayo; una advertencia por ausencia de mspdbcmf.exe y sin paquete de símbolos. Firma, instalación, actualización y retirada NOT_TESTED.
- CI ampliada: matriz Debug/Release, ejecución explícita de los dos proyectos de pruebas y validación de PRI/XBF publicados. Ejecución remota de esta nueva CI NOT_TESTED: Fase 4 no se publica.
- PASS: manifiesto SHA256 verificado de 539 archivos, 244.157.411 bytes (~232,85 MiB en disco), en `artifacts/phase4/manifest.json`. Este tamaño es distribución, no memoria residente. Artefactos privados; no instalación ni release.
- PASS final: candidata actualizada `artifacts/phase4/candidate/`, 539 archivos y 244.162.583 bytes; hashes comprobados en `candidate-manifest.json`. Arranque y dos cambios de perfil con comprobación explícita del número de ventanas restauradas, salida 0 (`final-candidate-smoke.json`). MSIX final sin firma: 65.998.909 bytes, misma advertencia de símbolos; no instalado.

## Recorrido visual de esta revisión

BLOCKED: el helper devolvió `foreground window did not report a process id` al capturar Manager y nuevamente tras reseleccionar/activar la ventana. Se detuvo la entrada para no reutilizar coordenadas. No hay capturas nuevas de Fase 4, ni PASS de contraste, foco, cuatro temas o tamaños mínimos basado en esta revisión. La evidencia visual auténtica de Fase 3 se conserva como antecedente, no se hace pasar por captura nueva.

El inventario sí devolvió Manager, NexDock, Estudio y Recursos en una ejecución normal Release autocontenida, PID 5292 al abrirla, sobre la biblioteca aislada con perfiles Programación/Universidad. Queda abierta sin harness para inspección del usuario; listar ventanas no prueba distribución o legibilidad. Explorer conserva PID 11128 e inicio 08:58:23 local, sin reinicio de esta ejecución.

La demostración abierta usa `artifacts/phase4/unpackaged` (anterior a las últimas ampliaciones diagnósticas/limpieza al cerrar); el artefacto actualizado y probado es `artifacts/phase4/candidate`. No se sustituyeron binarios de un proceso abierto. La UI de la demostración no acredita validación visual de los cambios finales.

Validación manual breve: desde Manager recorrer las siete secciones con Tab/Shift+Tab y Enter; cambiar los cuatro temas y revisar texto, botones de título y popovers; variar orientación/etiquetas/espaciado del dock sin cambiar borde; comprobar actualización inmediata; mover un contenedor con categoría desplegada y confirmar que no se reconstruye el menú. Revisar tamaños mínimos y regresar a dos perfiles de demostración. No repetir cien cambios hasta resolver el fallo de carga.

NOT_TESTED: incremento aislado de memoria por cada contenedor/categoría, primer frame realmente interactivo, soak de varias horas y análisis de retenciones nativas. Las muestras de ciclos combinan múltiples factores; no atribuirlas a un coste fijo por elemento.

Compatibilidad: PASS limitado en Windows 11 x64 26100 con runtime local/autocontenido y arranques breves; Windows 11 22000 es mínimo declarado, no probado aquí; ARM64/x86/Windows 10, entorno limpio y despliegue firmado NOT_TESTED. Sin certificación de accesibilidad, multimonitor ni integración de escritorio.

## Diagnóstico y rendimiento

La instrumentación registra etapas constructor/XAML/dispatcher, tipo/HRESULT/métodos, sin mensajes ni argumentos. `manager-dispatch-ready` mide dispatcher disponible, **no primer frame interactivo**.

Ensayo de diez ciclos: WS inicial 179,62 MiB, privado 106,66 MiB, 2148 handles; antes de reposo WS 269,13 MiB, privado 159,93 MiB y 3874 handles; después WS 320,13 MiB, privado 154,28 MiB y 3800 handles. CPU acumulada durante 60 s pasó de 9,015625 a 9,203125 s (~0,31% de un núcleo). Perfilado activo puede alterar WS/CPU; no comparar directamente con una ejecución sin instrumentar. Cambios de perfil: ~409–457 ms, temporización técnica hasta encolar/activar ventanas, no hasta comprobar interacción humana.

Gcdump: 3.163.780 bytes gestionados y 36.755 objetos; aparecían 22 ContainerWindow/11 DockWindow. SOS no encontró raíces para dos objetos antiguos examinados; el dock activo sí tenía raíces esperadas en el coordinador. Contar objetos del heap no demuestra retención viva. La advertencia de buffering lossy en .NET 10 limita el volcado. La traza EventPipe existe; topN muestra actividad ComWrappers, SQLite y JSON, no atribuye una fuga nativa.

No se afirma consumo menor de 150 MB ni ausencia de fugas. El histórico 0xC000027B de Fase 2 sigue sin causa demostrada: compartir código de excepción con el defecto de publicación actual no prueba identidad causal. [Microsoft explica las stowed exceptions y el análisis con estructuras y backtraces](https://learn.microsoft.com/en-us/shows/inside/c000027b). No se cambiaron claves WER ni se instaló WinDbg.

Comparaciones nativas adicionales (sin GC forzado): cien ventanas WinUI vacías completaron con código 0, handles 1983→2205 y WS 173.654.016→181.817.344 bytes. Cien docks sobre biblioteca vacía completaron con código 0, handles 1954→2364. Cien contenedores vacíos completaron con código 0 pero handles 1954→9570 y WS 176.336.896→451.776.512 bytes. Por tanto, PASS de completar esos bucles **no** es PASS de liberación/retención. La tendencia es mucho mayor en ContainerWindow; no se atribuye a referencias, thumbnails ni al número de perfiles porque estos ensayos no los usan.

Liberar contenido/región de título antes de destruir la ventana no bastó: otra repetición de cien contenedores terminó con 9487 handles. Se conserva limpieza explícita y se desactiva AllowDrop durante cierre; no se declara que esas medidas resuelvan el fallo. [SetTitleBar(null) es una operación documentada por Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar?tabs=winui3). Evidencias: `empty-windows.json`, `component-container.json`, `component-dock.json`, `container-preclose-unload.json`.

Otra comparación desactivó AllowDrop desde el constructor exclusivamente bajo opt-in diagnóstico: cien contenedores completaron con 9591 handles, desde 1979. No resuelve ni atribuye el crecimiento a recepción de drop. `container-without-drop.json` no valida el gesto físico. Se conserva la posibilidad diagnóstica, nunca habilitada en uso normal.

Los hashes SHA256 de los dos archivos fixture originales coinciden con los de Fase 3 (`fixture-integrity.json`). No se abrieron archivos personales ni se probaron operaciones físicas destructivas.

## Pendientes funcionales, visuales y accesibilidad

- BLOCKED: drag-and-drop físico Explorer→NexContainers. La API disponible solo expone drag dentro de una ventana; no acredita un gesto OLE entre HWND. Manual: arrastrar un archivo fixture desde Explorer, comprobar aparición de una referencia y hashes/original intactos; abrir, quitar referencia y volver a comprobar original.
- BLOCKED: menú de bandeja físico si la superficie del taskbar no es targetable. Manual: abrir menú, mostrar Manager/dock, cambiar perfil, recuperar, salir; confirmar retirada del icono y ausencia de proceso. No reiniciar Explorer.
- NOT_TESTED: inicio Windows reversible, por restricción expresa de no habilitarlo en pruebas. Revisar diálogo y adaptador no equivale a registro/arranque/desregistro real.
- NOT_TESTED: DPI/multimonitor y cambios de resolución. Manual: mover entre monitores con escalas diferentes, desplegar menú junto a cada borde y recuperar ventanas tras desconectar monitor; revertir configuración personalmente.
- NOT_TESTED: translucidez y animaciones activas. Windows las tenía deshabilitadas; no se cambió su configuración. Fallback sólido no acredita acrílico.
- NOT_TESTED: accesibilidad integral (Narrador, alto contraste del sistema y navegación completa solo teclado). Se añaden nombres de filas de contenedores/perfiles; inspección UIA y foco son cobertura parcial, no certificación.
- BLOCKED: auto-ocultamiento robusto no implementado; preferir dock visible recuperable a ocultamiento sin garantía. Pendiente mantenido desde Fase 3.
- NOT_TESTED: confirmaciones destructivas mediante UI; la skill computer-use exige confirmación en el momento de borrar, incluso con autorización previa. Se continúa con pruebas no destructivas sin solicitar esa operación. La eliminación de metadatos dispone de pruebas de servicio históricas, no sustituye este gesto.

## Checklist de aceptación 1.0

- [ ] Resolver el fallo de estrés nativo y repetir cien ciclos sin recuperación forzada.
- [ ] Completar soak prolongado y perfilado nativo/gestionado con límites de aceptación acordados.
- [ ] Verificar drag-and-drop físico y accesibilidad con usuario.
- [ ] Probar DPI/multimonitor, bandeja y recuperación en escenarios reales.
- [ ] Validar publicación en máquina limpia, firma autorizada, actualización y retirada conservando datos.
- [ ] Revisar todos los pendientes condicionados de Fases 1–3.
- [ ] Obtener aceptación humana final antes de publicar Fase 4 o una release.

Guías: `docs/phase-4/DISTRIBUCION.md`, `USO_Y_RECUPERACION.md`; arquitectura y decisiones en ADR-011. Las capturas conceptuales nunca sustituyen evidencia real.

## Git

Commit técnico local: `ce096d5`. Incluye regresión del dock, limpieza de suscripciones, diagnóstico, tests, recursos publicados y CI; **no** declara resuelto el fallo de estrés. Auditoría staged sin capturas, volcados, bases locales, artefactos generados ni patrones habituales de secretos. Fase 4 no se publica. Se conserva toda la historia de Fase 3 y las matrices condicionadas.
