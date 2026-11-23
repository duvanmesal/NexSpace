# NexSpace

NexSpace es un organizador modular para el escritorio de Windows 11. La dirección visual aprobada combina contenedores compactos con un dock flotante opcional, siguiendo el concepto C de `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

El repositorio contiene Fases 1, 2 y 3 aceptadas condicionalmente. La **Fase 4.1** conserva pendientes de estabilidad. Un proceso comparte contenedores FLOATING, dock, Manager, perfiles manuales, temas, SQLite v3, bandeja y recuperación. Las referencias nunca alteran originales. Drag-and-drop físico, accesibilidad integral, DPI/multimonitor, translucidez, animación y perfilado prolongado siguen pendientes; DESKTOP está deshabilitado.

## Trabajo desde una sola carpeta

Abre PowerShell en `Documents/NEXSPACE`. Cada worktree comparte objetos y ramas con esta carpeta. El código WinUI vive en `src/`, las pruebas en `tests/`, los diagnósticos en `tools/`, las decisiones en `docs/` y las referencias aprobadas en `assets/`. Los experimentos viven en `prototypes/web-shell/` en sus propias ramas.

| Rama | Uso |
|---|---|
| `origin/main` | Base publicada de Windows 11. Consultar el SHA remoto antes de distribuir. |
| `main` local | Contiene también estabilización de Fase 4.1 anterior a la aceptación final. |
| `design/nexspace-ux-refresh` | Historial de diseño del Concepto C. |
| `integration/concept-c-phase41` | Candidata WinUI que reúne ambos historiales. |
| `codex/web-shell-lab` | Comparación A/B de WebView2 y Tauri. |
| `experiment/tauri-nexcore` | Piloto local de Tauri con servicios .NET y SQLite de demostración. |

Desde la raíz se pueden consultar todas las ramas sin abrir las otras carpetas.

```powershell
git status --short --branch
git worktree list
git fetch origin
git branch -vv
git log --oneline --graph --decorate --all -35
git ls-remote --heads origin
```

Para revisar una rama ocupada en otro worktree, comprueba primero el estado y los archivos locales sin seguimiento. Con el trabajo guardado, usa `git switch --detach codex/web-shell-lab` para ejecutar el laboratorio desde esta carpeta. Para editar, crea una rama de trabajo con `git switch -c work/mi-cambio codex/web-shell-lab`. Si Git informa una colisión, preserva los archivos locales antes de continuar. `git switch -` vuelve a la revisión anterior.

En esta instalación, el SDK .NET y la cadena Rust GNU se pueden reutilizar desde la raíz habitual. Estas variables duran una sesión de PowerShell.

```powershell
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'dotnet-sdk'
$labTools = (Resolve-Path '../NEXSPACE-web-lab/artifacts/web-lab').Path
$env:CARGO_HOME = Join-Path $labTools 'cargo'
$env:RUSTUP_HOME = Join-Path $labTools 'rustup'
$env:Path = "$env:DOTNET_ROOT;$env:CARGO_HOME/bin;$labTools/mingw64/bin;$env:Path"
```

Desde `codex/web-shell-lab` o una revisión derivada de esa rama, compila y ejecuta el laboratorio A/B:

```powershell
./prototypes/web-shell/Build.ps1
& ./artifacts/web-lab/A/NexSpace.WebLab.exe
# Cierra A antes de iniciar B.
& ./artifacts/web-lab/B/nexspace-tauri-lab.exe
```

Desde `experiment/tauri-nexcore`, usa `./prototypes/web-shell/Build-Pilot.ps1` y ejecuta `./artifacts/tauri-nexcore/B/nexspace-tauri-lab.exe`. Para partir de una biblioteca de demostración existente, usa el parámetro `-DemoDatabase`. La rama del piloto contiene los contratos, las pruebas y la preparación reproducible en `prototypes/web-shell/PILOTO.md`. Las salidas de A/B y las del piloto ocupan carpetas distintas. Mantén los ejecutables, bibliotecas de prueba, capturas y mediciones privadas en `artifacts/`. El lanzamiento WinUI unpackaged y las pruebas se describen más abajo.

## Principios del producto

- Añadir un elemento crea una referencia; no mueve ni copia el original.
- NexSpace no reemplaza Explorer ni la barra de tareas.
- La integración con Windows queda aislada detrás de interfaces y debe degradarse de forma segura.
- El comportamiento sigue la especificación y las decisiones aprobadas.
- Los datos del producto permanecen locales; no se requiere cuenta ni telemetría.

## Arquitectura

La solución es un monolito modular con dependencias unidireccionales:

```text
NexSpace.UI
 ├── NexSpace.Application ── NexSpace.Domain
 ├── NexSpace.Presentation ── NexSpace.Application
 ├── NexSpace.Infrastructure ── NexSpace.Application
 └── NexSpace.Windows ── NexSpace.Application

