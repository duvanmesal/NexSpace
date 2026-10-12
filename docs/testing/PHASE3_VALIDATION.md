# Fase 3 · Validación de perfiles, temas y Shell

Pruebas principales: 2026-10-11; consolidación final: 2026-10-12. Implementación entregada para revisión; no aceptación integral ni autorización de Fase 4. Fases 1 y 2 mantienen su aceptación condicionada y sus matrices históricas. FLOATING es la única presentación; DESKTOP experimental/deshabilitado. No se ejecutaron HWND_BOTTOM, SetParent/WorkerW, cambios de Explorer, iconos, Developer Mode ni inicio Windows.

## Entorno y evidencias

Windows 11 x64; .NET SDK 10.0.401; Windows App SDK 2.5.1; Microsoft.Data.Sqlite 10.0.12. Ejecución real Release unpackaged. Datos persistentes exclusivamente en `%TEMP%\NexSpace-Phase3-20261011\data`, fixtures en la carpeta hermana `fixtures`. No se abrió ni migró la biblioteca personal.

Capturas auténticas, árboles UIA, TRX, hashes, mediciones y registros privados: `docs/testing/evidence/private/phase3-astra-20261011/` (ignorado por Git). `trace.json` relaciona capturas, ventana y hora. Las imágenes son bytes originales del capturador, sin retoque; algunas tienen extensión .png aunque el formato entregado sea JPEG. No usar las referencias conceptuales como evidencia.

Dos perfiles de demostración: Programación y Universidad, cada uno con dos contenedores; inicialmente tres categorías y una cuarta categoría Medición añadida realmente en Programación. Parte de la biblioteca se preparó directamente como fixture SQLite aislado (`demo-seed.json`); **esa preparación no constituye prueba de CRUD mediante UI**. Se distinguen debajo las acciones reales realizadas con controles nativos.

## Automatización

- PASS: restore; builds Debug/Release x64, cero errores y advertencias en las ejecuciones satisfactorias. Build unpackaged Release adicional.
- PASS: **32 unitarias y 31 de integración por configuración Debug/Release**, cero fallos y omisiones (126 ejecuciones). TRX unit-Debug/Release e integration-Debug/Release privados. Incluyen pruebas previas de referencias, acceso denegado aislado, originales intactos, geometría, persistencia y carga.
- PASS técnico: CRUD de perfiles, duplicación independiente, eliminación rechazada para activo/predeterminado, nombres duplicados, preferencias globales, 30 ciclos de cambio con categorías enlazadas y orden.
- PASS técnico: destino corrupto/ausente revierte sin perder el activo; identidades compartidas incoherentes se rechazan antes de reemplazar datos.
- PASS técnico: migración v2 en copia aislada conserva IDs/referencias y genera backup previo reabrible todavía v2. Backups explícitos únicos y consistentes.
- PASS técnico: cuatro presets, parámetros inválidos/NaN, límites y fuentes permitidas; orientación explícita frente al borde, área negativa y DPI simulado.
- PASS técnico: Shell_NotifyIconW v4 sobre HWND aislado propio, callback y tres ciclos de Dispose. No equivale a inspección del menú físico en la barra de tareas.
- PASS técnico: rutas inválidas de inicio rechazadas y estado real de registro sin cambios. No se activó/desactivó inicio Windows para probar escritura.

## Recorridos funcionales y visuales reales

