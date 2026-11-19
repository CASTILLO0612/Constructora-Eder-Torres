# Simulación local de Constructora Eder Torres

Herramienta de desarrollo que carga datos ficticios relacionados en la base existente `constructora_eder`. Solo admite PostgreSQL en loopback y requiere una cuenta Gerente General activa. No modifica la matriz de permisos ni sustituye las credenciales de cuentas existentes.

## Ejecutar

Desde la raíz del repositorio, con `ConnectionStrings:Constructora` configurada en secretos de usuario del servidor o en la variable de entorno `ConnectionStrings__Constructora`:

```powershell
dotnet run --project Tools/Simulacion/ConstructoraEderTorres.Simulacion.csproj --configuration Release -- --aplicar
```

Sin `--aplicar` no se carga información. La cadena y su contraseña permanecen fuera del repositorio. No se necesita modificar el frontend, el esquema ni los documentos funcionales.

La carga usa los manejadores de aplicación, sus validaciones y auditoría, dentro de una transacción serializable. Un bloqueo transaccional evita ejecutar dos cargas simultáneas. Si existe `SIM-CET-01`, la herramienta termina sin duplicar ni reiniciar el conjunto. Los catálogos existentes se reutilizan por nombre sin reemplazar su configuración.

## Contenido de la carga inicial

| Información | Cantidad |
|---|---:|
| Obras: dos activas, una planificada, una suspendida y una finalizada | 5 |
| Trabajadores con cuentas individuales ficticias | 18 |
| Cuadrillas | 4 |
| Asignaciones de trabajadores a obras | 16 |
| Actividades | 20 |
| Jornadas cerradas de asistencia | 77 |
| Unidades físicas de EPP | 40 |
| Herramientas | 18 |
| Cuentas, incluidas las anteriores de trabajadores y cuatro perfiles de gestión | 22 |

Incluye cargos, especialidades, tipos de EPP/herramientas, avances históricos, entregas/devoluciones, una incidencia climática y dos actas semanales. Las jornadas históricas se generan respecto a la fecha local de Managua; su cantidad puede variar si se aplica el conjunto por primera vez otro día.

Las entidades específicas del conjunto usan prefijos `SIM-` o descripciones de simulación. Los contactos utilizan `example.invalid`. Los nombres y ubicaciones son ficticios; no constituyen operaciones empresariales reales. Los catálogos genéricos pueden ser compartidos con registros existentes.

## Accesos ficticios

Las contraseñas son aleatorias e individuales. Se guardan solo en:

`%LOCALAPPDATA%\ConstructoraEderTorres\Simulacion\accesos-demo.json`

Ese archivo está fuera de Git. Contiene usuario, rol y contraseña de cada cuenta creada; no copiarlo a documentación, capturas o archivos públicos.

Usuarios: `sim.gerencia`, `sim.operaciones`, `sim.higiene`, `sim.administrativo` y `sim.persona01` a `sim.persona18`. Las personas 01, 05, 09 y 13 son residentes; las restantes tienen perfil Trabajador. La aplicación sigue permitiendo entrar con las cuentas existentes.

Los selectores muestran únicamente referencias autorizadas y compatibles: una obra sin asignaciones no tendrá trabajadores elegibles; un recurso entregado no figura como disponible. La simulación no desactiva estas reglas.

No incluye borrado automático. Al tratarse de registros relacionados, cualquier retirada posterior debe respetar las relaciones y los datos que el usuario haya añadido durante sus pruebas.
