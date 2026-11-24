# Laboratorio NexSpace · Concepto C

La capa flotante experimental conectada a SQLite v3 se compila, ejecuta y valida siguiendo [Paridad de NexContainers y NexDock](PARIDAD.md). Los ensayos A/B siguientes corresponden al laboratorio histórico.

**Rama del piloto NexCore.** Para compilar y ejecutar esta revisión, sigue [PILOTO.md](PILOTO.md) y consulta [la validación](VALIDACION_PILOTO.md). Las instrucciones de comparación A/B que siguen corresponden a la rama histórica `codex/web-shell-lab`.


Dos anfitriones Windows x64 abren el mismo bundle de producción de React, TypeScript, Vite y Tailwind. Cada contenedor, el dock y el popover son ventanas independientes. El diseño está en `src/`, la entrada .NET en `host/` y la entrada Tauri en `src-tauri/`. [Contrato visual](../../assets/references/concepts/03_C_sistema_hibrido_APROBADO.png).

## Ejecutar

Desde la raíz del worktree, abrir **una alternativa por vez**. Conservar las DLL junto a cada EXE.

```powershell
& ./artifacts/web-lab/A/NexSpace.WebLab.exe
# Después de cerrar A
& ./artifacts/web-lab/B/nexspace-tauri-lab.exe
```

El botón de encendido cierra el laboratorio. Cada ejecución crea una biblioteca de fichas TXT y un directorio WebView2 privados en TEMP bajo NexSpace-WebLab-A-<PID>-<sesión> o NexSpace-WebLab-B-<PID>-<sesión>. La ficha se abre al pulsar su icono y usa la asociación de Windows. Las ventanas comienzan expandidas. La flecha contrae, el encabezado mueve y el tirador redimensiona. Las flechas del teclado ajustan el tamaño cuando el tirador tiene el foco. Escape cierra el popover. El primer botón del dock recupera la disposición y Ctrl+Mayús+Inicio lo hace desde una superficie enfocada. El cambio de lista y de tema se comparte con las otras ventanas de esa sesión. Los perfiles de prueba se conservan en TEMP para diagnóstico.

## Compilar

Se necesitan Node/npm, SDK .NET 10, Rust con enlazador Windows x64 y runtime Evergreen WebView2. La publicación A incluye .NET. B incorpora el frontend y se distribuye junto a WebView2Loader.dll. Ambas usan Evergreen del equipo. Ejecutar desde la raíz:

```powershell
./prototypes/web-shell/Build.ps1
```

Esta máquina usó una cadena Rust GNU, WinLibs GCC, rustup y Cargo aislados dentro de `artifacts/web-lab`. Para reproducir con esa instalación en una nueva consola:

```powershell
$lab = (Resolve-Path artifacts/web-lab).Path
$env:CARGO_HOME = "$lab/cargo"
$env:RUSTUP_HOME = "$lab/rustup"
$env:Path = "$env:CARGO_HOME/bin;$lab/mingw64/bin;$env:Path"
./prototypes/web-shell/Build.ps1 -DotNet "$env:LOCALAPPDATA/dotnet-sdk/dotnet.exe"
```

