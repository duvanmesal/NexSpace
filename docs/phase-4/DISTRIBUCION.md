# Distribución de la candidata · Fase 4

Estado: preparación técnica local, no lanzamiento. Windows 11 x64, mínimo declarado 22000, entorno probado 26100. No se certifican otras versiones, ARM64 ni x86.

## Variante unpackaged

Compilar explícitamente con `WindowsPackageType=None` y `EnableMsixTooling=false`. El EXE generado por una compilación empaquetada no debe tratarse como un EXE portátil. Un lanzamiento así falló con REGDB_E_CLASSNOTREG antes del constructor App; no demuestra la causa del fallo histórico 0xC000027B.

Publicación en carpeta (no single-file ni trimming):

```powershell
dotnet publish src/NexSpace.UI/NexSpace.UI.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None -p:EnableMsixTooling=false -p:WindowsAppSDKSelfContained=true -p:PublishTrimmed=false -o artifacts/phase4/unpackaged
```

Cerrar primero las instancias que usan el directorio de compilación. Conservar toda la carpeta, incluidos recursos y DLL; no distribuir solo el EXE. La modalidad self-contained intenta incorporar .NET y Windows App SDK; no elimina requisitos del sistema ni acredita funcionamiento en una máquina limpia. Esta última comprobación queda pendiente.

Publicación local verificada: 539 archivos, 244.157.411 bytes y manifiesto SHA256 comprobado. Se corrigió la omisión de XBF/PRI del publish inicial; cinco arranques breves corregidos terminaron con código 0. La prueba intensiva aún falla: el artefacto no está listo para distribución pública.

No se instalaron runtimes globales, certificados ni paquetes. MSIX x64 sin firma se generó correctamente, con una advertencia por herramienta de símbolos ausente; requiere firma con identidad autorizada y despliegue en un entorno limpio antes de ofrecerse a usuarios. No se activa Developer Mode. La generación no prueba instalación, actualización o desinstalación. [Documentación oficial de despliegue autocontenido](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).

## Actualización y retirada

Para unpackaged: cerrar NexSpace, realizar respaldo desde Perfiles y conservar la versión anterior en otra carpeta. Extraer la nueva versión en un directorio distinto; verificar hashes del manifiesto local si se suministra. Probar primero con `NEXSPACE_DATA_DIR` aislado. Nunca sustituir binarios mientras están en ejecución.

Los datos predeterminados están en `%LOCALAPPDATA%/NexSpace/data`; los registros en el directorio hermano `logs`. Retirar binarios no debe borrar la biblioteca. Desactivar inicio con Windows desde la aplicación solo si el usuario lo había autorizado; no tocar entradas de otras aplicaciones.

Rollback: SQLite v3 no debe abrirse con versiones que solo entienden v1/v2. Recuperar un respaldo compatible en un directorio nuevo y seleccionar explícitamente ese directorio. Conservar intacta la biblioteca reciente. No existe una conversión inversa silenciosa.

Artefactos y hashes locales quedan bajo `artifacts/`, excluidos de Git. No hay release, firma ni publicación de binarios autorizada en Fase 4.

Entrega local más reciente: carpeta `artifacts/phase4/candidate`, manifiesto `artifacts/phase4/candidate-manifest.json` (539 archivos, 244.162.583 bytes) y MSIX sin firma en `artifacts/phase4/msix-final`. Los artefactos previos son evidencia de iteraciones, no deben confundirse con la candidata final. La revisión humana no debe repetir carga intensiva mientras siga abierto el fallo de recursos.
