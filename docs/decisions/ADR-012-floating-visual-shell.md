# ADR-012 · Capa visual flotante fiel al Concepto C

- **Estado:** PROPUESTA — pendiente de aprobación humana. No modifica `DECISIONES_APROBADAS.md`.
- **Fecha:** 2026-10-13
- **Rama:** `design/nexspace-ux-refresh` (worktree aislado; sin merge ni push)
- **Referencia visual:** `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`

## Contexto

Tras la estabilización de Fase 4, NexContainers seguía usando la barra de título del sistema
(título gris, minimizar/maximizar/cerrar). NexDock era un rectángulo con borde blanco. Ese estado
no se parecía al Concepto C, que propone tarjetas flotantes sin marco de sistema, un dock centrado
con selección visible y un popover anclado al icono activo.

La personalización nativa del caption ya se descartó en Fase 4 porque acumulaba recursos GDI. No
se reintroduce.

## Rutas evaluadas

| Criterio (1 = malo · 5 = bueno) | R1 WinUI 3 puro (caption del sistema) | R2 WinUI 3 + shell flotante propio | R3 WPF híbrido para la capa flotante | R4 Win32/DirectComposition propio |
|---|---|---|---|---|
| Fidelidad al Concepto C | 1 | 4 | 4 | 5 |
| Riesgo técnico (5 = bajo) | 5 | 4 | 2 | 1 |
| Coste de implementación (5 = bajo) | 5 | 4 | 1 | 1 |
| Impacto en estabilidad (5 = nulo) | 5 | 4 | 2 | 2 |
| Compatibilidad con el trabajo actual | 5 | 5 | 2 | 2 |
| Conservar funcionalidad existente | 5 | 5 | 2 | 2 |
| Calidad visual alcanzable | 1 | 4 | 4 | 5 |
| Riesgo de romper escritorio/recuperación (5 = bajo) | 5 | 5 | 4 | 3 |
| Riesgo de recursos (5 = bajo) | 5 | 5 (medido) | 3 | 2 |
| **Total** | 37 | **40** | 24 | 23 |

- **R1 — descartada.** Es estable, pero ya se probó: el marco de sistema sobre cada contenedor
  contradice el Concepto C y ninguna opción permitida lo elimina.
- **R2 — recomendada.** Es la misma pila, los mismos servicios y el mismo ciclo de vida. Solo cambia
  el marco de las ventanas flotantes. Es la única ruta que se midió en ejecución real.
- **R3 — descartada.** WPF no aporta un material que WinUI no tenga: una ventana WPF transparente
  (layered) no recibe Mica ni Acrylic del sistema. Además duplica framework, despachadores, temas,
  recuperación y diagnósticos de recursos. Exige reescribir la UI de contenedores, dock y popover
  en un segundo stack sin una ventaja medible.
- **R4 — descartada como ruta principal.** Subclasificar `WM_NCCALCSIZE`, o dibujar con
  DirectComposition, es lo único que elimina al 100 % el marco residual. A cambio, toca HWND y
  procedimientos de ventana, lo que la directriz de estabilidad restringe. No se justifica por una
  franja de pocos píxeles. Avalonia no ofrece ninguna ventaja concreta: tiene los mismos límites de
  material en Windows y obligaría a migrar todo.

## Decisión propuesta (Ruta 2)

1. Contenedores y dock usan `OverlappedPresenter.SetBorderAndTitleBar(false, false)`. Así lo hacía
   ya NexDock.
2. Bordes redondeados de Windows 11 y borde discreto mediante dos atributos DWM de una sola vez
   (`DWMWA_WINDOW_CORNER_PREFERENCE`, `DWMWA_BORDER_COLOR`) y el tema del marco residual
   (`DWMWA_USE_IMMERSIVE_DARK_MODE`). **No** se usa `DWMWA_CAPTION_COLOR` ni ninguna API de colores
   del caption.
