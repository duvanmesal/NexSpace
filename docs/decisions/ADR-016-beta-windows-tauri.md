# ADR-016 · Distribución Windows de la beta Tauri

**Estado** · Implementada en `experiment/tauri-nexcore`. Aceptación del instalador pendiente.

El piloto flotante prepara una biblioteca de demostración junto a los binarios y usa rutas de diagnóstico específicas de cada sesión. Distribuir ese layout permitiría mezclar datos y escribir en el destino de instalación. La beta necesita una identidad persistente y rutas propias.

El build selecciona `windows-beta` y genera NSIS x64 por usuario con la identidad `com.duvanmesal.nexspace`, el nombre `NexSpace` y el icono oficial de jade. React se incluye en el host Tauri. NexCore se publica autocontenido para `win-x64` y se ejecuta desde el subdirectorio `core` del paquete mediante el canal stdio existente. WebView2 Evergreen se utiliza cuando está instalado; el NSIS puede obtener el instalador de Microsoft si falta. La aplicación mantiene el modo FLOATING del ADR-015.

Los binarios se instalan por defecto en `%LOCALAPPDATA%\NexSpace`. Los datos, la caché de WebView2 y los logs se escriben en `%LOCALAPPDATA%\com.duvanmesal.nexspace`. El primer inicio crea un perfil válido y tres islas vacías. La beta comprueba SQLite antes de servir peticiones y escribe un respaldo en `backups` antes de una migración con cambio de esquema. El bloqueo por biblioteca impide abrir otra instancia que use esos mismos datos. La desinstalación conserva los datos salvo selección explícita de la opción de borrado.

Las actualizaciones son manuales mediante NSIS de versión superior, con los mismos nombre, publisher, identificador, modo de instalación y ruta de datos. Quedan pendientes la aceptación del reemplazo sobre una instalación real, el caso sin WebView2, el MSI y la firma. La firma Authenticode de Windows y la firma de artefactos para un futuro actualizador Tauri son controles distintos.

[Guía y matriz de aceptación de distribución](../../prototypes/web-shell/BETA_WINDOWS.md) · [Instaladores de Tauri](https://v2.tauri.app/distribute/windows-installer/) · [Actualizador de Tauri](https://v2.tauri.app/plugin/updater/).
