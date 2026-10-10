# NexSpace

NexSpace es un organizador modular para el escritorio de Windows 11. La dirección visual aprobada combina contenedores compactos con un dock flotante opcional, siguiendo el concepto C de `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

El repositorio está en **Bootstrap B0**: estructura de solución, convenciones de desarrollo y CI. La Fase 0 funcional (ventanas, convivencia con Explorer, recuperación y pruebas de viabilidad) aún no se ha ejecutado. No se afirma compatibilidad con ventanas integradas al escritorio.

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

`NexSpace.UI` alberga WinUI 3 y composición. `Application` contiene casos de uso y contratos. `Domain` no depende de plataforma ni almacenamiento. `Infrastructure` queda reservado para persistencia y diagnóstico local. `Windows` encapsula Win32 y Shell. Los proyectos son esqueletos: NexContainers, NexDock, NexManager y la integración con Windows no están implementados.

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
- Modo de desarrollador de Windows habilitado para lanzar el paquete WinUI con `dotnet run`.
- Visual Studio con herramientas Windows App SDK recomendado para depuración gráfica. No estaba instalado en el entorno del bootstrap.

## Compilar y ejecutar pruebas

```powershell
dotnet restore NexSpace.sln -p:Platform=x64
dotnet build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64
dotnet test NexSpace.sln --configuration Debug --no-build -p:Platform=x64
```

GitHub Actions ejecuta restore, build y tests en Windows. La prueba unitaria actual verifica el runtime usado por el host de pruebas; no valida comportamiento de NexSpace. El test de integración queda explícitamente pendiente hasta que se incorporen probes reales de Windows durante Fase 0. Una compilación exitosa no valida la integración con el escritorio.

Para abrir localmente el cascarón WinUI:

```powershell
dotnet run --project src/NexSpace.UI/NexSpace.UI.csproj
```

## Privacidad del repositorio

Los conceptos aprobados de `assets/references/concepts/` se versionan. La captura original en `assets/references/original/` contiene información personal y queda excluida de Git; permanece disponible localmente. También se ignoran binarios de compilación, estado del IDE, bases locales, registros, credenciales, firmas y capturas privadas.

Antes de publicar, revisar `git status`, `git diff --cached` y la lista de archivos staged. No agregar secretos ni capturas privadas del escritorio.

## Documentación principal

- `AGENTS.md`: límites operativos del repositorio.
- `docs/requirements/NEXSPACE_ESPECIFICACION_COMPLETA.md`: especificación funcional y técnica.
- `docs/decisions/DECISIONES_APROBADAS.md`: decisiones aprobadas.
- `docs/phase-0/PLAN_Y_CRITERIOS.md`: plan y matriz de evidencia de Fase 0.
- `assets/references/README.md`: guía de referencias visuales.

Este bootstrap no ejecuta la implementación funcional de Fase 0. Las fases posteriores requieren revisión humana.
