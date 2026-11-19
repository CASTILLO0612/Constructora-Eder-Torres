# Integración con la API

La PWA consume la API bajo el mismo origen. En desarrollo, Vite o el servidor local de integración envían `/api` y `/salud` al proceso ASP.NET Core. En Linux, esta función corresponde al proxy Nginx.

## Sesión y protección CSRF

1. Consultar `GET /api/sesion/antifalsificacion`, conservando las cookies de la respuesta.
2. Enviar `POST /api/sesion/iniciar` con `nombreUsuario` y `contrasena`, y la cabecera/token devueltos por la consulta anterior.
3. Obtener un token nuevo después de autenticarse y enviarlo en cada POST, PUT, PATCH o DELETE.
4. Consultar el perfil en `GET /api/sesion/actual` y cerrar mediante `POST /api/sesion/cerrar`.

La cabecera de protección es `X-CET-CSRF`. La cookie de sesión es HttpOnly. La API comprueba la vigencia de la cuenta, sus permisos y la sesión en las solicitudes autenticadas.

Si cliente y API se alojan en orígenes distintos, configurar HTTPS, los orígenes CORS exactos y la política de cookies correspondiente. Las solicitudes del navegador deben conservar cookies con `credentials: 'include'`.

## Contratos

Scalar y `/openapi/v1.json` están disponibles en Development. `frontend/contrato-api.json` conserva una instantánea del contrato sin datos empresariales. Para regenerar tipos, operaciones, esquemas y catálogos del cliente, ejecutar `npm run contratos` dentro de `frontend`.

Los listados aceptan `pagina` y `tamanoPagina`; sus límites se configuran en `Api`. Las fechas de jornada usan `yyyy-MM-dd`; los instantes se transmiten en UTC con sufijo `Z`. La jornada de asistencia corresponde a `America/Managua`.

| Estado HTTP | Significado general |
|---|---|
| 400 | Contrato o protección CSRF inválidos. |
| 401 | Se requiere una sesión válida. |
| 403 | La sesión carece de permiso o alcance. |
| 404 | El recurso no existe. |
| 409 | Conflicto de estado, duplicidad o concurrencia. |
| 413 | Se excedió el tamaño permitido. |
| 429 | Se alcanzó un límite; respetar `Retry-After`. |
| 503 | Servicio o persistencia no disponibles temporalmente. |

Los errores usan Problem Details. El cliente muestra la descripción operativa y conserva el identificador para diagnóstico.

## Asistencia y operación offline

Las asignaciones propias se consultan en `GET /api/asistencias/mis-asignaciones`. `POST /api/asistencias/validar-ubicacion` comprueba si una lectura habilita la marcación. El servidor vuelve a validar la ubicación al registrar o sincronizar entrada y salida.

Se exige GPS y el criterio conservador `distancia al centro + precisión ≤ radio de la obra`. Las capturas conservan su instante original y toleran hasta dos minutos de adelanto del reloj. El GPS se obtiene para cada marcación; no se realiza rastreo continuo.

La PWA guarda las capturas compatibles en IndexedDB y las sincroniza mediante `POST /api/sincronizacion/lote`. Cada elemento mantiene su UUID y recibe una confirmación independiente. Un envío incierto debe consultarse o reenviarse con el mismo UUID. Los pendientes se conservan cuando vence la sesión; se requiere autenticación antes de transmitir.

El funcionamiento offline requiere que el dispositivo pueda obtener ubicación. El estado de conectividad del navegador no garantiza disponibilidad de la API ni señal GPS.

## Fotografías y diagnóstico

Las fotografías se descargan mediante `GET /api/seguimiento/evidencias/{id}/archivo`, sujeto a sesión, permisos y obra. La vista previa utiliza esa respuesta autenticada. Se validan tamaño, tipo, firma y hash del archivo. Las carpetas de evidencias no se publican como contenido estático.

`/salud/viva` comprueba la respuesta del proceso; `/salud/lista`, la disponibilidad de PostgreSQL. Una respuesta saludable no reemplaza las pruebas funcionales ni la comprobación de GPS en el dispositivo real.
