# ADR-006: Estrategia experimental de integración con el escritorio

- **Fecha:** 2026-10-10
- **Estado:** Experimento implementado; integración visual pendiente de validación humana
- **Alcance:** prototipo WinUI 3 en Windows 11 x64

## Contexto

DEC-01 conserva el objetivo híbrido y DEC-10 deja pendiente el mecanismo técnico. La Fase 0 requiere un modo flotante seguro y una hipótesis reversible, sin reemplazar Explorer, ocultar iconos ni depender exclusivamente de jerarquías privadas como `WorkerW`.

## Decisión de implementación

- FLOATING usa una ventana WinUI 3 independiente como ruta de referencia.
- `IWindowHost` mantiene una identidad `WindowHandle` basada en GUID; `WinUiWindowHost` es dueño de `Window`/`AppWindow` y resuelve HWND solo en el borde WinUI/Windows. El HWND no se persiste.
- El candidato DESKTOP es una operación explícita y reversible de orden Z con `SetWindowPos(HWND_BOTTOM)`. La restauración devuelve el panel al orden superior sin activarlo.
- No se usa `SetParent`, no se inyecta en Explorer y no se busca `WorkerW`/`Progman` como requisito de funcionamiento.
- La interfaz llama a este modo **experimental**, muestra el riesgo y solicita confirmación en cada activación/restauración. DESKTOP se bloquea si no se registró el atajo global de emergencia.
- Antes de `Attach(HWND_BOTTOM)` se arma un watchdog de timer independiente de WinUI, con timeout de 15 segundos y reintento cada segundo hasta recuperar FLOATING. `Ctrl+Alt+Shift+F12` y el control de consola intentan restaurar todos los paneles sin depender de foco/visibilidad. Las transiciones y fallos se registran localmente.
- Un cambio de conjunto de procesos Explorer provoca fallback a FLOATING; NexSpace nunca inicia, finaliza ni reinicia Explorer.
- La consulta/modificación de visibilidad de iconos queda fuera del adaptador: consulta no disponible; no se escribe configuración del Shell.
- `UnavailableDesktopIconService` devuelve `null` (desconocido), nunca infiere `false` cuando la consulta no existe.
- Si el panel queda oculto, el atajo global y watchdog no necesitan mostrarlo. El botón de consola es un camino adicional; el cierre de aplicación también intenta devolverlo a FLOATING.

## Evidencia y límites

La solución compila en Debug y Release. Las pruebas automatizadas verifican el timer de recuperación, reintento ante excepción y registro/desregistro del hotkey en un HWND aislado. En Windows 11 real, una ejecución controlada confirmó: hotkey registrado; watchdog activo antes de aplicar `HWND_BOTTOM` a una sola ventana NexSpace; restauración automática aproximadamente a los 15 segundos; proceso aún respondía. Esto verifica el mecanismo de rollback, no su activación con tecla física ni el aspecto/ubicación del panel frente al escritorio/apps. No existe evidencia de respeto visual a los iconos, foco o estabilidad con otras ventanas. DESKTOP no es compatible/certificado y permanece experimental.

### Verificación manual reportada por el usuario

El 2026-10-10 el usuario reportó en Windows 11 que el panel aparece correctamente, se mueve y redimensiona, el atajo global `Ctrl+Alt+Shift+F12` funciona, el panel puede cerrarse y Explorer continuó funcionando durante las interacciones probadas. Se registra como evidencia manual aportada por el usuario, no como prueba automatizada ni observación independiente. Confirma la viabilidad funcional básica de FLOATING; no reporta observación de DESKTOP, reinicio de Explorer, iconos ni cambios de DPI/monitor.

Una prueba adicional no invasiva de cierre de la app unpackaged terminó normalmente: `CloseMainWindow()` aceptado, proceso PID 8580 terminado en menos de 2 s, sin proceso de esa ruta ejecutable y con evento `application-closed` en el log. Se cerró sin panel abierto; el cierre de consola con panel abierto continúa parcialmente pendiente. El cierre de Fase 0 está condicionado a los criterios pendientes de P0; no se amplía la afirmación de compatibilidad DESKTOP.

El intento de `dotnet run` empaquetado se detuvo porque Developer Mode estaba desactivado. No se cambió esa configuración. El modo unpackaged se lanzó con propiedades de MSBuild locales; Microsoft documenta esa forma de auto-inicializar Windows App SDK, pero este ADR no la convierte en decisión de distribución.

## Consecuencias

- FLOATING es el fallback de producto para Fase 0 y sigue independiente del Shell.
- FLOATING superó las comprobaciones funcionales básicas reportadas por el usuario y la prueba técnica de salida limpia; los resultados manuales se distinguen de automatización en `docs/testing/PHASE0_VALIDATION.md`.
- P0-04 continúa pendiente. No volver a ejecutar la operación `HWND_BOTTOM` en esta entrega; cualquier ensayo futuro requiere autorización/sesión controlada y el watchdog.
- No habilitar DESKTOP por defecto ni presentarlo como función estable.
- Si el experimento falla o no es demostrable, mantenerlo desactivado y conservar FLOATING; evaluar mecanismos soportados o una VM antes de cualquier prueba más invasiva.

## Referencias técnicas

- [Windows AppWindow y conceptos de ventanas](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview)
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [SetWindowSubclass](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass)
- [SetParent y consideraciones de estilos/DPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- [Despliegue unpackaged de Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/use-windows-app-sdk-run-time)
