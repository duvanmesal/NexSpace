# ADR-014 · Integración del Concepto C y Fase 4.1

Fecha: 2026-11-15. Estado: implementación y validación en la rama local de integración. Aceptación para incorporar a `main` pendiente.

## Base

La rama `integration/concept-c-phase41` parte de `2627a6a` e incorpora `d2def4f`. El prototipo histórico de diseño pasa a ADR-013 para distinguirlo del ADR-012 de recursos nativos. Se conserva el registro de decisiones aprobadas y FLOATING sigue siendo la presentación activa.

## Resoluciones

- **NexContainers.** Se conserva el debounce cancelable, las guardas diagnósticas y el cierre con limpieza de Fase 4.1. Se incorpora el encabezado integrado, el marco sin caption convencional y el control de tamaño de Concepto C. Los eventos de `FloatingChrome` se retiran en `Closed`. La variante diagnóstica `system-chrome` permite comparar con el título del sistema. El caption histórico con colores sigue detrás de su guardia de diagnóstico aislado.
- **NexDock.** Se conservan el debounce, la firma que evita reconstruir controles por un cambio de posición, el cierre coordinado y la liberación de flyouts. Se integran la barra centrada, el indicador activo y el panel emergente anclado. Se retiran las escrituras de colores de botones del caption del dock. La preferencia de acento manual controla el indicador de selección en temas manuales. En Automático, ese indicador sigue el acento resuelto de Windows.
- **NexGlass y tema.** Se integran la paleta de Concepto C, los recursos compartidos y Automático con derivación RGB. Las notificaciones de Windows encolan la actualización en el hilo de interfaz. La suscripción de alto contraste se trata como opcional ante `0x80070490`, observado en el arranque unpackaged. La lectura de alto contraste sigue disponible al aplicar el tema. Cada material acrílico conectado conserva su controlador mientras se actualizan el tinte y el tema. El material se desconecta al cambiar a superficie sólida o cerrar la ventana.
- **Inicio portable.** Se conserva la copia de XBF/PRI y se publica en carpeta x64 con .NET y Windows App SDK autocontenidos. El esquema SQLite sigue siendo v3. Una prueba adicional comprueba el cambio entre perfiles con temas Automático y Contraste, una categoría enlazada y bibliotecas independientes.

El diagnóstico, los límites de seguridad, la recuperación, `DeferredAction` y la lógica de persistencia de Fase 4.1 permanecen como base. Las APIs empleadas por el material se contrastaron con la documentación de [DesktopAcrylicController](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.systembackdrops.desktopacryliccontroller) y [SystemBackdropConfiguration](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.systembackdrops.systembackdropconfiguration).

## Aceptación

La candidata se evalúa con la serie de regeneración de Fase 4.1 y una biblioteca de demostración independiente. El informe de pruebas distingue funcionamiento, consumo nativo y validación visual. La adopción del marco y la publicación de 1.0 siguen sujetas a la evidencia de estabilidad y revisión humana.
