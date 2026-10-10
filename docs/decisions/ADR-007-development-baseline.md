# ADR-007: Baseline de herramientas y estructura del repositorio

- **Fecha:** 2026-10-10
- **Estado:** Aceptada para Bootstrap B0
- **Alcance:** herramientas de desarrollo y esqueleto; no certifica compatibilidad funcional

## Contexto

El repositorio aún no tenía historial Git ni proyectos .NET. La especificación establece C#, .NET LTS, WinUI 3/Windows App SDK y un monolito modular. La combinación concreta debe ser reproducible y compatible con Windows 11 x64.

## Decisión

- Fijar .NET SDK `10.0.401` en `global.json`, con `latestFeature` para admitir parches/feature bands compatibles de .NET 10.
- Usar WinUI 3 mediante Windows App SDK `2.5.1`, canal estable al momento de esta decisión.
- Compilar contra Windows SDK `10.0.26100.0`, declarar mínimo Windows 11 `10.0.22000.0` y fijar `Microsoft.Windows.SDK.BuildTools` en `10.0.28000.2705`.
- Fijar `Microsoft.Windows.SDK.BuildTools.WinApp` en `0.7.1`; proporciona integración de lanzamiento con identidad de paquete desde `dotnet run`.
- Generar la app con el paquete oficial de plantillas `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` `0.0.7-alpha`; es una herramienta de scaffolding y no se referencia desde los proyectos ni se distribuye con NexSpace.
- Usar xUnit `2.9.3` y Microsoft.NET.Test.Sdk `17.14.1` para los proyectos de prueba.
- Crear módulos UI, Application, Domain, Infrastructure y Windows, más proyectos UnitTests e IntegrationTests. Mantener dependencias unidireccionales.
- No introducir almacenamiento, MVVM Toolkit ni lógica de NexContainers/NexDock/NexManager en el bootstrap. Añadir dependencias cuando exista un caso de uso aprobado y verificable.
- Mantener el test de integración como espacio reservado hasta que haya probes reales de Windows en Fase 0.

## Entorno inspeccionado

- Windows 11 Pro x64, versión de kernel `10.0.26300`, build `26300`.
- .NET SDK `10.0.401`, runtime .NET `10.0.12` instalado en `C:\Users\Duvan\.dotnet` para el usuario.
- El equipo no tenía SDK de .NET, Visual Studio, Windows SDK instalado independientemente ni `winget` antes del bootstrap. Las referencias de compilación de Windows y Windows App SDK se obtienen mediante NuGet.
- Git for Windows `2.47.1.windows.2`.
- El remoto configurado no tenía refs/ramas al inspeccionarlo.

## Consecuencias

- El proyecto requiere .NET 10 y acceso a NuGet para restaurar paquetes.
- El SDK instalado en el perfil de usuario no cambia la instalación .NET del sistema.
- CI compila y ejecuta en Windows; la máquina local todavía carece de Visual Studio para depuración visual integrada.
- La captura de escritorio original queda excluida de Git. Los conceptos A, B y C pueden versionarse; C sigue siendo la dirección aprobada.
- Esta decisión no modifica decisiones de producto aprobadas ni establece la técnica de integración con Explorer. Esa investigación corresponde a Fase 0 funcional y ADR-006.

## Referencias

- [Política oficial de soporte de .NET](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- [Descargas estables de Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- [Versionado de Windows SDK y Windows App SDK](https://learn.microsoft.com/windows/apps/get-started/versioning-overview)
- [Plantillas oficiales WinUI 3 para .NET CLI](https://github.com/microsoft/WindowsAppSDK/tree/main/dev/Templates/Dotnet)
