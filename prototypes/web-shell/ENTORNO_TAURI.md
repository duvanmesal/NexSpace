# NexSpace Tauri · Compilar, abrir y verificar un ensayo

Desde la raíz de NexSpace, en Windows PowerShell 5.1 o PowerShell 7:

```powershell
.\prototypes\web-shell\Build-Pilot.ps1 -CheckOnly
.\prototypes\web-shell\Build-Pilot.ps1
$ensayo = .\prototypes\web-shell\Run-Pilot.ps1 -DropTrial | ConvertFrom-Json
$ensayo
```

`-CheckOnly` valida requisitos y prueba el enlazador con un PE x64 antes de instalar dependencias o compilar React. El script busca primero en el PATH y luego en ubicaciones de instalación habituales. También consulta `artifacts/toolchain`, `artifacts/web-lab` y la instalación del laboratorio A/B identificada por su rama en `git worktree list`. La última ruta es una alternativa de descubrimiento y puede sustituirse con `-ToolchainRoot` o `NEXSPACE_TOOLCHAIN_ROOT`. Para instalaciones independientes se pueden indicar `-Cargo`, `-Rustc`, `-GnuBin`, `-DotNet`, `-Node`, `-Npm` y `-Python`. El entorno del subproceso se restaura al salir.

El fallo original de Cargo se reprodujo porque el binario existía en la instalación privada del laboratorio y no estaba en el PATH de una terminal nueva. En el equipo de prueba se detectaron Node x64 24.22.0, npm 11.17.0, Python x64 3.14.0, Windows PowerShell 5.1, PowerShell 7.6.9, Rust/Cargo 1.100.0 para `x86_64-pc-windows-gnu`, MinGW GCC 16.2.0, SDK .NET 10.0.401 y WebView2 Evergreen 157/158. Windows App SDK 2.5.1 está en la caché NuGet y pertenece a la variante WinUI. La compilación Tauri usa WebView2 y NexCore autocontenido. La ruta GNU sigue activa. Una instalación Rust MSVC x64 debe tener su enlazador operativo en la sesión y superar la misma prevalidación.

## Compilación y procedencia

- Cada compilación tiene una carpeta bajo `artifacts/tauri-nexcore/builds`. `build.json` incluye SHA Git, indicador de cambios locales, huella de los archivos de entrada, hora UTC, arquitectura, versiones, validaciones y hashes SHA-256 del EXE, el cargador WebView2 y los archivos de NexCore. El host incorpora el identificador en el EXE.
- El orden es preflight, `npm ci`, TypeScript/Vite, Cargo con `--locked`, publicación .NET x64 autocontenida, comprobación PE, preparación SQLite y consulta IPC al sidecar publicado. Un fallo marca `build-status.json` como inválido. Los artefactos previos se conservan. El lanzador comprueba la revisión, la huella de entrada y los hashes de los archivos antes de abrir la última compilación verificada.
- El resultado es reproducible desde el código y los lockfiles con las herramientas registradas. Cada compilación incorpora un ID nuevo. Eso cambia el hash del EXE aunque las entradas sean idénticas.

## Sesiones y cierre

`Run-Pilot.ps1` crea una base aislada por arranque. `-DemoDatabase` copia una biblioteca de demostración desde una conexión SQLite de solo lectura. Por defecto el lanzador rechaza un segundo piloto si encuentra un proceso activo. `-AllowParallel` permite una decisión explícita y cada sesión recibe su propia base y su propio directorio WebView2. Para instancias antiguas que arrancaron fuera del lanzador, la procedencia de la biblioteca se informa como desconocida.

La salida muestra el ID de sesión, el build y su hora UTC, la ruta del EXE, los PID de host y NexCore, el perfil, el contenedor de destino, la base real y las rutas de evidencias. El host escribe `ready.json` después de recibir el manifiesto de NexCore y el lanzador verifica la relación entre ambos procesos. Las evidencias se guardan en la carpeta temporal de la sesión, bajo `evidence`. El recibo local queda en `artifacts/tauri-nexcore/sessions`. Las bases, trazas, rutas locales y capturas quedan fuera de Git.

