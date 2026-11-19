# Base de datos PostgreSQL

Esta carpeta contiene los scripts necesarios para preparar la base sin depender de los documentos académicos. Los scripts de esquema no contienen registros de trabajadores, usuarios ni fotografías.

## Archivos

| Archivo | Uso |
|---|---|
| `EsquemaConciliadoPostgreSql.sql` | Esquema de partida para una base vacía: tablas, claves, índices y disparadores. |
| `RegistrarLineaBaseExistente.sql` | Registra el punto de partida en el historial de EF Core sin crear de nuevo las tablas. |
| `ActualizarAsistenciaTrabajadores.sql` | Actualización idempotente de asistencia propia y geocerca. |
| `ActualizarCapturaAsistencia.sql` | Actualización idempotente de precisión GPS y captura. |
| `ActualizarSesionesRevocables.sql` | Actualización idempotente de las sesiones persistentes. |

Las migraciones completas y posteriores están en [`src/Infrastructure/Persistencia/Migraciones`](../src/Infrastructure/Persistencia/Migraciones). El DDL anterior al esquema conciliado se conserva como recurso de pruebas en `Tests/Integracion/Datos/EsquemaOriginalPostgreSql.sql`.

## Preparar una base nueva

Con PostgreSQL 16 disponible, crear una base vacía y dedicada. Este ejemplo utiliza `constructora_eder` y las utilidades de PostgreSQL solicitan la contraseña cuando corresponde:

```powershell
createdb --host localhost --port 5432 --username postgres constructora_eder
psql --host localhost --port 5432 --username postgres --dbname constructora_eder --set ON_ERROR_STOP=1 --file database/EsquemaConciliadoPostgreSql.sql
psql --host localhost --port 5432 --username postgres --dbname constructora_eder --set ON_ERROR_STOP=1 --file database/RegistrarLineaBaseExistente.sql
```

Ejecutar desde la raíz del repositorio. Cargar el esquema inicial una sola vez sobre un destino vacío; no ejecutarlo sobre una base con datos.

Configurar `ConnectionStrings:Constructora` en los secretos de usuario del proyecto de presentación, con una cuenta propietaria o migradora autorizada para esta tarea. Aplicar después todas las migraciones:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project src/Infrastructure/ConstructoraEderTorres.Infraestructura.csproj --startup-project src/Presentation/ConstructoraEderTorres.Presentacion.csproj
```

El historial de EF Core incluye asistencia propia, precisión GPS, sesiones revocables y el ciclo de atención de incidencias de herramientas. La API no ejecuta estas migraciones al arrancar.

## Actualización y permisos

Crear y verificar un respaldo antes de modificar una base existente. Revisar `__EFMigrationsHistory` y aplicar únicamente las migraciones pendientes. Los scripts idempotentes se conservan para procedimientos operativos que ya los utilicen; no sustituyen la revisión del historial completo.

La cuenta de la API debe estar separada del propietario y carecer de privilegios administrativos. [`Tools/Operacion/ConfigurarRolEjecucion.sql`](../Tools/Operacion/ConfigurarRolEjecucion.sql) configura los permisos y protege la auditoría. Recibe las variables `rol_ejecucion` y `rol_propietario` de psql. Las contraseñas se establecen por un canal privado y no se incorporan al DDL.
