# ADR-010 · Perfiles transaccionales, NexThemes y adaptadores Shell

Fecha: 2026-10-11. Estado: implementado bajo autorización de Fase 3; aceptación visual integral pendiente. No modifica las decisiones aprobadas de FLOATING/DESKTOP.

## Decisión

Un solo proceso y composición compartida. Los perfiles son espacios manuales, nunca activados por la aplicación enfocada. SQLite v3 conserva las tablas v2 como **proyección activa autoritativa** y agrega profiles (identidad, nombre único, snapshot tipado) y profile_state (activo, predeterminado, restaurar último). Esto reutiliza contratos/servicios de contenedores sin dependencias circulares ni duplicar lógica Shell.

Cambiar de perfil: detener debounce y guardar geometría/sesión; cerrar solo ventanas propias; en una transacción capturar biblioteca activa, validar destino, archivar origen, reemplazar proyección activa y actualizar ID activo; restaurar ventanas y mantener Manager visible. Ante fallo SQLite revierte y UI restaura la proyección que permanezca confirmada. Un mutex por directorio impide dos escritores de la aplicación. No se soporta edición externa concurrente de la base.

Duplicar conserva IDs dentro del espacio (solo uno está activo), orden, etiquetas, preferencias y enlaces a contenedores. Las referencias físicas pueden coincidir entre perfiles; sus metadatos son independientes. Ninguna operación toca archivos originales. Guardado inmediato: no hay edición pendiente oculta; cancelar un diálogo no guarda. Eliminar requiere confirmación y rechaza activo/predeterminado. Límite 100 perfiles/1000 contenedores por snapshot.

## Alternativas y costes

Normalizar todas las relaciones por profile_id implica reconstruir restricciones y cambiar cada consulta v2. Los snapshots permiten una migración aditiva pequeña y transacciones verificables. Coste: cambio O(n) y JSON para perfiles inactivos; requiere pruebas con bibliotecas grandes antes de distribución. No es sincronización/cloud ni importación de archivos arbitrarios. La API de repositorio debe usarse a través del servicio; no interpretar snapshots como scripts SQL.

## Migración y recuperación

Antes de migrar una base v1/v2, BackupDatabase crea un .pre-v3-GUID.bak consistente; si falla, no migra. DDL en transacción. Datos/IDs v2 permanecen en Principal. Versiones futuras y JSON inválido se rechazan sin reset. La versión anterior de NexSpace rechaza v3: no downgrade sobre la base migrada.

El respaldo explícito crea data/backups/nexspace-fecha-GUID.db sin reemplazar otros respaldos. Recuperación conservadora: cerrar NexSpace, preservar la base fallida, copiar respaldo a **otro directorio** y abrirlo mediante NEXSPACE_DATA_DIR. Revisar ahí antes de decidir sustituir manualmente la biblioteca. No hay restauración automática sobre datos existentes ni recuperación de daños físicos garantizada. Los respaldos contienen rutas privadas; nunca publicarlos.

## Presentación

ThemeSettings validado/persistido por perfil; cuatro presets reales, acento, densidad de tinte, radio, espaciado y tres fuentes locales permitidas. NexThemeManager mantiene destinos débiles, aplica recursos/pinceles compartidos también a popovers y estilos nativos en ventanas abiertas. Alto contraste de Windows tiene prioridad. Opacidad afecta tinte Acrylic, no texto; fallback sólido si Windows deshabilita efectos. No se fuerzan ajustes globales. Movimiento reducido conserva las transiciones existentes condicionadas a UISettings.

Dock usa OverlappedPresenter.SetBorderAndTitleBar(false,false), grip propio, comandos de Manager/recuperar/cerrar, orientación explícita/automática, etiquetas opcionales y tamaños DIP. Auto-ocultamiento no se ofrece hasta validar foco y popovers: fallback estable siempre visible.

Manager usa NavigationView y siete secciones. ProfilesViewModel es portable; diálogos, ciclo nativo y selección viven en UI. No se afirma MVVM exhaustivo: construcción de formularios y adaptación de eventos siguen en code-behind, sin trasladar reglas de negocio allí.

## Shell y distribución

Bandeja: Shell_NotifyIconW v4, subclass de HWND propio, menú efímero con mostrar Manager/dock, perfiles, recuperar y salir. Dispose idempotente retira icono/subclass. TaskbarCreated vuelve a registrar solo su icono ante un reinicio externo de Explorer; esta entrega no reinicia Explorer. Icono compartido del sistema no se destruye. Si falla, Manager y hotkey permanecen disponibles.

Inicio Windows: adaptador **unpackaged** por valor NexSpace en HKCU Run, solo tras confirmación del usuario, reversible, nunca invocado automáticamente ni activado en pruebas. No sobrescribe valores ajenos: solo considera propio el comando exacto del ejecutable actual. La ruta debe ser estable; una build de desarrollo movida rompe el registro y exige revisar manualmente el valor anterior, que no se borra automáticamente. MSIX requiere un adaptador StartupTask y distribución firmada futura; no se certifica con el adaptador unpackaged.

Fuentes primarias: [BackupDatabase](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup), [chrome WinUI](https://learn.microsoft.com/windows/apps/develop/title-bar), [Shell_NotifyIconW](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw).