```powershell
.\prototypes\web-shell\Run-Pilot.ps1 -Status
.\prototypes\web-shell\Run-Pilot.ps1 -Status -SessionId $ensayo.session
.\prototypes\web-shell\Run-Pilot.ps1 -StopSession $ensayo.session
```

La salida desde la bandeja sigue disponible. `-StopSession` valida la identidad del proceso, solicita su salida ordinaria y comprueba que el árbol observado terminó. Si el cierre se atasca, informa del fallo y conserva el proceso para diagnóstico.

## Arrastre físico reproducible

1. Ejecutar `Run-Pilot.ps1 -DropTrial`. Se abre únicamente la carpeta **Arrastrar a Archivos**, con el TXT, el PDF, el acceso directo y la carpeta Proyecto. El mecanismo de accesos directos busca primero `pwsh.exe` y recurre a `powershell.exe`, incluido el instalado en Windows. Se puede fijar `-PowerShell powershell.exe`.
2. Colocar Explorer al lado de **Archivos** en la ventana del piloto. Seleccionar los cuatro elementos, arrastrarlos al interior de la tarjeta y soltar una vez.
3. Ejecutar `python prototypes/web-shell/bench/Explorer-Drop.py --verify ID_DE_SESION`. El script encuentra su propio `drop.jsonl` y comprueba sesión, build, perfil, contenedor, rutas, tipos, hashes y lote OLE. Guarda `drop-verdict.json` en las evidencias. Usa 0 para PASS, 1 para FAIL y 2 para BLOCKED. Una referencia añadida por el selector o por el banco de IPC no acredita un gesto.

La traza vincula el destino y sus coordenadas físicas y lógicas, el DPI, el recuento, la validación de cada ruta, el resultado de NexCore y el cierre del lote. El registro privado del sidecar distingue `ole-drop`, `picker` y `test-ipc` en sus altas. El bloqueo por sesión impide abrir dos hosts con la misma base gestionada.

## Diagnóstico de los arrastres anteriores

La trazabilidad histórica confirmó un lote OLE real de cuatro elementos en **Archivos** el 24 de noviembre de 2026 a las 16:30:39 UTC. Los cuatro tiempos de alta en SQLite caen entre la recepción y el cierre del lote con cuatro respuestas satisfactorias de NexCore. Los cuatro destinos correctos procedían de la carpeta de otro ensayo. El verificador anterior comparaba únicamente las rutas de la carpeta del ensayo destino. Al usar la carpeta que realmente originó el gesto, los cuatro tipos y los hashes coinciden. Para comparar el árbol histórico se normalizaron los separadores de las rutas relativas. Esto explica la discrepancia sin cambiar el criterio del ensayo nuevo, que exige una sola identidad.

## Validación del entorno

| Caso | Estado | Evidencia |
|---|---|---|
| PowerShell 5.1 con PATH sin toolchain local | PASS | Prevalidación, compilación Release y lanzamiento. Creación del .lnk con Windows PowerShell. |
| Cargo explícito inexistente | PASS | Terminó con error, dejó el marcador `failed` y el lanzador rechazó iniciar. |
| Identidad y artefacto obsoleto | PASS | El lanzador rechazó una huella de entrada diferente. El arranque ordinario comprobó build, base y parentesco del sidecar. |
| Sesiones duplicadas y cierre | PASS | Se rechazó un segundo piloto por omisión. El cierre ordinario comprobó los 12 procesos observados y devolvió cero residuales. |
| TypeScript, Rust, .NET y verificador | PASS | Pasaron TypeScript/Vite, dos pruebas Rust, 127 .NET por configuración y cinco pruebas Python de detección y correlación. Se conservó el aviso GNU de manifiestos múltiples. |
| Persistencia e iconos | PASS | `Test-Floating.py --shell-icons` comprobó referencias, PNG, perfiles, geometría y reinicio del sidecar por EOF. |
| Arrastre histórico reconstruido | PASS | Cuatro rutas y tipos correlacionados con una traza OLE histórica. Los originales coinciden con los hashes previos. |
| Nuevo ensayo trazado de extremo a extremo | BLOCKED | La comprobación previa al gesto devolvió 0 de 4 referencias, hashes intactos y ausencia de drop. La herramienta de escritorio admite arrastre dentro de una ventana. |
| Toolchain MSVC | NOT_TESTED | El equipo ensayado utilizó GNU x64. |
