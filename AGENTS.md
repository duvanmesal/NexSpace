# Instrucciones para agentes de código · NexSpace

## Contexto obligatorio
Antes de planificar o editar archivos, leer:
- `docs/requirements/NEXSPACE_ESPECIFICACION_COMPLETA.md`
- `docs/decisions/DECISIONES_APROBADAS.md`
- `docs/phase-0/PLAN_Y_CRITERIOS.md`
- `assets/references/README.md`
Visual principal: `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

## Alcance y seguridad
- La **Fase 1 está aceptada con alcance condicionado** (2026-10-11). Conservar pendientes: drag-and-drop físico, accesibilidad, DPI/multimonitor, translucidez, animaciones y perfilado de memoria. No declararlos PASS sin evidencia.
- La **Fase 2 está aceptada condicionalmente** (2026-10-11). Conservar su matriz y pendientes, incluido el fallo nativo de arranque sin causa demostrada.
- La **Fase 3 está autorizada**: perfiles manuales, temas, evolución de Dock/Manager, bandeja, preferencias de inicio opcionales y migraciones seguras. Compartir servicios y ciclo de vida. Commits de Fase 3 únicamente locales; **no iniciar Fase 4**.
- Implementación de Fase 3 entregada para revisión el 2026-10-12, sin aceptación integral: consultar PHASE3_VALIDATION.md y PHASE3_PERFORMANCE.md antes de ampliar afirmaciones de compatibilidad o distribuirla.
- No habilitar inicio con Windows sin autorización adicional. No ejecutar HWND_BOTTOM, reiniciar Explorer, cambiar sus opciones/iconos ni activar Developer Mode.
- Fase 0 tiene alcance condicionado: FLOATING es la base funcional aprobada; DESKTOP sigue experimental, desacoplado y **deshabilitado**. Todos los contenedores, NexDock y NexManager deben usar FLOATING. No ejecutar integración de escritorio ni habilitarla por defecto.
- No reemplazar Explorer, modificar claves del sistema o cambiar la visibilidad de iconos sin aprobación explícita.
- No mover, copiar, renombrar o borrar archivos personales para pruebas. Usar solamente archivos de prueba aislados.
- NexContainers administra referencias: agregar por selector o drag-and-drop no debe mover, copiar, renombrar ni borrar el original. Quitar referencias o contenedores solo elimina metadatos locales.
- Abrir un recurso requiere acción explícita del usuario y debe delegarse a las asociaciones de Windows; errores y destinos ausentes deben manejarse sin tumbar la aplicación.
- No declarar compatibilidad con escritorio integrado sin validación real en Windows 11. El hecho de que una ventana FLOATING funcione no valida DESKTOP.
- No considerar compilación exitosa como verificación completa.
- Registrar evidencia de pruebas: PASS, FAIL, BLOCKED o NOT_TESTED. No fabricar capturas.
- Evitar dependencias no documentadas de WorkerW/Explorer como única estrategia viable.

## Convenciones
- C# y nombres de clases en inglés; documentación de producto en español.
- Componentes aislados por interfaces; MVVM; arquitectura monolítica modular.
- Mantener dependencias unidireccionales: Domain independiente, Application con contratos, Infrastructure/Windows como adaptadores y UI como raíz de composición/presentación.
- Anotar versiones reales y decisiones nuevas en `docs/decisions/`.
- No modificar las decisiones aprobadas sin solicitar aprobación.
- Al cerrar Fase 1, actualizar propuesta, arquitectura, ADR aplicable y matriz de validación. Separar pruebas automatizadas, smoke tests y evidencia manual reportada/pendiente; dar pasos precisos para validación visual.
- Al terminar Fase 3, resumir evidencia, migraciones, rendimiento, riesgos y pruebas pendientes; detenerse antes de Fase 4. No habilitar inicio Windows durante pruebas.
