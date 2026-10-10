# Registro de decisiones aprobadas · NexSpace

**Baseline:** v0.4. Decisiones de producto confirmadas, distintas de hipótesis de viabilidad técnica.

| ID | Decisión | Estado |
|---|---|---|
| DEC-01 | Sistema **híbrido**: contenedores integrados y flotantes | Aprobada funcionalmente |
| DEC-02 | Se admitirán accesos directos, aplicaciones, carpetas y archivos reales | Aprobada |
| DEC-03 | Al arrastrar un archivo se agrega **referencia por defecto**; no se mueve | Aprobada |
| DEC-04 | Se podrán **ocultar opcionalmente** los iconos originales con reversión sencilla | Aprobada funcionalmente |
| DEC-05 | Perfiles con **cambio manual**; no automáticos en primera versión | Aprobada |
| DEC-06 | **Windows 11 x64 primero**, Windows 10 fuera del alcance inicial | Aprobada |
| DEC-07 | C# + .NET LTS compatible, WinUI 3/Windows App SDK | Dirección técnica, versiones pendientes |
| DEC-08 | Monolito modular con MVVM y contratos por interfaces | Dirección aprobada |
| DEC-09 | SQLite local + JSON para importación/exportación | Dirección aprobada |
| DEC-10 | Técnica de integración detrás de ventanas y con Explorer | **Pendiente de investigación Fase 0** |
| DEC-11 | Windows y Explorer seguirán utilizables si NexSpace falla | Obligatorio |
| DEC-12 | **Opción visual C**: paneles discretos + dock opcional, estilo oscuro tipo vidrio como tema inicial | Aprobada como referencia conceptual |

## Distinciones importantes
- Soportar archivos reales no implica moverlos automáticamente.
- Quitar elemento de NexSpace no equivale a borrar el archivo del disco.
- El modo flotante es una función distinta del escritorio integrado.
- El diseño visual C es inspiración; las imágenes no son mockups pixel-perfect ni código.
- No está aprobada ninguna técnica interna específica de Explorer.
- La Fase 0 debe probar compatibilidad y definir una alternativa si el modo integrado no resulta estable.

## Control de cambios
Cualquier decisión nueva debe quedar documentada con fecha, motivo, consecuencias y aceptación explícita del usuario.
