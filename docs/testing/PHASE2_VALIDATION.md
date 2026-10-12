# Validación de Fase 2 · NexDock + NexManager integrados

## Cierre condicionado · 2026-10-11

El usuario acepta Fase 2 y autoriza Fase 3, sin convertir sus pendientes en PASS. Reverificación previa: restore, Debug/Release x64 (0 advertencias/errores), 24 unitarias y 21 de integración Release PASS. Auditoría de los 24 archivos modificados: sin bases, capturas privadas, generados ni patrones de secretos. Fetch: 0 commits remotos exclusivos y 2 locales. Push normal de e1fc741 y 87716d1 completado; remoto comprobado en 87716d1274c4bb3304cd5b8afd8984cea86a1d07. Sin force ni reescritura. Los párrafos siguientes son evidencia histórica de Fase 2; conservar todos los límites.

Fecha: 2026-10-11. Estado: implementación funcional entregada para revisión; **no aceptación integral**. Fase 1 aceptada condicionalmente por el usuario. FLOATING es la única presentación; DESKTOP experimental/deshabilitado. No se inició Fase 3.

## Entorno y método

Windows 11 x64, .NET SDK 10.0.401, Windows App SDK 2.5.1, Microsoft.Data.Sqlite 10.0.12. Se ejecutaron ventanas WinUI reales unpackaged, con clics, menús, teclado, selector de archivos, arrastre de ventana y capturas originales mediante automatización nativa. No se usaron renders como evidencia ni se sustituyeron recorridos UI por tests.

Biblioteca aislada persistente: `%TEMP%\NexSpace-Phase2-20261011\data\nexspace.db`, mediante NEXSPACE_DATA_DIR. Fixtures exclusivamente en la carpeta hermana fixtures: Guia.txt y Documentos/Contenido.txt. No se abrió ni migró la biblioteca personal. Datos de prueba permanecen disponibles entre reinicios.

Evidencia **privada excluida de Git**: `docs/testing/evidence/private/phase2-astra-20261011/`. trace.json registra hora UTC, árbol accesible, ventana y nombres de PNG originales. Incluye capturas iniciales FAIL, correcciones/repeticiones, snapshots SQLite y hashes de fixtures. Algunas imágenes de aplicaciones asociadas/selectores contienen contexto personal incidental: **no publicarlas**. Los binlogs/TRX también permanecen privados.

## Automatizadas · PASS

- Restore solución x64: PASS.
- Solución Debug y Release x64: PASS, 0 errores y 0 advertencias en ambas.
- Release UI unpackaged explícito: PASS, 0 errores y 0 advertencias.
- Debug x64: 24 unitarias y 21 de integración PASS, 0 fallos/omitidas.
- Release x64: 24 unitarias y 21 de integración PASS, 0 fallos/omitidas.
- Resultados: automated/Unit-Debug.trx, Integration-Debug.trx, Unit-Release.trx, Integration-Release.trx; solution-Debug.binlog, solution-Release.binlog, unpackaged-Release.binlog.

Las suites preservan cobertura Fase 1 y agregan reglas de categorías/referencias/sesión, geometría DIP/área de trabajo/origen negativo/cinco anclajes, duplicados, reordenamiento, etiquetas, rechazo de recursos no permitidos, enlace vivo sin clonado, borrado solo de metadatos, 12 reaperturas del repositorio, migración v1→v2, comparación de revisiones obsoletas, JSON corrupto sin reset, liberación de conexiones y aislamiento de observadores/registros fallidos. Rutas ausentes y ACL de lectura denegada se prueban solo sobre fixtures aislados; la ACL se restaura. Estas pruebas **no** certifican gestos UI, cambios físicos DPI ni perfilado de memoria.

## Funcionales/visuales reales

