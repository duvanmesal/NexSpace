# NexSpace

NexSpace es un organizador modular para el escritorio de Windows 11. La dirección visual aprobada combina contenedores compactos con un dock flotante opcional, siguiendo el concepto C de `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

El repositorio contiene Fases 1 y 2 aceptadas condicionalmente y **Fase 3 · perfiles y temas**, pendiente de aceptación. Un proceso comparte contenedores FLOATING, dock, Manager, perfiles manuales, temas, SQLite v3, bandeja y recuperación. Las referencias nunca alteran originales. Drag-and-drop físico, accesibilidad integral, DPI/multimonitor, translucidez, animación y perfilado prolongado siguen pendientes; DESKTOP está deshabilitado.

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
& "$env:USERPROFILE\.dotnet\dotnet.exe" restore NexSpace.sln -p:Platform=x64
& "$env:USERPROFILE\.dotnet\dotnet.exe" build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests\NexSpace.UnitTests\NexSpace.UnitTests.csproj --configuration Debug
& "$env:USERPROFILE\.dotnet\dotnet.exe" test tests\NexSpace.IntegrationTests\NexSpace.IntegrationTests.csproj --configuration Debug
```

La solución mapea librerías/pruebas a AnyCPU y UI a x64. También se pueden compilar/ejecutar pruebas explícitamente con `-p:Platform=x64`; no mezclar ese override con `--no-build` sobre ensamblados construidos solo por la solución en otra carpeta. Una compilación exitosa no valida comportamiento visual ni integración de escritorio. Resultados actuales y comandos en [PHASE3_VALIDATION](docs/testing/PHASE3_VALIDATION.md).

Para abrir el paquete de desarrollo (requiere Developer Mode):

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project src/NexSpace.UI/NexSpace.UI.csproj --configuration Debug -p:Platform=x64
```

Alternativa sin Developer Mode: compila como unpackaged y ejecuta el EXE. No registra un paquete ni modifica Windows:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build src\NexSpace.UI\NexSpace.UI.csproj --configuration Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
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

Fase 2 publicada tras aceptación condicionada. Fase 3 autorizada, sus commits permanecen locales hasta revisión del usuario. No avanzar a Fase 4. Inicio con Windows desactivado por defecto; no activado en pruebas. Estado y pendientes en [roadmap](docs/ROADMAP.md).
