# Evidencia de validación funcional · Fase 0

**Fecha:** 2026-10-10
**Plataforma:** Windows 11 Pro x64, versión reportada por CIM `10.0.26300`, build `26300.9550` (el valor `ProductName` del registro aún dice “Windows 10 Pro”; se conserva la identificación por CIM/build).
**SDK/runtime:** .NET SDK `10.0.401`; .NET runtime `10.0.12`; Windows App SDK NuGet `2.5.1`; Windows App Runtime instalado `2.5.1.0` (x64/x86) y `1.8.0` `8000.994.2142.0`; Windows SDK target `10.0.26100.0`; BuildTools `10.0.28000.2705`; Git `2.47.1.windows.2`.
**Alcance:** evidencia técnica y manual reportada para P0-01 a P0-10. Ninguna prueba cambia iconos, archivos del usuario o el estado de Explorer.

## Evidencia manual reportada por el usuario

**Fuente:** reporte del usuario en esta conversación, 2026-10-10. No es captura ni observación independiente del agente. El usuario confirma: (1) el panel aparece correctamente; (2) puede moverlo y redimensionarlo; (3) `Ctrl+Alt+Shift+F12` funciona; (4) puede cerrar el panel; (5) Explorer continuó funcionando durante las interacciones realizadas. Esto verifica el uso básico de FLOATING y recuperación, pero no afirma que se haya probado modo DESKTOP, reinicio de Explorer, iconos, resolución/DPI ni cierre completo del proceso NexSpace.

## Construcción y pruebas automatizadas

| Comprobación | Estado | Evidencia |
|---|---|---|
| Restore | PASS | `dotnet restore NexSpace.sln -p:Platform=x64`; paquetes ya estaban restaurados. |
| Build Debug x64 | PASS | `dotnet build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64`; 0 errores, 0 advertencias. |
| Build Release x64 | PASS | `dotnet build NexSpace.sln --configuration Release -p:Platform=x64`; 0 errores, 0 advertencias. |
| Unit tests Release | PASS | 4/4; geometría visible y dos pruebas del watchdog (recuperación y reintento tras excepción). |
| Integration tests Release | PASS | 2/2; consulta Win32 de monitores/DPI y registro/desregistro/ruta del hotkey en HWND aislado. |
| Salida limpia de la app | PASS | Se lanzó la build unpackaged, se cerró por `CloseMainWindow()`/WM_CLOSE, el proceso terminó dentro del límite de espera y no quedó ningún `NexSpace.UI.exe`. Comprobación limitada a la app; no se cerró ni reinició Explorer. |
| Recuperación watchdog | PASS | 2/2 pruebas nuevas: recuperación en timer independiente del dispatcher, reintento tras fallo y captura de excepción; el timeout de producción es 15 s y el reintento es cada 1 s. |
| Cierre limpio de aplicación | PASS | Build unpackaged de verificación: PID 8580, título `NexSpace · Fase 0`; `CloseMainWindow()` aceptado, proceso terminó en menos de 2 s, no quedó PID ni proceso con esa ruta ejecutable. Log `application-closed` a `2026-10-10T10:12:43Z`. Se cerró sin panel abierto; no demuestra todavía cierre de consola con un panel abierto. |
| Atajo de emergencia | PASS (ruta de registro) | Prueba de integración registra `Ctrl+Alt+Shift+F12` sobre un HWND `STATIC` aislado sin foco, inyecta `WM_HOTKEY`, observa una llamada y verifica desregistro. La pulsación física global requiere confirmación manual. |
| Prueba controlada `HWND_BOTTOM` + rollback | PASS (mecanismo; no integración visual) | 2026-10-10, proceso unpackaged PID 17180: logó atajo activo y `recovery-watchdog-armed` antes del attach de una única ventana propia; a los 15 s logó `controlled-desktop-probe-pass`; el proceso seguía respondiendo. No se tocó ninguna otra ventana ni Explorer. No demuestra ubicación correcta detrás del escritorio/apps. |
| Inicio WinUI unpackaged | PASS | Compilación local con `-p:WindowsPackageType=None -p:EnableMsixTooling=false`, luego ejecución del EXE; proceso Windows observado como `Responding=True`, título `NexSpace · Fase 0`. No se fabricó captura. |
| Inicio empaquetado por `dotnet run` | BLOCKED | Windows App Development CLI 0.7.1 informa que Developer Mode no está habilitado. No se cambió el ajuste del sistema. |
| Ejecución directa del EXE de build empaquetado | FAIL (ruta no soportada) | Sin identidad de paquete terminó con `COMException 0x80040154` en `DeploymentManager.AutoInitialize`; se probó después la ruta unpackaged y arrancó. No es evidencia de fallo del flujo MSIX instalado. |

