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

## Convivencia con Explorer

```powershell
$ensayo = .\tools\NexSpace-Test.ps1 -Test windows-explorer | ConvertFrom-Json
$ensayo | Select-Object id, buildId, hostPid, sidecarPid, sourceFolder, evidenceDirectory
$ensayo.details.instructions
```

1. En la ventana que abrió el lanzador, entra a **01-Durante**. Selecciona `Notas.txt` y abre y cierra su menú contextual. Haz lo mismo con el menú contextual de una zona libre del escritorio. En NexDock abre y cierra un popover y en **Archivos** contrae y expande el contenedor. Vuelve a la primera carpeta en la misma ventana de Explorer.
2. Registra lo observado con `-Observe -Session $ensayo.id -Phase During -Outcome Pass -Note 'TU OBSERVACIÓN'`. Mientras el comando espera, pon **01-Durante** en primer plano y deja el puntero en su contenido. Confirma `checkpointRecorded: true` antes de cerrar el piloto.
3. Selecciona **Cerrar NexSpace experimental** en el menú de **NexSpace · ensayo de aceptación**. En la misma ventana de Explorer vuelve a la carpeta raíz, abre **02-Despues**, selecciona `Notas.txt` y comprueba que el menú contextual responde. Conserva intactos los archivos de prueba.
4. Registra esas observaciones con `-Observe -Session $ensayo.id -Phase After -Outcome Pass -Note 'TU OBSERVACIÓN'`. Devuelve el foco a **02-Despues** y deja el puntero en su contenido hasta que el comando confirme el punto de control. Ante un problema, usa `-Outcome Fail` con una nota que describa qué ocurrió. Las notas son privadas.
5. Ejecuta `.\tools\NexSpace-Test.ps1 -Verify -Session $ensayo.id`. Puedes repetir la verificación con el mismo ID aun después de cerrar el piloto.

Los puntos de control separan lo que observó la persona de lo que observó Windows. El verificador exige el mismo escritorio de Explorer desde antes de abrir el piloto y la misma ventana de documentos en ambos puntos de control. También comprueba el foco, la ventana bajo el puntero en el punto muestreado, el cierre desde el menú nativo, el fin de los procesos observados y los hashes de los archivos aislados. La aceptación cubre el recorrido físico realizado. [Identidad del escritorio](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getshellwindow) · [Ventana bajo un punto](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint) · [Ventanas de Explorer](https://learn.microsoft.com/en-us/windows/win32/shell/shell-windows)

## Teclado, UI Automation y Narrador

Abre el ensayo con `.\tools\NexSpace-Test.ps1 -Test windows-accessibility`. El piloto abre una biblioteca aislada y guarda una primera captura de UI Automation en la sesión. Los PASS históricos de la matriz conservan el alcance del build en el que se observaron.

1. Activa **NexDock** con **Alt+Tab** o con un clic en un espacio libre de la barra. Desde ahí, usa **Tab** y **Shift+Tab** para recorrer **Organizar espacio y perfiles**, **Archivos**, **Juegos** y **Aplicaciones**. Comprueba que el foco distingue un solo control, se mueve en ambas direcciones y no salta elementos.
2. Con **Juegos** enfocado, pulsa **Enter**. Debe abrirse su popover. Usa Tab para llegar a **Opciones** y pulsa **Espacio**. Debe aparecer el menú con foco en una opción. Pulsa **Escape** y comprueba que vuelve a Opciones.
3. Enfoca **Controles.txt** con Tab o Shift+Tab. Pulsa **Shift+F10**. Debe abrirse el menú de la referencia, con opciones como **Renombrar**. Pulsa Escape y comprueba que vuelve a Controles.txt. Con ambos menús cerrados, pulsa Escape otra vez. El popover debe cerrarse y el foco debe volver al botón **Juegos** del dock. Repite la comprobación del menú contextual en la isla flotante **Juegos** y verifica el foco sobre la misma referencia.
4. Reabre el popover **Juegos** y deja sus menús cerrados. Vuelve a PowerShell para registrar `Keyboard`. Describe los resultados de Tab, Shift+Tab, el indicador de foco, Enter, Espacio, Shift+F10 y los retornos de Escape.
5. Activa Narrador con **Windows+Ctrl+Enter**. Repite el recorrido por el dock, la isla Juegos y su popover. Escucha el nombre, el rol y el estado cuando corresponda en **Juegos** del dock, **Opciones**, **Cerrar categoría** y **Controles.txt**. Si quieres cotejar palabras concretas, usa **Narrador+Alt+X** para ver el resumen de voz y anota solo frases del piloto. Mantén Narrador activo y deja el popover abierto, con los menús cerrados, mientras registras `Narrator`. [Atajos de Narrador](https://support.microsoft.com/es-es/accessibility/windows/narrator/appendix-b-narrator-keyboard-commands-and-touch-gestures)

La isla y el popover tienen el mismo título accesible **Juegos**. UI Automation los enumera como ventanas separadas y el botón **Cerrar categoría** identifica el popover. Registra si el título repetido te impide distinguirlos con Narrador.

```powershell
$id = 'ID DE LA SESIÓN'
.\tools\NexSpace-Test.ps1 -Observe -Session $id -Phase Keyboard -Outcome Pass -Note 'Describe los controles, teclas y retornos de foco observados'
.\tools\NexSpace-Test.ps1 -Observe -Session $id -Phase Narrator -Outcome Pass -Note 'Anota los nombres, roles o estados que anunció Narrador'
.\tools\NexSpace-Test.ps1 -Verify -Session $id
```

Usa `-Outcome Pass` cuando completes la fase descrita. Ante un defecto, usa `-Outcome Fail` y anota el control, la tecla o el anuncio y el resultado esperado. Si falta un requisito, usa `-Outcome Blocked` y explica cuál. Una observación guardada muestra `checkpointRecorded: true`. El verificador exige las dos observaciones y las capturas nativas asociadas al mismo build y sesión. La detección del proceso Narrador comprueba que estaba activo en la sesión de Windows durante la captura. Tus notas aportan lo que se vio y escuchó. Conserva las evidencias en la carpeta privada de la sesión.

Tras registrar y verificar las fases, puedes dejar Narrador en su estado anterior y cerrar el piloto con `.\tools\NexSpace-Test.ps1 -Stop -Session $id`.

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
