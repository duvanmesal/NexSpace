# Uso, persistencia y recuperación

NexManager administra una única biblioteca compartida con NexContainers y NexDock. Todos usan FLOATING. DESKTOP permanece experimental y deshabilitado.

Crear contenedores desde Manager y añadir referencias con selector. Una referencia no mueve ni modifica el recurso original. Abrir requiere una acción explícita y usa Windows Shell; un destino ausente debe mostrar un error recuperable. Las categorías del dock pueden vincular un contenedor o mantener sus propias referencias.

En Perfiles, crear o duplicar configuraciones independientes; cambiar de perfil guarda la sesión anterior, cierra sus ventanas y restaura la nueva. Los temas Claro, Contraste, Minimal y NexGlass comparten recursos. Materiales y animaciones respetan la configuración de Windows; el fallback sólido es válido y no acredita translucidez real.

Recuperación: utilizar el control de restauración de Manager, el dock o Ctrl+Alt+Shift+F12. Este mecanismo no requiere foco en un panel. No invoca integración DESKTOP. Cerrar Manager termina la sesión y sus ventanas; cerrar un contenedor no elimina sus referencias.

La biblioteca SQLite v3 guarda proyección activa y snapshots de perfiles. Cambio de perfil y persistencia usan transacciones; un fallo conserva el último estado confirmado, no reinicializa la biblioteca. Antes de migrar v1/v2 se crea un respaldo. La opción de respaldo explícito genera otro archivo, no sustituye el original.

Restaurar: cerrar todas las instancias; copiar únicamente el respaldo autorizado a **otro directorio de prueba**, con nombre `nexspace.db`, y ejecutar con `NEXSPACE_DATA_DIR` apuntando a él. Verificar perfiles, referencias y sesión antes de adoptar la copia. No sobrescribir una biblioteca personal ni ejecutar dos escritores sobre la misma ruta. El mutex local rechaza una segunda instancia por directorio.

Inicio con Windows es opt-in, requiere confirmación y no se habilita durante pruebas. Preferencias de restauración de sesión no equivalen a registro de inicio automático.

Registros y capturas pueden contener metadatos privados: no compartir la carpeta completa sin revisarla. Los nuevos eventos de diagnóstico de excepciones registran tipo, HRESULT y nombres de métodos, no mensajes, argumentos ni contenido de archivos.
