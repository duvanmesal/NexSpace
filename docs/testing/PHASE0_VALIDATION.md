# Evidencia de validación funcional · Fase 0

**Fecha:** 2026-10-10
**Plataforma:** Windows 11 Pro x64, versión reportada por CIM `10.0.26300`, build `26300.9550` (el valor `ProductName` del registro aún dice “Windows 10 Pro”; se conserva la identificación por CIM/build).
**SDK/runtime:** .NET SDK `10.0.401`; .NET runtime `10.0.12`; Windows App SDK NuGet `2.5.1`; Windows App Runtime instalado `2.5.1.0` (x64/x86) y `1.8.0` `8000.994.2142.0`; Windows SDK target `10.0.26100.0`; BuildTools `10.0.28000.2705`; Git `2.47.1.windows.2`.
**Alcance:** implementación y evidencia de P0-01 a P0-10. Ninguna prueba cambia iconos, archivos del usuario o el estado de Explorer.

## Construcción y pruebas automatizadas

| Comprobación | Estado | Evidencia |
|---|---|---|
| Restore | PASS | `dotnet restore NexSpace.sln -p:Platform=x64`; paquetes ya estaban restaurados. |
| Build Debug x64 | PASS | `dotnet build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64`; 0 errores, 0 advertencias. |
| Build Release x64 | PASS | `dotnet build NexSpace.sln --configuration Release -p:Platform=x64`; 0 errores, 0 advertencias. |
| Unit tests Release | PASS | 4/4; geometría visible y dos pruebas del watchdog (recuperación y reintento tras excepción). |
| Integration tests Release | PASS | 2/2; consulta Win32 de monitores/DPI y registro/desregistro/ruta del hotkey en HWND aislado. |
| Recuperación watchdog | PASS | 2/2 pruebas nuevas: recuperación en timer independiente del dispatcher, reintento tras fallo y captura de excepción; el timeout de producción es 15 s y el reintento es cada 1 s. |
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

| ID | Estado | Evidencia observada y pendiente |
|---|---|---|
| P0-01 Arranque WinUI | PASS (unpackaged) | Ventana WinUI creada; proceso responde y reporta el título. Ruta MSIX de desarrollo bloqueada por Developer Mode. |
| P0-02 Ventana sin chrome convencional | NOT_TESTED | Panel implementado con barra personalizada WinUI. La prueba controlada creó la ventana, pero no se realizó inspección visual/captura verificable de su chrome. |
| P0-03 Mover/redimensionar FLOATING | NOT_TESTED | AppWindow usa ventana top-level y hay restauración segura; falta manipulación visual real. |
| P0-04 Modo escritorio detrás de apps | NOT_TESTED | Se ejecutó la llamada `HWND_BOTTOM` solo sobre ventana NexSpace y se verificó rollback automático. Queda sin probar que quede correctamente detrás del escritorio/apps, mantenga foco/iconos y sea estable en uso real; no se acredita integración. |
| P0-05 Cambio repetido conservando estado | NOT_TESTED | Se verificó una transición Desktop y vuelta automática en el mismo HWND; falta probar ciclo repetido y persistencia/experiencia visual. |
| P0-06 Explorer y sus iconos | NOT_TESTED | Enumeración de proceso Explorer solo lectura y fallback implementados. No se hizo clic/contexto ni se comprobó el Shell en pantalla. No se reinició Explorer. |
| P0-07 Consulta de visibilidad de iconos | BLOCKED | El adaptador declara consulta no disponible; no se usa jerarquía interna de Explorer como consulta supuestamente soportada. No se modificó visibilidad. |
| P0-08 DPI/resolución/monitores | NOT_TESTED (consulta técnica PASS) | Consulta Win32 de monitores, área útil y DPI pasa en integración. No se alteró resolución, escala ni configuración multimonitor; comportamiento visual ante esos cambios requiere prueba manual. |
| P0-09 Reinicio de Explorer | NOT_TESTED | Observador de cambios de procesos y fallback implementados. No se reinició ni terminó Explorer para probarlo. |
| P0-10 Cierre | NOT_TESTED | Cierre de ventanas y cleanup implementados. Falta probar el botón/cierre de ventana y confirmar ausencia de ventana fantasma en interacción visual. |

## Recuperación verificada y procedimiento manual pendiente

La consola informa si `Ctrl+Alt+Shift+F12` se pudo registrar; si no, DESKTOP queda deshabilitado. El atajo llama a `RestoreAllFloating` sin depender del foco/visibilidad del panel. Cada transición experimental arma primero un watchdog nativo de timer a 15 s; si la restauración falla o lanza excepción, registra el error y reintenta cada segundo. El control de consola restaura todas las ventanas conocidas. Al cerrar, se desregistra el hotkey y se limpian timers. El test cronometrado real verificó recuperación; la prueba de la tecla física queda pendiente.

1. Abrir el EXE unpackaged anterior. Sin archivos personales, inspeccionar la barra del panel, mover/redimensionar, abrir Diagnóstico, usar “Restaurar posición segura” y el botón “Restaurar todos a FLOATING”.
2. Con el panel flotante, pulsar físicamente `Ctrl+Alt+Shift+F12` mientras otra aplicación propia/no sensible tiene el foco; comprobar en dashboard/log que vuelve a FLOATING. No cambiar el foco a NexSpace antes de pulsar.
3. Validar cierre por botón y por X; confirmar que desaparecen consola y panel, y que se puede volver a abrir sin hotkey duplicado.
4. P0-04: solo en sesión controlada sin trabajo importante, abrir el panel, usar la prueba Desktop (watchdog 15 s), observar orden Z frente al escritorio y una ventana normal, probar foco/contexto; dejar que watchdog lo restaure. No desactivar el watchdog ni modificar Explorer/iconos.
5. P0-08/P0-09: usar VM o sesión reversible antes de cambiar escala/resolución, conectar monitores o reiniciar Explorer; confirmar fallback y logs.
6. P0-07: consulta de visibilidad se mantiene no disponible. No probar ocultamiento/restauración de iconos.

## Cierre de Fase 0

FLOATING tiene prototipo ejecutable y salvaguardas de recuperación verificadas; la interacción visual y ciclo de cierre requieren prueba humana. DESKTOP sigue experimental: se comprobó el rollback de una operación limitada, no la integración visual ni compatibilidad. No se inicia Fase 1; se requiere revisión humana para los casos indicados y la ruta de distribución.
