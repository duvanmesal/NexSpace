# Reproductor independiente · ciclo de vida WinUI

No referencia ningún proyecto, servicio, tema, tile, icono, base de datos ni coordinador de NexSpace. Solo depende de Windows App SDK 2.5.1 y .NET 10. Usa una ventana anfitriona y crea/cierra 100 `Window` con `Grid` vacío. No crea timers de DispatcherQueue, backdrops, handlers de ventanas hijas ni regiones de título. No fuerza GC ni libera compositores que pertenecen a XAML.

Publicar desde la raíz:

```powershell
dotnet publish tools/WinUiWindowLifetimeRepro/WinUiWindowLifetimeRepro.csproj -c Release -p:Platform=x64 -o artifacts/phase41/independent
```

Ejecutar el EXE publicado, esperar aproximadamente 55 segundos y comprobar su salida. Cada ejecución escribe `result.json` en un directorio nuevo `%TEMP%/NexSpace-Phase4-independent-<guid>`. `REPRO_NO_CONTENT=1` omite incluso el Grid de las ventanas hijas; `REPRO_CYCLES` permite 1–300 (100 por defecto). Mantener un timeout externo de 120 segundos para 100 ciclos; si el dispatcher se bloquea, el límite interno no puede ejecutarse.

Las muestras incluyen handles totales, memoria privada, Working Set, GDI/USER y generaciones GC. El censo `NtQueryObject` es de solo lectura, únicamente del proceso actual, acotado a valores menores de 131072 y no atómico: puede infracontar, no es una enumeración oficial completa. No consulta nombres de objetos. Comprueba `IsWindow=false` después de cada cierre; las muestras inicial/final incluyen la anfitriona todavía abierta. Finalmente cierra la anfitriona normalmente.

Límites internos: 180 s, 5500 handles, 512 MiB privados, 768 MiB WS, 4000 GDI y 2000 USER. Solo es una herramienta diagnóstica; no forma parte de la solución/distribución de producto. No adjuntar resultados privados a Git.

Resultado 2026-10-12, Windows 11 Pro 26300: ambas variantes completaron 100 ciclos y salieron con código 0. `Composition` pasó de 5 a 105 y `Event` de 1269 a 1376 después de 30 segundos de reposo. GDI y USER permanecieron en 9 y 29. El síntoma existe sin NexSpace; esto no identifica la función nativa responsable ni demuestra que toda la memoria del producto tenga la misma causa. Ver `docs/testing/PHASE41_PERFORMANCE.md`.
