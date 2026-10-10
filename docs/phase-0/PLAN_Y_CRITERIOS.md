# Fase 0 · Viabilidad de integración con Windows 11

**Meta:** demostrar las bases de ventanas, modo flotante, posible modo integrado, recuperación y convivencia con Explorer. **No construir NexDock, gestión completa de archivos ni perfiles en esta fase.**

## Secuencia prevista
1. Inspeccionar Windows 11, Visual Studio, .NET, SDK y accesos disponibles. Documentar versiones reales.
2. Preparar solución modular mínima en `src/`; unit tests en `tests/`.
3. Crear una ventana de prueba WinUI 3; mostrarla, moverla, cambiar dimensiones y cerrarla.
4. Implementar primero modo flotante estable (sin modificación del Shell).
5. Investigar modo integrado con pruebas aisladas, reversibles y mediciones de foco/Z-order.
6. Probar interacción con Explorer, superposición con otras ventanas, pantalla completa, cambio DPI/monitor, reinicio de Explorer.
7. Probar consulta de visibilidad de iconos; **no cambiarla sin autorización explícita**.
8. Registrar evidencia, limitaciones y decisión ADR-006; detenerse.

## Matriz mínima
| ID | Caso | Criterio |
|---|---|---|
| P0-01 | Arranque WinUI 3 | Abre correctamente |
| P0-02 | Ventana sin chrome | Presentación de panel viable |
| P0-03 | Flotante | Interactiva, movable, closeable |
| P0-04 | Escritorio | No se superpone indebidamente a apps normales |
| P0-05 | Cambio de modo | No pierde estado; si no viable, indicar BLOCKED |
| P0-06 | Explorer | No bloquea escritorio/menu/iconos |
| P0-07 | Iconos originales | Consulta; cambio solo con autorización y reversión probada |
| P0-08 | Resolución/monitor | Panel recuperable |
| P0-09 | Reinicio Explorer | Recuperación segura |
| P0-10 | Salir | Explorer se mantiene operativo |

Para cada caso registrar **PASS / FAIL / BLOCKED / NOT_TESTED**, entorno real, pasos, observaciones y evidencia. Si no es posible automatizar una prueba visual, documentar verificación manual pendiente.

## Criterio de salida
- Compilación y ejecución comprobadas cuando el entorno lo permita.
- Modo flotante probado.
- Modo integrado probado **o** declarado no viable/no confirmado con fallback documentado.
- Ningún archivo personal modificado ni configuración de Windows cambiada sin permiso.
- ADR-006 registrado con recomendaciones.
- Esperar autorización para comenzar Fase 1.