- PASS: arranque unpackaged, Manager accesible y navegación por sus siete áreas; creación real de contenedor Trabajo. Evidencias 01–02.
- PASS: renombrar perfil inicial a Programación, duplicarlo como Universidad, selección y cambio manual desde Manager. Se observaron la biblioteca restaurada, un Manager, un Dock y dos contenedores, sin duplicados. Evidencias 03 y 11. La independencia de cambios y la eliminación están cubiertas técnicamente; eliminación visual NOT_TESTED.
- PASS básico: Dark, Light, Contrast y Minimal son paletas distintas aplicadas en vivo. Light corregido muestra texto legible; Contrast y Minimal capturados en 16–17. No es auditoría WCAG/Narrador completa ni prueba de alto contraste del sistema.
- PASS: popover real de categoría con iconos Shell y referencias de archivo/carpeta/ausente; corregido el texto blanco sobre blanco. Evidencia 09.
- PASS: foco mediante teclado y activación del destino ausente muestran error controlado sin terminar el proceso. Evidencia 10. Aperturas de originales personales no realizadas.
- PASS: dock horizontal compacto sin barra de título administrativa, etiquetas opcionales, dock vertical configurado desde Preferencias; etiquetas normales completas tras corrección. Evidencias 07 y 14. Nombres extensos siguen truncándose con tooltip; no se afirma presentación ilimitada.
- PASS: botón Crear copia de respaldo SQLite produjo una ruta nueva y mantuvo la biblioteca; evidencia 15, más prueba automatizada de reapertura.
- PASS básico: cierres normales de procesos 20308 y 14928 devolvieron código 0 (JSON exit-cycle-final-1/2); el primero usó otra carpeta de prueba vacía por un error de parámetro, **no es prueba de restauración de la demo**. No se modificó la biblioteca demo en ese arranque.
- PASS: dos reinicios de la demo, primero Programación/Minimal y después Universidad/Light. Comparación SQLite antes/después idéntica en perfiles, activo/predeterminado, referencias/orden, geometría/colapso y workspace, excluyendo únicamente Revision. Evidencias restart-comparison.json y restart-university-comparison.json, capturas 18–20. Un Manager/un Dock/dos contenedores en ambos arranques. Proceso 27904 cerró con código 0 y sin residuos antes del siguiente lanzamiento; exit-cycle-final-3.json.
- PASS: instancia duplicada sobre la misma biblioteca salió con código 0; permaneció solo el proceso original (duplicate-instance.json).
- PASS: atajo global enviado desde Chrome recuperó las ventanas propias con failures=0 y global-hotkey registrados. No se manipuló la ventana de Chrome salvo dirigir el atajo autorizado.
- PASS: cerrar/reabrir Lectura desde biblioteca; añadir categoría Medición actualizó Manager y Dock; muestras incrementales del mismo proceso en PHASE3_PERFORMANCE.md.
- PASS de integridad: ambos archivos fixture conservan SHA256 inicial después de todos los cambios; source-hashes-before/after.json. Durante las pruebas del 11 de octubre Explorer mantuvo PID 22580 y hora de arranque original. Al reanudar el 12 de octubre la sesión ya tenía Explorer PID 11128, iniciado antes de NexSpace; el agente no lo reinició. Esto verifica continuidad durante cada recorrido, no una auditoría completa de Explorer.

## Fallos detectados y repetición

1. FAIL inicial: texto claro sobre superficies Light, incluidos popovers separados. Corrección: recursos semánticos compartidos, tema explícito y primer plano aplicado al árbol de controles. Repetido con Light real y popover: PASS (05/09).
2. FAIL inicial de esta fase: COM HRESULT 800F0902 al insertar alias de un mismo pincel en ResourceDictionary; stack instrumentado identifica Insert. Se eliminaron esos alias, conservando pinceles propios y mutación compartida. Arranques posteriores continuaron sin ese error. **No se identifica con esto la causa del fallo nativo histórico 0xC000027B de Fase 2**; ese diagnóstico sigue abierto.
3. FAIL inicial: geometría consultada demasiado tarde al cerrar HWND. Guardado explícito y AppWindow.Closing antes de destruir; Closed solo dispone. Cierres posteriores correctos; Shutdown continúa liberando ventanas aunque guardar sesión falle, registrándolo.
4. FAIL visual: Aplicaciones recortado en dock vertical. Ancho y alto de celda ajustados con etiquetas, hasta dos líneas. Repetido y capturado sin oclusión (14): PASS para nombres normales.
5. Una compilación encontró el ejecutable bloqueado por una instancia de pruebas aún abierta; se cerró normalmente y se recompiló satisfactoriamente. No se terminó ningún proceso de Windows.
6. FAIL visual final: botones de caption claros sobre contenedor Light. Se diferencia color de caption para contenedores Light y Manager con cabecera negra. PASS en Release reiniciado el 12 de octubre: botones minimizar/maximizar/cerrar oscuros legibles (22-light-caption-fixed-0.jpg y JSON asociado). Se restauraron Universidad, dock y dos contenedores; logs del nuevo arranque sin errores de tema ni excepciones no controladas.

## BLOCKED y NOT_TESTED conservados

- BLOCKED: gesto físico Explorer→NexSpace. La herramienta ofrece drag dentro de una sola ventana y no un arrastre entre HWND; Explorer/barra de tareas no aparecieron entre las superficies seleccionables. No se sustituyó por inyección Win32 ni por tests de ViewModel.
- BLOCKED: inspección/interacción física de menú de bandeja por la misma limitación de superficie; registro/callback/Dispose sí se probaron técnicamente. El Manager sigue disponible como alternativa.
- BLOCKED: auto-ocultamiento. Se conserva siempre visible; no se entrega un temporizador de ocultamiento sin validar foco y popovers.
- NOT_TESTED: registro y reversión reales del inicio Windows, por prohibición de activarlo en pruebas. Requiere distribución unpackaged en ruta estable; MSIX/StartupTask y firma pendientes.
- NOT_TESTED integral: accesibilidad con Narrador/alto contraste, DPI real y varios monitores, cambios de escala/resolución, efectos Acrylic/animaciones. Windows informó advancedEffects=False y animations=False; se verificó la alternativa sólida, no translucidez activa. No se cambiaron ajustes globales.
- NOT_TESTED: eliminación visual de perfiles, restauración humana completa desde backup y recuperación ante daño físico del almacenamiento. La cobertura transaccional automatizada no certifica estas interacciones.
- NOT_TESTED: soak prolongado, ETW/heap/GPU y ausencia de fugas. Ver PHASE3_PERFORMANCE.md.