NexSpace.UnitTests ── Domain, Application, Presentation
NexSpace.IntegrationTests ── Infrastructure, Windows, Presentation
```

`NexSpace.UI` alberga WinUI 3, NexManager, NexDock y ventanas por contenedor bajo un coordinador común. `Application` define servicios y contratos; `Presentation` expone estado observable sin WinUI. `Domain` mantiene reglas independientes. `Infrastructure` persiste SQLite y registros sin rutas de recursos. `Windows` encapsula monitores, Shell, bandeja e inicio opcional. Véase [arquitectura de Fase 3](docs/architecture/PHASE3_PROFILES_THEMES.md). El wiring nativo conserva code-behind; no se afirma MVVM exhaustivo. DESKTOP sigue experimental y deshabilitado.

## Tecnologías y versiones

Baseline seleccionada el 2026-10-10:

- .NET SDK 10.0.401 y .NET 10 LTS.
- WinUI 3 mediante Windows App SDK 2.5.1 (canal estable).
- Windows SDK build-tools 10.0.28000.2705; API target `10.0.26100.0`; mínimo Windows 11 `10.0.22000.0`.
- `Microsoft.Windows.SDK.BuildTools.WinApp` 0.7.1 para ejecutar el paquete de desarrollo con identidad de aplicación.
- Microsoft.Data.Sqlite 10.0.12, xUnit 2.9.3 y Microsoft.NET.Test.Sdk 17.14.1.

La razón de selección y las limitaciones del entorno constan en `docs/decisions/ADR-007-development-baseline.md`.

## Requisitos

- Windows 11 x64.
- .NET SDK 10.0.401 o un SDK compatible de la línea .NET 10.
- Acceso a NuGet para restaurar paquetes.
- Developer Mode habilitado para lanzar el paquete WinUI de desarrollo mediante `dotnet run`; como alternativa local se puede usar la configuración unpackaged documentada abajo.
- Visual Studio con herramientas Windows App SDK recomendado para depuración gráfica. No estaba instalado en el entorno del bootstrap.

## Compilar y ejecutar pruebas

```powershell
dotnet restore NexSpace.sln -p:Platform=x64
dotnet build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64
dotnet test tests\NexSpace.UnitTests\NexSpace.UnitTests.csproj --configuration Debug
dotnet test tests\NexSpace.IntegrationTests\NexSpace.IntegrationTests.csproj --configuration Debug
```

La solución mapea librerías/pruebas a AnyCPU y UI a x64. También se pueden compilar/ejecutar pruebas explícitamente con `-p:Platform=x64`; no mezclar ese override con `--no-build` sobre ensamblados construidos solo por la solución en otra carpeta. Una compilación exitosa no valida comportamiento visual ni integración de escritorio. Resultados actuales y comandos en [PHASE3_VALIDATION](docs/testing/PHASE3_VALIDATION.md).

Para abrir el paquete de desarrollo (requiere Developer Mode):

```powershell
dotnet run --project src/NexSpace.UI/NexSpace.UI.csproj --configuration Debug -p:Platform=x64
```

Alternativa sin Developer Mode: compila como unpackaged y ejecuta el EXE. No registra un paquete ni modifica Windows:

```powershell
dotnet build src\NexSpace.UI\NexSpace.UI.csproj --configuration Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
& ".\src\NexSpace.UI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\NexSpace.UI.exe"
```

La evidencia actual está en [PHASE3_VALIDATION.md](docs/testing/PHASE3_VALIDATION.md), con antecedentes en las matrices de Fases 1 y 2. SQLite v3, respaldos, perfiles y Shell se explican en [ADR-010](docs/decisions/ADR-010-profiles-themes-shell.md); DESKTOP sigue experimental según ADR-006. No usar binarios anteriores sobre una biblioteca v3. Antes de migrar v1/v2 se crea un respaldo consistente; recuperar en otro directorio, nunca sobrescribir silenciosamente.

Para repetir la demostración aislada persistente, consultar el comando Release unpackaged de la matriz. `NEXSPACE_DATA_DIR` permite conservar una biblioteca de prueba entre reinicios sin usar datos personales. Windows decide si permite acrílico/animaciones: no es necesario cambiar ajustes para usar la alternativa sólida.

## Privacidad del repositorio

Los conceptos aprobados de `assets/references/concepts/` se versionan. La captura original en `assets/references/original/` contiene información personal y queda excluida de Git; permanece disponible localmente. También se ignoran binarios de compilación, estado del IDE, bases locales, registros, credenciales, firmas y capturas privadas.

Antes de publicar, revisar `git status`, `git diff --cached` y la lista de archivos staged. No agregar secretos ni capturas privadas del escritorio.

## Documentación principal

- `AGENTS.md`: límites operativos del repositorio.
- `docs/requirements/NEXSPACE_ESPECIFICACION_COMPLETA.md`: especificación funcional y técnica.
- `docs/decisions/DECISIONES_APROBADAS.md`: decisiones aprobadas.
- `docs/phase-0/PLAN_Y_CRITERIOS.md`: plan y matriz de evidencia de Fase 0.
- `assets/references/README.md`: guía de referencias visuales.

Fases 2 y 3 aceptadas condicionalmente y publicadas. Fase 4 autorizada para estabilización y preparación de distribución. La publicación de ramas de investigación requiere revisar su historial y verificar el SHA remoto. No es una declaración de disponibilidad de NexSpace 1.0. Inicio con Windows desactivado por defecto; no activado en pruebas. Estado y pendientes en [roadmap](docs/ROADMAP.md).

Preparación de la candidata: [instalación y distribución](docs/phase-4/DISTRIBUCION.md), [uso y recuperación](docs/phase-4/USO_Y_RECUPERACION.md), [diagnóstico y aceptación](docs/testing/PHASE4_VALIDATION.md).

## Conservación de worktrees

La carpeta principal conserva la configuración local de agentes. La de diseño contiene una instalación local de skills. La de integración incluye bases de demostración, capturas y mediciones excluidas. La del laboratorio contiene la cadena Rust, ejecutables y evidencia histórica. Antes de retirar cualquiera, comprobar su rama con `git branch -vv`, revisar `git status --short --ignored`, respaldar los artefactos privados que deban conservarse y verificar las herramientas de compilación desde la raíz. La retirada requiere autorización explícita por carpeta.