La guía principal de Tauri para Windows usa Microsoft C++ Build Tools. El enlazador GNU emitió una advertencia por manifiestos de recursos múltiples. La ejecución medida fue satisfactoria en el monitor de prueba. Antes de distribuir hay que verificar el manifiesto DPI efectivo, comparar con MSVC, firmar, probar instalación y desinstalación y definir la entrega de Evergreen en equipos sin runtime. [Prerrequisitos de Tauri](https://v2.tauri.app/start/prerequisites/).

## Medir

En PowerShell 7 x64, repetir cada comando con A y B. La opción Capture guarda el escritorio completo en la carpeta privada ignorada `artifacts/web-lab/evidence`. Usarla con el escritorio preparado.

```powershell
./prototypes/web-shell/bench/Measure.ps1 A one
./prototypes/web-shell/bench/Measure.ps1 A three
./prototypes/web-shell/bench/Measure.ps1 A exercise -Capture
```

El escenario exercise usa las mismas acciones React en ambos hosts, contrae Juegos, redimensiona Aplicaciones, abre Archivos y alterna categorías y cierre. El ensayo registra procesos por relación padre/hijo, muestra de CPU, memoria y censo acotado de tipos de handle. Un desbordamiento de 2 GiB privados o 24 000 handles en una muestra interrumpe el proceso de prueba. El plazo de arranque es 45 s. El cierre solicita cerrar las ventanas de esa ejecución y registra si fue preciso terminarla. Los perfiles Chromium se crean vacíos en cada lanzamiento. No se fuerza GC.

## Composición y frontera nativa

- React dibuja encabezados, iconos, selección, foco, bordes y cola. Las superficies usan un gradiente con alfa y una sombra interior de un píxel. El documento carece de fondo opaco.
- A combina WPF, ventana sin decoraciones, AllowsTransparency y WebView2CompositionControl. El fondo del controlador es transparente. Se comparte CoreWebView2Environment entre las ventanas. WPF permite probar esta ruta sin recrear el ciclo de ventanas WinUI que se investigó en Fase 4.1. Un WebView2 dentro del WinUI existente también reutilizaría C#, pero conservaría la capa XAML y el problema del marco como variables del experimento. [WebView2 en WPF](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf).
- B usa ventanas transparentes sin decoraciones de Tauri/wry, un directorio WebView2 común y recursos compilados en el ejecutable. Windows compone el alfa con lo que está detrás. El CSS no puede muestrear el contenido de Explorer para desenfocarlo. El vidrio observado conserva el detalle del fondo. Acrylic y Mica requerirían integración con un material nativo y sus políticas de foco, contraste y efectos. La opción Windows de transparencia estaba desactivada. Las ventanas del laboratorio permiten alfa en esta configuración como variable experimental. Para producción hace falta respetar el fallback sólido. [Acrylic de Windows](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic).
- Los mensajes admiten operaciones concretas, identificadores de referencia de 0 a 17, categorías enumeradas y tamaños acotados. La ruta de cada TXT se construye en el anfitrión. A comprueba el documento emisor, restringe navegación, popups, permisos y objetos de host y reutiliza WindowsContainerItemPlatformService. B limita navegación y ventanas emergentes y expone un único comando local con enum Serde y validación del rol. El documento aplica CSP. No hay servidor TCP de desarrollo en las entregas. El laboratorio no solicita acceso a la biblioteca del usuario. [Seguridad WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security) · [Capacidades Tauri](https://v2.tauri.app/security/capabilities/).
- Los iconos de referencias son fixtures de [Microsoft Fluent Emoji](https://github.com/microsoft/fluentui-emoji), con licencia MIT incluida en public/art. Los nombres de aplicaciones y juegos identifican fichas de prueba. React usa controles HTML con etiquetas, foco y preferencia de movimiento reducido. Los colores de selección pueden recibir el acento mediante UISettings en A y el color de composición DWM en B. Las APIs pueden devolver valores distintos según la configuración. La notificación de cambio de acento en vivo queda para el adaptador de producción.

## Mapa de reutilización

| Módulo actual | Clasificación para A | Trabajo adicional para B |
|---|---|---|
| Domain y reglas de referencia | Reutilizable sin cambios | Mantener en proceso .NET |
| Application, ContainerService, WorkspaceService y ProfileService | Reutilizable sin cambios | DTO de consultas y comandos versionados |
| Infrastructure, SQLite v3, migraciones y perfiles | Reutilizable sin cambios | Un único escritor .NET propietario de SQLite |
| Categorías, perfiles y referencias en React | Reutilizable mediante adaptador | El mismo contrato pasa por IPC |
| Windows Shell, inspección y apertura | Reutilizable mediante adaptador. A ya usa el servicio real | Enviar ID, resolver destino en NexCore y devolver OperationResult |
| ShellIconLoader | Requiere refactorización. Separar extracción/cache de BitmapImage y retorno a XAML | Entregar bytes o URI local acotada con cancelación y límite de caché |
| Recuperación, coordenadas y watchdog | Reutilizable mediante adaptador. Reenlazar coordinador y registros de ventanas | Coordinar visibilidad, DPI y reinicio del sidecar |
| JsonEventLog | Reutilizable sin cambios en .NET | Correlacionar petición, PID y fallo Rust/.NET |
| Inicio Windows | Reutilizable mediante adaptador del ejecutable registrado | Definir dueño de la instalación. Se mantuvo inactivo |
| WindowsTrayIcon y GlobalEmergencyHotkey | Reutilizable mediante adaptador de ventana y ciclo de vida | Asignar un dueño único. Evitar dobles registros |
| NexManager | Reutilizable sin cambios durante el piloto | Mantener WinUI fuera del proceso de ventanas flotantes |
| Plantillas XAML del dock, tarjetas y flyouts | Requieren sustitución en la capa React | Mismos componentes y pruebas de interacción |

En B, la opción preferida para NexCore es un proceso hijo .NET con entrada y salida estándar heredadas, longitud máxima y un contrato versionado por acción. El propietario cancela peticiones y cierra el hijo al salir. Un pipe con ACL del usuario y autenticación de sesión serviría si el servicio necesitara vivir independientemente. Añade descubrimiento, control de versiones, exclusión del escritor y recuperación tras desconexión. El binario B medido aún carece de sidecar. Su consumo en una integración completa sería distinto. [Sidecars en Tauri](https://v2.tauri.app/develop/sidecar/).

## Coste y adopción condicional

Estimación de ingeniería para una persona familiarizada con NexSpace, en semanas-persona, antes de validar un instalador. Son rangos de planificación.

| Bloque | A | B |
|---|---:|---:|
| Contratos y biblioteca de demostración con servicios reales | 1–2 | 2–4, incluido IPC y ciclo de vida |
| Iconos Shell, selección, drag-and-drop y estados de error | 1–3 | 1–3 |
| Recuperación, bandeja, DPI y múltiples monitores | 2–3 | 2–4 |
| Accesibilidad, materiales, rendimiento, pruebas y empaquetado | 2–4 | 2–4 |

Primero pilotar un único NexContainer B conectado a un proceso hijo NexCore .NET con un perfil separado y una orden de apertura por ID. Usar A como control con acceso directo al mismo servicio. Medir el árbol completo con el sidecar antes de elegir un anfitrión para producción. Después comparar el árbol de procesos con WinUI en 100/300 cierres y cambios de perfil sobre el mismo conjunto. Someterlo a Narrador, dos escalas DPI, reinicio del anfitrión, monitor desconectado y efectos del sistema. Solo si cumple el presupuesto acordado, portar dock y popover con una opción de retorno al renderizador anterior. Mantener el gestor WinUI durante esas etapas. Los futuros widgets e islas podrían compartir el frontend, pero cada WebView adicional exige medir su coste antes de crear una ventana por componente.

[Mediciones, evidencia visual y decisión](RESULTADOS.md)
