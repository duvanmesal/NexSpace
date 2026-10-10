# NexSpace

NexSpace es un organizador modular para el escritorio de Windows 11. La dirección visual aprobada combina contenedores compactos con un dock flotante opcional, siguiendo el concepto C de `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

El repositorio contiene un **prototipo funcional inicial de Fase 0**: consola WinUI, panel flotante de demostración, probe de monitores, recuperación de posición y un candidato experimental de orden Z bajo. La convivencia visual con Explorer y la integración real al escritorio siguen pendientes de validación manual; no se afirma compatibilidad integrada.

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
 ├── NexSpace.Infrastructure ── NexSpace.Application
 └── NexSpace.Windows ── NexSpace.Application

NexSpace.UnitTests ── Domain, Application
NexSpace.IntegrationTests ── Infrastructure, Windows
```

`NexSpace.UI` alberga WinUI 3, la consola de estado, `WinUiWindowHost` y el panel de demostración. `Application` contiene `IWindowHost`, identidades estables no basadas en HWND, contratos de integración y política pura de geometría. `Domain` no depende de plataforma ni almacenamiento. `Infrastructure` contiene registro JSON local sin contenido de archivos. `Windows` encapsula enumeración Win32 de pantallas, respuesta desconocida de visibilidad y el cambio Z experimental. No están implementados NexDock, gestión de referencias, perfiles ni operaciones de archivos.

## Tecnologías y versiones

Baseline seleccionada el 2026-10-10:

- .NET SDK 10.0.401 y .NET 10 LTS.
- WinUI 3 mediante Windows App SDK 2.5.1 (canal estable).
- Windows SDK build-tools 10.0.28000.2705; API target `10.0.26100.0`; mínimo Windows 11 `10.0.22000.0`.
- `Microsoft.Windows.SDK.BuildTools.WinApp` 0.7.1 para ejecutar el paquete de desarrollo con identidad de aplicación.
- xUnit 2.9.3 y Microsoft.NET.Test.Sdk 17.14.1.

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

Los proyectos de prueba no deben recibir `-p:Platform=x64`: ese override cambia su carpeta de salida y el test runner no encuentra el ensamblado construido por la solución. Una compilación exitosa no valida el comportamiento visual ni la integración con el escritorio.

Para abrir el paquete de desarrollo (requiere Developer Mode):

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project src/NexSpace.UI/NexSpace.UI.csproj --configuration Debug -p:Platform=x64
```

Alternativa sin Developer Mode: compila como unpackaged y ejecuta el EXE. No registra un paquete ni modifica Windows:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build src\NexSpace.UI\NexSpace.UI.csproj --configuration Debug -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false
& ".\src\NexSpace.UI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\NexSpace.UI.exe"
```

La estrategia y sus límites están en [ADR-006](docs/decisions/ADR-006-desktop-integration.md); la evidencia actual, incluida la matriz P0, en [PHASE0_VALIDATION.md](docs/testing/PHASE0_VALIDATION.md).

## Privacidad del repositorio

Los conceptos aprobados de `assets/references/concepts/` se versionan. La captura original en `assets/references/original/` contiene información personal y queda excluida de Git; permanece disponible localmente. También se ignoran binarios de compilación, estado del IDE, bases locales, registros, credenciales, firmas y capturas privadas.

Antes de publicar, revisar `git status`, `git diff --cached` y la lista de archivos staged. No agregar secretos ni capturas privadas del escritorio.

## Documentación principal

- `AGENTS.md`: límites operativos del repositorio.
- `docs/requirements/NEXSPACE_ESPECIFICACION_COMPLETA.md`: especificación funcional y técnica.
- `docs/decisions/DECISIONES_APROBADAS.md`: decisiones aprobadas.
- `docs/phase-0/PLAN_Y_CRITERIOS.md`: plan y matriz de evidencia de Fase 0.
- `assets/references/README.md`: guía de referencias visuales.

No iniciar Fase 1 hasta revisión humana de la evidencia de Fase 0.