3. `Window.ExtendsContentIntoTitleBar = true` solo como bandera, sin `SetTitleBar`.
4. Arrastre desde la cabecera dibujada por la app y redimensión desde un grip propio.
   - Ambos usan `AppWindow.Move/Resize` con coordenadas de cursor en pantalla (`GetCursorPos`).
   - Esto no subclasifica la ventana.
5. Popover del dock con presentador transparente, sin sombra por defecto, tarjeta propia y cola
   anclada al icono activo.
6. NexManager se queda como ventana estándar de configuración.
7. DESKTOP sigue deshabilitado; no se toca Explorer ni `HWND_BOTTOM`.

### Qué queda en WinUI y qué se aísla

- **Sin cambios en WinUI:** todo. Esto incluye servicios, persistencia SQLite, perfiles,
  recuperación, `NexThemeManager`, `DispatcherQueue` y debounce.
- **Aislado:** el marco flotante en `src/NexSpace.UI/Visuals/FloatingChrome.cs`, una sola clase con
  `Dispose()` que desengancha sus manejadores. Si se rechaza la propuesta, basta revertir esa clase
  y sus dos llamadas.

## Evidencia medida

Los datos de recursos salen de un contador externo (`GetGuiResources`) durante ciclos de cerrar y
reabrir un contenedor.

| Ejecución | Configuración | GDI | Resultado |
|---|---|---|---|
| A | Barra de título del sistema (referencia) | 64 → 56, plano | PASS |
| B | Primer chrome flotante: `SetBorderAndTitleBar(true,false)` | 159 → 639 | FAIL (fuga) |
| E/F | Solo presentador `(true,false)`, con o sin borde DWM | +16 por ciclo | FAIL: la causa está en el presentador |
| G/H | Solo manejadores / presentador `(false,false)` | plano | PASS |
| I | Prototipo, 60 ciclos | plano en 85 | PASS |
| J | Chrome final (`ExtendsContentIntoTitleBar` + tema de marco), 60 ciclos | plano en 85 | PASS |
| K | J + arrastre por cursor en pantalla, 30 ciclos | plano en 85 | PASS |

- Los handles crecen unos 3–4 por ciclo y el working set alrededor de 1 MB por ciclo. La referencia
  A con barra del sistema crece igual (handles +4, WS +0,6 MB por ciclo), así que el chrome no lo
  causa. Queda como pendiente de perfilado (Fase 4).
- Pruebas: 71 unitarias y 34 de integración en PASS. Compilación Debug y Release x64 sin errores.

## Sacrificios y límites conocidos

- **Franja superior en ventana activa.**
  - La ventana activa muestra una franja clara de unos 5 px en el borde superior. Es el marco
    residual que Windows pinta con el color de acento porque «Mostrar color de énfasis en barras de
    título» está activado en el equipo de prueba (`ColorPrevalence=1`).
  - Solo se corrige con `DWMWA_CAPTION_COLOR` (prohibido) o subclasificando `WM_NCCALCSIZE` (Ruta 4).
  - En ventanas inactivas no aparece.
- **Translucidez no evaluada (BLOCKED).** «Efectos de transparencia» está desactivado en el equipo
  (`EnableTransparency=0`). La app cae correctamente a superficie sólida, pero no se cambió el ajuste
  global sin autorización.
- **Esquinas y sombra según DWM.** DWM solo ofrece tres radios (ninguno, pequeño, redondo); la sombra
  es la del sistema.
- **Iconos `.lnk`.** No producen miniatura y caen al glifo genérico. Es una brecha de producto aparte.
- Accesibilidad de arrastre y redimensión por teclado: NOT_TESTED.
- DPI ≠ 100 % y multimonitor: NOT_TESTED.

## Validación pendiente (humana)

- Arrastrar y redimensionar con ratón físico. Con entrada sintética relativa ya pasa: +100/+80 exacto
  y mínimo de 320×220 respetado.
- Observar la franja superior con «color de énfasis en barras de título» desactivado.
- Con autorización explícita, activar temporalmente la transparencia para evaluar el material.
