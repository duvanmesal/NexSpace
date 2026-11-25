# NexSpace Test Center

Desde la raíz del repositorio, abre el menú en Windows PowerShell 5.1 o PowerShell 7. El centro requiere Python 3.11 o posterior. Los ensayos de rendimiento que ya dependen de PowerShell 7 lo indican en el catálogo.

```powershell
.\tools\NexSpace-Test.ps1
.\tools\NexSpace-Test.ps1 -List
.\tools\NexSpace-Test.ps1 -Test build-release
.\tools\NexSpace-Test.ps1 -Suite smoke
.\tools\NexSpace-Test.ps1 -Suite regression
.\tools\NexSpace-Test.ps1 -Suite beta-readiness
```

El menú muestra el ID, nombre, tipo, último estado y fecha de cada prueba. Un número ejecuta la prueba, **H** abre su historial, **V** revalida una sesión guiada y **S** sale. `-List` devuelve JSON para automatizaciones. `-TimeoutSeconds` sustituye el límite de catálogo por prueba, o por miembro cuando se usa en una suite.

## Arrastre físico

```powershell
$ensayo = .\tools\NexSpace-Test.ps1 -Test explorer-drop | ConvertFrom-Json
$ensayo | Select-Object id, hostPid, profile, group, sourceFolder, nextAction
$ensayo.details.instructions
.\tools\NexSpace-Test.ps1 -Verify -Session $ensayo.id
.\tools\NexSpace-Test.ps1 -Report -Session $ensayo.id
# Cuando termines con el host de esa sesión
.\tools\NexSpace-Test.ps1 -Stop -Session $ensayo.id
```

El inicio valida el build, abre un único host y muestra la carpeta de prueba en Explorer. Selecciona **Proyecto**, **Acceso.lnk**, **Muestra.pdf** y **Notas de prueba.txt** en esa carpeta y suéltalos juntos dentro de **Archivos**. Hasta que exista evidencia del gesto, la sesión devuelve **BLOCKED**. Un piloto abierto bloquea el inicio de otro. `-Stop` solo solicita el cierre ordinario al host registrado.

El primer ensayo físico aceptado se puede consultar incluso fuera del equipo original. Si los archivos originales siguen accesibles, `-Verify` vuelve a comprobarlos. Si se perdieron, la revalidación devuelve **BLOCKED** y el historial conserva el PASS original.

```powershell
.\tools\NexSpace-Test.ps1 -Report -Session bcbf8d92f0094c6cb2dbc4c4be989051
.\tools\NexSpace-Test.ps1 -Verify -Session bcbf8d92f0094c6cb2dbc4c4be989051
```

## Recuperación desde la bandeja

```powershell
$ensayo = .\tools\NexSpace-Test.ps1 -Test windows-tray | ConvertFrom-Json
$ensayo | Select-Object id, buildId, hostPid, sidecarPid, trayTrace
$ensayo.details.instructions
```

1. Enfoca **NexDock** y pulsa **Alt+F4**. Comprueba que desaparece.
2. Abre el menú del icono **NexSpace · ensayo de aceptación** en la bandeja de Windows y selecciona **Recuperar ventanas**. Comprueba que NexDock reaparece.
3. Ejecuta `.\tools\NexSpace-Test.ps1 -Verify -Session $ensayo.id`. Si abriste otra terminal, usa el ID de la sesión en lugar de `$ensayo.id`. Puedes consultar `-Report -Test windows-tray` para localizarlo. No vuelvas a ejecutar `-Test` para verificar el mismo recorrido.

El verificador exige una transición de visible a oculto, una solicitud nativa del menú mientras el dock sigue oculto y una confirmación de visibilidad tras esa misma solicitud. Vincula la traza al recibo del host, build y sesión. Hasta completar el recorrido, el resultado es **BLOCKED**. La prueba de recuperación y la prueba de cierre `windows-close` tienen criterios propios.

## Informes e historial

```powershell
.\tools\NexSpace-Test.ps1 -Report -Test explorer-drop -Format JSON
.\tools\NexSpace-Test.ps1 -Report -Suite regression
.\tools\NexSpace-Test.ps1 -Report -Build BUILD_ID
.\tools\NexSpace-Test.ps1 -Report -From 2026-11-24 -To 2026-11-25
.\tools\NexSpace-Test.ps1 -Report -Session ID -Format Markdown -Output .\artifacts\resultado.md
```

Los filtros de fecha usan UTC. Una fecha final sin hora incluye todo ese día. `-Output` escribe un archivo nuevo. JSON y Markdown comparten los mismos datos de sesión. La duración del reporte local incluye la espera hasta la verificación. El resumen histórico del arrastre mide hasta el cierre del lote OLE. Las rutas locales pueden contener nombres privados. Para Git usa únicamente resúmenes revisados y sanitizados como [accepted-scenarios.json](accepted-scenarios.json).

Las sesiones se registran en `artifacts/test-center/sessions/<id>` con `session.json`, `result.json`, intentos en `history` y logs por etapa. Cada suite guarda los ID de sus miembros. Las pruebas gestionadas enlazan el recibo del lanzador, el build y la base de la sesión. Los bancos automáticos guardan su identidad de ejecución y datos de adaptador cuando existen. Los campos que no corresponden al tipo de prueba quedan vacíos. En el arrastre se archivan localmente el recibo, el veredicto original y la traza pequeña. Los binarios, SQLite y los perfiles WebView2 permanecen en su ubicación original. El historial persiste al cerrar el piloto y el centro no borra sesiones ni evidencias automáticamente.

## Catálogo y suites

[catalog.json](catalog.json) describe los pasos de preparación, ejecución y verificación, los requisitos y criterios. Los tests automáticos sin comando separado de verificación comprueban sus criterios dentro del script. Los casos físicos pendientes aparecen con su condición de ejecución y próxima acción. Los bancos A/B históricos que dependen de los binarios del laboratorio están descritos en el [README del piloto](../../prototypes/web-shell/README.md).

- **smoke** comprueba herramientas, procedencia y contrato esencial.
- **regression** añade .NET Debug/Release, reinicio, cierre normal y recuperación de colocación.
- **beta-readiness** incluye pruebas de Windows, rendimiento y acciones físicas. Las pruebas manuales salen **BLOCKED** en la suite y las demás continúan. El arrastre histórico aparece en el historial y conserva el alcance del build probado.

Los comandos de ejecución devuelven **0 PASS**, **1 FAIL**, **2 BLOCKED**, **3 ERROR** o **4 NOT_TESTED**. Una suite solo pasa si todos sus miembros obligatorios pasan. Los informes muestran criterios aprobados, fallos, bloqueadores, duración, build, evidencia y siguiente acción.
