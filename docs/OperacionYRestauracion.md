# Operación y restauración

## Configuración por entorno

| Configuración | Finalidad |
|---|---|
| `ConnectionStrings:Constructora` | Conexión de ejecución a PostgreSQL. |
| `AllowedHosts` | Hosts permitidos en Production. |
| `Evidencias:RutaRaiz` | Carpeta persistente y privada de fotografías. |
| `Seguridad:Sesion` | Cookie, duración, SameSite y CORS. |
| `Seguridad:Despliegue` | Proxy confiable, claves y certificado de Data Protection. |
| `Api` | Tamaños, paginación y límites de solicitudes. |
| `Observabilidad` | Identificación del servicio, muestreo y destino OTLP opcional. |

En desarrollo se utiliza User Secrets. En el hospedaje, suministrar los valores mediante el gestor de secretos o un archivo de entorno privado. Las variables de entorno representan la jerarquía con `__`, por ejemplo `ConnectionStrings__Constructora`.

[`ConfiguracionProduccion.ejemplo.json`](ConfiguracionProduccion.ejemplo.json) es una plantilla. Production requiere hosts explícitos, cookies Secure y rutas absolutas, separadas y persistentes para evidencias, claves y certificado. La cuenta PostgreSQL no puede ser propietaria ni administradora; el arranque verifica estas restricciones.

## Herramienta de respaldo

`Tools/Respaldos` proporciona `crear`, `verificar`, `restaurar` y `depurar`. Recibe las conexiones mediante `ConnectionStrings__Respaldo` y `ConnectionStrings__Restauracion`, no como argumentos.

Con rutas absolutas y conexión suministrada por el entorno privado:

```powershell
dotnet run --project Tools/Respaldos/ConstructoraEderTorres.Respaldos.csproj --configuration Release -- crear --destino $carpetaRespaldos --evidencias $carpetaEvidencias --binarios $binariosPostgreSql --base-esperada $baseOrigen
dotnet run --project Tools/Respaldos/ConstructoraEderTorres.Respaldos.csproj --configuration Release -- verificar --carpeta $respaldoSeleccionado --binarios $binariosPostgreSql
```

La creación conserva una instantánea PostgreSQL de solo lectura, exporta la base y copia sus evidencias referenciadas. Comprueba hashes y tamaños y genera un manifiesto. Las carpetas `pendiente-*` representan operaciones incompletas; `respaldo-*`, copias completadas.

El respaldo contiene datos del sistema. Mantenerlo en almacenamiento privado y conservar una copia fuera del servidor. Las fotografías no están dentro del dump PostgreSQL. Custodiar el certificado y el anillo de Data Protection cuando se necesite recuperar sesiones.

## Restauración

Usar una base vacía distinta del origen y una carpeta de evidencias nueva:

```powershell
dotnet run --project Tools/Respaldos/ConstructoraEderTorres.Respaldos.csproj --configuration Release -- restaurar --carpeta $respaldoSeleccionado --binarios $binariosPostgreSql --base-esperada $baseNueva --destino-evidencias $carpetaEvidenciasNueva
```

La herramienta verifica la copia y restaura en una transacción. No utiliza `--clean` ni sustituye una base poblada. Después, revisar esquema, historial, datos, secuencias y fotografías; aplicar los permisos de ejecución y revocar las sesiones restauradas antes de habilitar tráfico.

## Mantenimiento

- Ejecutar las migraciones con una cuenta autorizada y un respaldo verificado previo.
- Usar `Tools/Operacion/ConfigurarRolEjecucion.sql` para los privilegios de la API.
- Usar `Tools/Operacion/DepurarSesionesVencidas.sql` con una cuenta de mantenimiento y lotes limitados.
- Revisar errores, uso de disco, renovación HTTPS y resultado de los respaldos.
- Ante una actualización fallida, seleccionar la versión anterior del código; no revertir datos automáticamente.

Los scripts de `Tools/Despliegue` preparan Linux, publican el cliente y configuran respaldos supervisados por systemd. La configuración del entorno Azure está en [`DespliegueAzureVm.md`](DespliegueAzureVm.md).
