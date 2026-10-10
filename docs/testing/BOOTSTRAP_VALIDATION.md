# Evidencia de validación · Bootstrap B0

**Fecha:** 2026-10-10

**Entorno:** Windows 11 Pro x64, build 26300; .NET SDK 10.0.401; Windows App SDK 2.5.1; compilación x64.
**Alcance:** estructura, restauración, compilación y runner de pruebas. No es la validación funcional de Fase 0.

| Caso | Resultado | Evidencia |
|---|---|---|
| Inspección del entorno | PASS | Windows 11 Pro x64, build 26300; Git 2.47.1.windows.2; SDK .NET 10.0.401 instalado en el perfil del usuario. |
| Restauración NuGet | PASS | `dotnet restore NexSpace.sln -p:Platform=x64`; siete proyectos restaurados. |
| Build Debug x64 | PASS | `dotnet build NexSpace.sln --configuration Debug --no-restore -p:Platform=x64`; 0 errores y 0 advertencias. |
| Build Release x64 | PASS | `dotnet build NexSpace.sln --configuration Release -p:Platform=x64`; 0 errores y 0 advertencias. |
| Pruebas UnitTests Release | PASS | 1 pasada: el host de pruebas usa .NET 10. |
| Pruebas IntegrationTests Release | NOT_TESTED | 1 prueba omitida explícitamente: los probes de integración se crearán en la Fase 0 funcional. |
| Inicio visual de WinUI | NOT_TESTED | No se lanzó la aplicación dentro del alcance B0. Visual Studio no está instalado. |
| Casos funcionales P0-01 a P0-12 | NOT_TESTED | Reservados para la siguiente ejecución de Fase 0; no se infiere aprobación a partir de la compilación. |
| Publicación del escritorio original | PASS | `assets/references/original/` está excluido por `.gitignore`; se conserva localmente. |

## Observaciones y límites

- La primera compilación de la solución seleccionó una plataforma distinta de x64 para el proyecto WinUI y produjo `NETSDK1047`. Se repitió restore y build con `-p:Platform=x64`; la compilación quedó correcta y ese parámetro se incorporó a CI e instrucciones locales.
- La plantilla WinUI activaba trimming en Release, lo que produjo advertencias de bibliotecas de Windows. Se desactivó trimming/ReadyToRun para este esqueleto. La compilación Release final terminó con cero advertencias.
- Las pruebas actuales no ejercitan comportamiento del producto. El test de integración permanece visible como omitido; no se reporta como aprobado.
- La ausencia de Visual Studio impide depuración F5 integrada; el proyecto sí restaura y compila con .NET CLI y paquetes oficiales.
- No se ha cambiado la configuración de Explorer, la visibilidad de iconos ni archivos personales.
- No se han creado capturas. Las referencias conceptuales no se consideran evidencia de pruebas.

## Estado de validación funcional de Fase 0

Todos los casos P0-01 a P0-12 siguen `NOT_TESTED`. Ejecutar y registrar esos casos, incluyendo la experiencia flotante y la hipótesis de escritorio integrado, corresponde a la fase funcional pendiente.