- **P2-01 PASS:** Manager, dock y contenedores son ventanas de un único proceso. Crear dos contenedores desde Manager, abrir/restaurar ambos y conservar biblioteca común. Evidencias 01–05, 21, 35.
- **P2-02 PASS:** crear categoría propia, editar nombre y reordenar categorías con controles de Manager; orden reflejado en dock tras reinicio. Evidencias 03, 25, 26, 29. Cambio de glifo está implementado y validado por reglas, pero selección visual de todos los glifos NOT_TESTED.
- **P2-03 PASS:** añadir destino de archivo aislado y añadir segundo archivo mediante selector nativo; iconos/nombres reales en Manager. Renombrar etiqueta sin renombrar archivo y reordenar por menú contextual. Evidencias 04, 27, 28, 32.
- **P2-04 PASS:** abrir Guia.txt desde popover con teclado/Windows Shell; Bloc de notas mostró el contenido esperado (08, 19). Abrir carpeta de registros desde diagnóstico abrió Explorer sobre la carpeta aislada (17, 18). Aplicaciones ejecutables y .lnk en dock usan el adaptador común, pero su apertura específica desde dock queda NOT_TESTED en esta sesión; no extrapolar la evidencia histórica Fase 1.
- **P2-05 PASS:** enlazar contenedor desde menú de Manager, agregar AppsFolder en la ventana del contenedor y observar el contenido nuevo en dock/Manager sin copiar referencias ni reiniciar. Evidencias 09–12. Abrir AppsFolder por Shell desde esa categoría NOT_TESTED.
- **P2-06 PASS acotado:** popover de categoría muestra lista/iconos/administración sin recorte del alto compacto (07, 30). Tab+Return en Administrar selecciona la categoría correcta en Manager (31). Clic directo fuera del rectángulo del dock BLOCKED por límite de la herramienta, no fallo demostrado de NexSpace.
- **P2-07 PASS acotado:** dock inferior inicial y cambio real a borde izquierdo desde Preferencias, restaurado en Release (13–15, 29). Se corrigió superposición de título con botones nativos en orientación vertical. Posición libre/otros bordes se prueban por política pura; arrastre/persistencia libre real y otros bordes físicos NOT_TESTED.
- **P2-08 PASS:** Ctrl+Alt+Shift+F12 con Explorer como ventana de entrada recuperó ventanas propias, registro workspace-recovered failures=0 seguido de floating-emergency-restore global-hotkey (20). No se ejecutó HWND_BOTTOM. Recuperación de geometría realmente fuera de pantalla en Fase 2 NOT_TESTED; política pura y antecedente Fase 1 no sustituyen ese recorrido.
- **P2-09 PASS:** mover segundo contenedor por barra nativa, cambiar acento a azul, contraerlo y restaurarlo contraído/azul en otro arranque. SQLite conservó posición 420/220 frente al primer contenedor 120/100. Evidencias 22–24, 34. No se repiten aquí todas las combinaciones de edición Fase 1.
- **P2-10 PASS:** cerrar solamente dock mantiene Manager accesible; Mostrar dock crea de nuevo una sola ventana de dock, sin cerrar contenedores (33 e inventario posterior). No se crean aplicaciones independientes.
- **P2-11 PASS:** persistencia real entre arranques y cierres normales, incluidos dos ciclos Release; comparación de biblioteca, IDs, nombres, orden, referencias, geometría, estado contraído, acento, preferencias y sesión igual. Solo se excluyen revision y updated_utc de la comparación por ser contadores/marcas de actividad. Datos completos en persistence-final-baseline.json, persistence-release-cycle2.json y persistence-cycle2-result.json. Los hashes de originales coinciden antes/después de reinicios y renombrado de etiqueta; la seguridad del borrado de metadatos tiene además pruebas automatizadas.
- **P2-12 PASS:** cierre normal Debug PID 5148, Release PID 27712 y Release PID 18864: salida 0; comprobación posterior sin NexSpace.UI residual. Explorer mantuvo PID 22580 y hora de inicio 08:09:55 local durante los recorridos. No equivale a certificar toda estabilidad de Explorer.
- **P2-13 PASS acotado:** diagnóstico muestra esquema v2, proceso, atajo, registros y estado experimental; no se registra inicio Windows. La política rechaza ocultar simultáneamente ambas superficies al iniciar. Prueba adicional real: desactivar Mostrar NexManager, guardar y reiniciar mantuvo dock y dos contenedores visibles sin Manager; botón Manager del dock restauró la ventana (37–39). Preferencia restaurada a visible (40). Otras combinaciones de sesión desde UI NOT_TESTED (persistencia/reglas automatizadas PASS).
- **P2-14 PASS acotado:** NexGlass/ReferenceTile/iconografía compartida, distribución legible de tabs y dock en la resolución actual, estados vacíos reales. Windows informó advancedEffects=False y animations=False; fallback sólido comprobado. Translucidez/animaciones activas NOT_TESTED, sin cambiar ajustes globales.
- **P2-15 BLOCKED UI / PASS automatizado:** quitar referencia/categoría/contenedor implementado con confirmación y semántica de metadatos. No se ejecutó la confirmación destructiva mediante UI: la política de interacción exige confirmación puntual adicional; se omitió para continuar autónomamente. No declarar borrado visual PASS por los tests.
- **P2-16 NOT_TESTED UI / PASS automatizado:** mensajes por destino ausente/acceso denegado y corrupción sin reset probados en adaptador/ViewModel/repositorio, no todos los diálogos reales de dock. Carga masiva, cloud/remotos y recuperación de corrupción requieren ampliación.

## Fallos encontrados y correcciones

