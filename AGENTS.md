# Instrucciones para agentes de código · NexSpace

## Contexto obligatorio
Antes de planificar o editar archivos, leer:
- `docs/requirements/NEXSPACE_ESPECIFICACION_COMPLETA.md`
- `docs/decisions/DECISIONES_APROBADAS.md`
- `docs/phase-0/PLAN_Y_CRITERIOS.md`
- `assets/references/README.md`
Visual principal: `assets/references/concepts/03_C_sistema_hibrido_APROBADO.png`.

## Alcance y seguridad
- **Solo Fase 0** hasta aprobación explícita del usuario. No pasar a Fase 1 por iniciativa propia.
- No reemplazar Explorer, modificar claves del sistema o cambiar la visibilidad de iconos sin aprobación explícita.
- No mover, copiar, renombrar o borrar archivos personales para pruebas. Usar solamente archivos de prueba aislados.
- Probar primero ventana flotante; evaluar integración al escritorio como hipótesis técnica, con alternativa estable si falla.
- Las operaciones de drag-and-drop deben agregar **referencias por defecto**, nunca mover los originales.
- No declarar compatibilidad con escritorio integrado sin validación real en Windows 11.
- No considerar compilación exitosa como verificación completa.
- Registrar evidencia de pruebas: PASS, FAIL, BLOCKED o NOT_TESTED. No fabricar capturas.
- Evitar dependencias no documentadas de WorkerW/Explorer como única estrategia viable.

## Convenciones
- C# y nombres de clases en inglés; documentación de producto en español.
- Componentes aislados por interfaces; MVVM; arquitectura monolítica modular.
- Anotar versiones reales y decisiones nuevas en `docs/decisions/`.
- No modificar las decisiones aprobadas sin solicitar aprobación.
- Cuando termines Fase 0, resumir evidencia, riesgos y bloqueos; detenerte para revisión humana.
