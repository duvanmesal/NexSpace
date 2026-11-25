# Auditoría de acciones del piloto Tauri

## Regresión de selección

En `e76b8eb`, el botón ejecutaba su acción y el formulario o la ruta IPC se encargaban de cerrar el menú. En `14da416`, el cierre pasó a `onClickCapture` del panel. Una traza del ejecutable original registró `pointerdown` y `mouseup` con el menú presente. Durante la captura de `click`, React solicitó desmontar el menú y devolvió el foco al disparador. Al llegar al final de la propagación, el botón de la opción ya estaba desmontado. Renombrar dejó el diálogo ausente y Mostrar lista conservó la cuadrícula. WebView2 no registró excepciones en esa reproducción.

La corrección ejecuta el manejador del botón antes del cierre en la fase de burbujeo. El clic exterior, Escape y los avisos de foco siguen usando sus rutas de descarte. El cierre sucede en la selección, y una respuesta tardía de NexCore deja en su sitio un menú abierto posteriormente. La regresión de Edge envía clics de navegador fuera de `act()` para comprobar los efectos de las opciones.

## Inventario de acciones

| Superficie y acción | Activación y destino | Resultado visible | Alcance de `14da416` |
|---|---|---|---|
| Isla y popover · Añadir archivos o accesos / Añadir carpeta | Clic en Opciones → Tauri `pick` → NexCore `add` al aceptar | Selector nativo del tipo elegido y referencias nuevas en el grupo o categoría | Botones desmontados en la fase de captura |
| Isla · Mostrar lista / Mostrar cuadrícula | Clic en Opciones → `library:listView` | Cambia la presentación de las referencias | Cuadrícula inmóvil en la reproducción |
| Isla · Bloquear / Desbloquear posición | Clic en Opciones → `library:lock` | Cambia la etiqueta y se habilitan o bloquean las interacciones de posición | Botones desmontados en la fase de captura |
| Isla · Mover a otra pantalla | Clic en Opciones → Tauri `frame:next-monitor` → NexCore `placement` | Cambia el monitor y la colocación guardada | Botón desmontado cuando aparece |
| Isla · Renombrar contenedor / Quitar contenedor | Clic, luego Guardar o Quitar → `renameGroup` / `deleteGroup` | Editor o confirmación, luego título actualizado o isla retirada | La captura antecedía a la apertura del formulario |
| Archivo en isla o popover · Renombrar | Clic derecho o Shift+F10, luego clic en Renombrar → editor y `library:rename` al guardar | Editor precargado, luego nombre de la referencia actualizado | Editor ausente en la reproducción |
| Archivo en isla o popover · Mover antes / Mover después | Clic en la opción → `library:moveItem` | Cambia el orden en la vista | Botones desmontados en la fase de captura |
| Archivo en isla o popover · Quitar referencia | Clic, luego Quitar → `library:remove` | Confirmación y retirada de la referencia | La captura antecedía a la confirmación |
| Isla y popover · Abrir referencia, paginar, contraer o expandir | Clic en ficha → `library:open`; paginación → `page`; encabezado de isla → `collapse` | Se abre el destino por Windows, cambia la página o el tamaño visible de la isla | Controles fuera del manejador de captura |
| NexDock · Categoría / Organizar espacio y perfiles | Clic en el dock → Tauri `surface:category` / `surface:organize` | Aparece o se retira el popover, cambia el estado activo o se abre Espacio de trabajo | Controles fuera del manejador de captura |
| Popover · X o Escape | Clic en X o Escape → Tauri `surface:dismiss` cuando el menú está cerrado | Se retira el popover y vuelve el foco al dock | Control fuera del manejador de captura |
| Espacio de trabajo · Nuevo contenedor, Ocultar, Mostrar | Formulario confirmado o clic → `library:createGroup` / `library:show` | Entrada nueva e isla creada, oculta o visible | Controles fuera del manejador de captura |
| Espacio de trabajo · Nueva categoría, Renombrar, Vincular, Subir/Bajar, Quitar | Formulario, selector confirmado o clic → `createCategory`, `editCategory`, `moveCategory`, `deleteCategory` | Cambian el dock, el orden, el vínculo o las entradas visibles | Controles fuera del manejador de captura |
| Espacio de trabajo · Orientación del dock | Clic → `library:vertical` | Dock horizontal o vertical | Control fuera del manejador de captura |
| Espacio de trabajo · Nuevo perfil, Duplicar, Renombrar y Activar | Formulario o clic → `createProfile`, `renameProfile`, `switchProfile` | Cambian la lista, el perfil activo y sus grupos | Controles fuera del manejador de captura |
| Espacio de trabajo · Recuperar ventanas, Cerrar, Salir | Clic o Escape → Tauri `surface:recover`, `surface:dismiss`, `surface:quit` | Ventanas restauradas, panel cerrado o piloto cerrado | Controles fuera del manejador de captura |

El menú contextual del piloto ya ofrecía operaciones sobre referencias. Abrir el destino se hace desde la ficha y la opción Mostrar ubicación todavía no está en ese menú. Los botones de orden de archivos ya estaban disponibles en los extremos de la lista, donde NexCore puede responder `not-movable`. Mover a otra pantalla sigue disponible para una isla desbloqueada con más de una pantalla. Las acciones de renombrar, ordenar y quitar se aplican a los metadatos de NexSpace.

La prueba automática de menús comprueba el editor y su cancelación, el cambio a lista, la llegada del clic exterior al archivo, el orden de Escape, la acción asíncrona y los comandos del dock. La [regresión del contrato](bench/Test-Floating.py) comprueba las mutaciones en una biblioteca aislada. La [aceptación física](ENTORNO_TAURI.md#cierre-de-los-menús-de-las-islas) cubre el foco entre ventanas y los selectores de Windows.
