# Arquitectura de ventanas y recuperación · Fase 0

## Límites de responsabilidad

- `NexSpace.Application` define `IWindowHost`, identificadores lógicos sin HWND persistente, resultados y `RecoveryWatchdog`.
- `NexSpace.UI.WinUiWindowHost` es dueño de las ventanas WinUI y mantiene el HWND nativo en memoria. `DisplayMode` y el temporizador se asocian a cada ventana.
- `NexSpace.Windows.DesktopIntegrationService` encapsula la operación experimental de orden Z. No cambia el parent, no localiza WorkerW y no configura Explorer.
- `GlobalEmergencyHotkey` registra `Ctrl+Alt+Shift+F12` en el HWND de la consola; desregistra al cerrar. El sistema entrega `WM_HOTKEY` al propietario independientemente del foco actual. Si el registro falla, DESKTOP se deshabilita.
- `JsonEventLog` persiste transiciones y errores en `%LOCALAPPDATA%\NexSpace\logs\events.jsonl`.

## Secuencia y fallbacks

1. Antes de intentar el modo experimental, el host inicia un `System.Threading.Timer` de 15 segundos. No requiere que el panel esté visible ni que el dispatcher WinUI procese mensajes.
2. Solo después del armado se llama `Attach(HWND_BOTTOM)` para la ventana demo propia.
3. El usuario puede restaurar mediante el atajo global o botón de consola; el watchdog también intenta restaurar automáticamente al expirar.
4. Un fallo/excepción de recuperación se registra y el watchdog reintenta cada segundo. Si el attach falla, el host intenta rollback inmediato; ante fallo de ese rollback conserva el watchdog.
5. En modo FLOATING, no hay cambio de orden Z. Al cerrar, se intenta detach, se limpian watchdog y hotkey, y se registra cualquier fallo.

El watchdog de producción dura 15 segundos; los tests usan intervalos cortos solo para comprobar timeout, reintentos y captura de excepción. Una prueba controlada Windows 11 confirmó el rollback de una ventana propia. Esto no acredita integración visual, compatibilidad con Explorer, comportamiento con varias aplicaciones ni respeto visual a los iconos. Esos puntos permanecen pendientes de observación manual; no se permiten cambios a Explorer ni a visibilidad de iconos.