1. **FAIL inicial:** dos arranques Debug terminaron con fallo nativo antes de cargar la biblioteca; el primero devolvió 0xC000027B (PIDs 1960/28320). Se preparó primera apertura SQLite fuera del hilo WinUI y se protegió Loaded contra duplicación/cierre. Arranques posteriores Debug y Release respondieron y cerraron normalmente. No se afirma conocer la causa interna WinUI sin depuración nativa; conservar el riesgo de repetir arranque frío en otros equipos.
2. **FAIL inicial (06):** popover recortado al alto del dock. ShouldConstrainToRootBounds=false permite popover nativo independiente; recompilado/repetido (07, 30) PASS. El límite restante de clic de la herramienta se superó mediante navegación real de teclado, no invocando lógica de aplicación.
3. Ancho horizontal insuficiente generaba desplazamiento innecesario: cálculo de longitud ampliado; dock inicial volvió a mostrar controles. Título vertical superpuesto con botones de sistema: se oculta etiqueta de grip en vertical, se centra columna (29).
4. Administrar desde categoría solo activaba Manager: ahora selecciona tab y categoría correspondiente (31). Se impide ocultar Manager si el dock no pudo crearse. Los errores de navegación se registran sin mensaje/rutas de excepción.

## Pendientes conservados y límites deliberados

DnD físico Explorer→NexContainers **BLOCKED**: API disponible arrastra entre dos puntos dentro de una única ventana y rechaza destinos fuera de sus límites; no genera una secuencia global entre Explorer y WinUI. El procesamiento de datos/referencias tiene pruebas PASS, pero no sustituye el gesto. No se implementa drop directo en el dock en esta entrega: usar Manager/selector o contenedor enlazado.

Accesibilidad integral/Narrador/alto contraste, DPI/multimonitor real, translucidez activa, animaciones y perfilado prolongado de memoria **NOT_TESTED**. El presupuesto orientativo de 150 MB no está demostrado y las mediciones de Fase 1 lo excedían; no declarar cumplimiento. Auto-ocultamiento opcional deliberadamente no ofrecido. Inicio automático Windows no registrado. Manager no persiste geometría; preferencias de sesión sí. Sin perfiles múltiples, editor avanzado, bandeja ni Fase 3.

## Guía manual breve

1. En la demostración aislada, usar Documentos en dock, abrir archivo, Tab a Administrar y comprobar categoría seleccionada. Editar nombre/icono, ordenar referencias y categorías; cerrar/reabrir NexSpace dos veces y comprobar valores.
2. Crear un archivo nuevo exclusivamente en fixtures. Arrastrarlo físicamente desde Explorer al contenedor; esperar referencia sin desaparición/movimiento/copia del original. Abrirla, renombrar etiqueta y quitarla con confirmación; comparar hash y ubicación del archivo. Registrar PASS/FAIL con evidencia privada.
3. Quitar una categoría enlazada tras confirmar; contenedor y originales deben seguir. Quitar un contenedor de prueba; enlace restante debe quedar vacío. Cancelar primero cada confirmación y comprobar ausencia de cambios.
4. Probar carpeta y .lnk aislados desde dock; para referencia ausente, añadir una ruta inexistente dentro de fixtures y abrir: mensaje controlado, UI operativa. No alterar ACL personales.
5. Preferencias: probar cinco anclajes, libre/arrastre, tamaño 32/64 y acentos; verificar popup/tooltip y restauración con Ctrl+Alt+Shift+F12. Cambiar DPI/monitor solo manualmente con posibilidad de reversión; no hacerlo como requisito para abrir la demo.
6. Revisar teclado, foco, Narrador, contraste y efectos en un entorno autorizado. Para memoria usar medición prolongada tras múltiples abrir/cerrar/popup; no confundir un único WorkingSet con perfilado.

## Repetición unpackaged

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" restore NexSpace.sln -p:Platform=x64
& "$env:USERPROFILE\.dotnet\dotnet.exe" build NexSpace.sln -c Debug -p:Platform=x64 --no-restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" build NexSpace.sln -c Release -p:Platform=x64 --no-restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests/NexSpace.UnitTests/NexSpace.UnitTests.csproj -c Release -p:Platform=x64
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests/NexSpace.IntegrationTests/NexSpace.IntegrationTests.csproj -c Release -p:Platform=x64
& "$env:USERPROFILE\.dotnet\dotnet.exe" build src/NexSpace.UI/NexSpace.UI.csproj -c Release -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
$env:NEXSPACE_DATA_DIR = Join-Path $env:TEMP 'NexSpace-Phase2-20261011/data'
Start-Process -FilePath '.\src\NexSpace.UI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\NexSpace.UI.exe' -WindowStyle Hidden
```

No lanzar una segunda instancia mientras la demo siga abierta. No activar Developer Mode. Mantener evidencia/datos fuera de commits. Git: Fase 1 publicada por push normal hasta 8f7b873; implementación Fase 2 en commit local e1fc741, documentación en commit posterior, sin publicar hasta revisión. No se ejecutó integración DESKTOP, no se reinició Explorer ni se cambiaron opciones/iconos o inicio Windows.

Demostración final Release: PID 28540, cuatro ventanas del mismo proceso (Manager, Dock, Trabajo y Ideas). Mostrar NexManager volvió a quedar activado. final-result.json y final-demo-state.json conservan resultado de comparación tras restaurar la preferencia. La ventana Ideas permanece contraída y azul para inspeccionar restauración; puede expandirse con su botón de encabezado.