## Guía manual breve

1. En la demo abierta, Espacios de trabajo → Universidad → Cambiar; comprobar dos contenedores, tema Light y dock horizontal. Volver a Programación; verificar nombres y referencias independientes. Solo un conjunto activo debe permanecer abierto.
2. Crear un espacio vacío; comprobar estado vacío; duplicar uno con contenido. Renombrar. Cambiar activo y predeterminado antes de eliminar la copia mediante confirmación. Los dos originales de fixtures deben seguir presentes.
3. Apariencia: probar las cuatro paletas, acento, radio, fuente y espaciado, pulsar Aplicar; revisar Manager, contenedores y popovers. Acrylic depende de Windows; no cambiar configuración global para forzarlo.
4. Preferencias: borde inferior/izquierdo, orientación horizontal/vertical, etiquetas y espaciado. Abrir categorías; comprobar foco visible, tooltip de nombres largos y comandos de recuperación.
5. Arrastrar desde Explorer solo `fixtures\Guia.txt` y carpeta Documentos; comprobar referencias añadidas y mismos hashes/originales. Quitar la referencia, no el original.
6. En el icono de bandeja comprobar Manager, ocultar/mostrar Dock, cambiar perfil, recuperar y Salir. Reabrir usando el comando aislado; comprobar sesión y ausencia de iconos duplicados. Ctrl+Alt+Shift+F12 debe funcionar desde otra aplicación.
7. Cerrar, comprobar ausencia de NexSpace.UI y reabrir dos veces con el mismo NEXSPACE_DATA_DIR. Revisar nombres, orden, geometría, colapso, tema y categorías. Explorer debe continuar funcionando.
8. Inicio Windows: no activarlo hasta autorizarlo y tener una ruta instalada estable. Recuperación: procedimiento en arquitectura, siempre sobre una carpeta nueva y conservando la base original.

## Reproducción técnica

```powershell
& "$env:USERPROFILE/.dotnet/dotnet.exe" restore NexSpace.sln -p:Platform=x64
& "$env:USERPROFILE/.dotnet/dotnet.exe" build NexSpace.sln -c Debug -p:Platform=x64 --no-restore
& "$env:USERPROFILE/.dotnet/dotnet.exe" build NexSpace.sln -c Release -p:Platform=x64 --no-restore
# Repetir ambos proyectos test en Debug y Release.
& "$env:USERPROFILE/.dotnet/dotnet.exe" test tests/NexSpace.UnitTests/NexSpace.UnitTests.csproj -c Release
& "$env:USERPROFILE/.dotnet/dotnet.exe" test tests/NexSpace.IntegrationTests/NexSpace.IntegrationTests.csproj -c Release
& "$env:USERPROFILE/.dotnet/dotnet.exe" build src/NexSpace.UI/NexSpace.UI.csproj -c Release -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
$env:NEXSPACE_DATA_DIR = "$env:TEMP/NexSpace-Phase3-20261011/data"
& ./src/NexSpace.UI/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/NexSpace.UI.exe
```

## Git y disposición

Fase 2: e1fc741 y 87716d1 auditados y publicados normalmente; origin/main confirmado en 87716d1274c4bb3304cd5b8afd8984cea86a1d07. Fase 3: commits solo locales, sin push. Capturas, TRX, logs, bases y backups excluidos. No se reescribió historial.

Commit local de implementación: `0216de5` (23 archivos de código/pruebas). Auditoría de nombres staged y patrones comunes de secretos sin hallazgos; diff --check correcto. Fetch final del 12 de octubre mantiene origin/main compatible, sin commits remotos exclusivos. La documentación se guarda en un segundo commit local. Release queda abierto con Universidad activo, Manager, dock y dos contenedores; Programación conserva su configuración independiente. Hashes finales de fixtures intactos. Capturas finales 22 (caption Light corregido) y 23 (Manager), con sus JSON asociados, siguen excluidas de Git.

Arquitectura/decisiones: PHASE3_PROFILES_THEMES.md y ADR-010. Lista para revisión condicionada, no certificada para distribución. No comenzar Fase 4.