Comando alternativo local para abrir la build sin Developer Mode (modo de desarrollo unpackaged; no instala nada):

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build src\NexSpace.UI\NexSpace.UI.csproj --configuration Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
& ".\src\NexSpace.UI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\NexSpace.UI.exe"
```

Para `dotnet test`, no pasar `-p:Platform=x64`: los proyectos de prueba se producen bajo `bin\Release`, mientras que la solución WinUI necesita x64 para compilar.

## Matriz P0

“Completo / parcial / pendiente” expresa el grado de verificación del criterio; PASS/BLOCKED/NOT_TESTED conserva el estado de la evidencia técnica donde aplica. Las pruebas manuales marcadas como reporte del usuario no se presentan como automatizadas.

| ID | Grado | Evidencia y pendiente |
|---|---|---|
| P0-01 Arranque WinUI 3 | **Completo** | Prueba técnica unpackaged PASS: ventana WinUI arrancó y respondió. La vía empaquetada `dotnet run` queda BLOCKED por Developer Mode desactivado (no se activó). |
| P0-02 Ventana sin chrome convencional | **Parcial** | El usuario reporta que el panel aparece correctamente. Inspección específica de chrome personalizado/accesibilidad visual no queda descrita por separado ni tiene captura. |
| P0-03 Mover/redimensionar FLOATING | **Completo (básico)** | El usuario reporta movimiento y redimensionamiento funcionales. No cubre cambios de DPI o multimonitor (P0-08). |
| P0-04 Escritorio detrás de apps | **Pendiente** | No se repitió `HWND_BOTTOM`. La ejecución previa solo verificó la llamada controlada y su rollback; no prueba el criterio de no superponerse indebidamente a apps. La convivencia de Explorer reportada cubre solo las interacciones realizadas, no DESKTOP. |
| P0-05 Cambio de modo sin perder estado | **Parcial** | El hotkey global de recuperación está verificado por prueba automatizada, prueba controlada del watchdog y reporte manual del usuario. No se reporta un ciclo explícito Desktop→Floating conservando contenido/estado; DESKTOP sigue experimental. |
| P0-06 Explorer no bloqueado | **Parcial** | El usuario reporta que Explorer siguió funcionando durante las interacciones probadas. No se reporta prueba de menú contextual, iconos ni modo DESKTOP; no se reinició Explorer. |
| P0-07 Iconos originales | **Pendiente (capacidad bloqueada)** | Consulta de visibilidad no disponible; no se cambia ni oculta icono alguno. Requiere una estrategia soportada y autorización separada para cambios. |
| P0-08 Resolución/monitor | **Parcial** | Consulta Win32 de monitor primario, área de trabajo y DPI: integración PASS. No se probó mover entre monitores, cambios de escala/resolución ni desconexión de pantalla. |
| P0-09 Reinicio de Explorer | **Pendiente** | Hay detección de cambios en el conjunto de procesos y fallback implementado; no se reinició ni finalizó Explorer para validar recuperación. |
| P0-10 Salir/cerrar | **Parcial** | El usuario reporta cierre del panel. La app unpackaged también superó una prueba de cierre normal: proceso terminó y no quedó `NexSpace.UI.exe`. Falta confirmar manualmente cierre de consola con panel abierto y ausencia de ventana residual visible. |

## Recuperación verificada y procedimiento manual pendiente

La consola informa si `Ctrl+Alt+Shift+F12` se pudo registrar; si no, DESKTOP queda deshabilitado. El atajo llama a `RestoreAllFloating` sin depender del foco/visibilidad del panel. Cada transición experimental arma primero un watchdog nativo de timer a 15 s; si la restauración falla o lanza excepción, registra el error y reintenta cada segundo. El usuario reporta que el atajo físico funciona; además, la ruta de registro pasó integración y el watchdog pasó la prueba cronometrada real. El control de consola restaura todas las ventanas conocidas. Al cerrar, se desregistra el hotkey y se limpian timers.

1. P0-02: confirmar específicamente que no aparece chrome convencional no deseado y revisar teclado/contraste; no hace falta repetir movimiento/redimensionamiento básico ya reportado.
2. P0-05/P0-10: con contenido de demostración solamente, validar cierre de consola mientras hay panel abierto, reabrir y confirmar ausencia de ventanas residuales y hotkeys duplicados.
3. P0-04: no repetir en esta entrega; DESKTOP permanece experimental y el usuario no ha reportado validación visual de su relación Z con escritorio/apps.
4. P0-08/P0-09: pruebas en VM o sesión reversible para cambio de escala/resolución/monitores y reinicio de Explorer; confirmar fallback y logs sin terminar procesos por fuera de procedimiento.
5. P0-07: consulta de visibilidad se mantiene no disponible. No probar ocultamiento/restauración de iconos.

## Cierre de Fase 0

FLOATING superó las comprobaciones funcionales básicas reportadas por el usuario (presentación, mover/redimensionar, atajo y cierre de panel), además de builds, pruebas y cierre limpio del proceso. P0-02, P0-05, P0-06, P0-08 y P0-10 conservan alcance parcial; P0-04, P0-07 y P0-09 están pendientes. DESKTOP sigue experimental, no compatible ni estable. La [propuesta técnica de Fase 1](../phase-1/PROPUESTA_TECNICA_NEXCONTAINERS.md) es solo documental; no se inició ni implementó NexContainers.
