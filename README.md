# Backend de Constructora Eder Torres

Inicializacion del backend ASP.NET Core en .NET 10, con un monolito modular por capas y PostgreSQL mediante Entity Framework Core/Npgsql. Estas decisiones fueron confirmadas por el usuario el 27 de octubre de 2026.

La API se ejecuta como DLL mediante el runtime de .NET instalado (`UseAppHost=false`), decision confirmada por el usuario durante la inicializacion. Windows rechazo la generacion del lanzador nativo `apphost.exe`; este modo de despliegue de .NET permite compilar y ejecutar el backend sin generar un ejecutable propio de Windows.

El contexto funcional y sus fuentes se mantienen en [backend-contexto-constructora-eder-torres.md](backend-contexto-constructora-eder-torres.md). La carpeta del proyecto conserva su nombre original; el nombre interno de la solucion corresponde a Constructora Eder Torres, como indica el contexto.

## Abrir y ejecutar

Abrir `ConstructoraEderTorres.sln` con Visual Studio 2026 y seleccionar `ConstructoraEderTorres.Presentacion` como proyecto de inicio. Desde una terminal situada en esta carpeta:

~~~powershell
dotnet restore ConstructoraEderTorres.sln
dotnet build ConstructoraEderTorres.sln --no-restore
dotnet test ConstructoraEderTorres.sln --no-build --no-restore
dotnet run --project src/Presentation/ConstructoraEderTorres.Presentacion.csproj --launch-profile http
~~~

El perfil HTTP de desarrollo utiliza `http://localhost:5180`. El perfil `https` utiliza `https://localhost:7180` y requiere un certificado de desarrollo valido y confiable. La geolocalizacion de la futura PWA requiere un contexto seguro y permiso del usuario; su captura corresponde al cliente.

`global.json` selecciona el SDK estable 10.0.403 y permite actualizaciones de parche de esa banda. El SDK se detecto instalado durante esta inicializacion; la finalizacion completa de la instalacion de Visual Studio debe comprobarse en el IDE.

## Estado implementado

- Solucion con Dominio, Aplicacion, Infraestructura, Presentacion y un proyecto de pruebas de integracion.
- Carpetas principales en ingles; carpetas internas, clases, metodos y variables propios en español. `Program.cs`, `appsettings.json`, `Properties/launchSettings.json` y otros nombres exigidos por las herramientas conservan su nombre convencional.
- Contexto EF Core preparado para cargar los futuros mapeos desde Infraestructura.
- Proveedor Npgsql y origen de conexiones registrado cuando se configura la cadena de conexion.
- Comprobaciones separadas de vitalidad de la API y disponibilidad de PostgreSQL.
- Manejo centralizado de errores con Problem Details e identificador de solicitud.
- Documento OpenAPI publicado exclusivamente en desarrollo.
- Paquetes con versiones centralizadas y archivos de bloqueo por proyecto.

La inicializacion implementa la base tecnica. Los casos de uso de negocio y los mapeos de las 38 tablas se incorporaran al conciliar el esquema actual. No hay migraciones, creacion de tablas, usuarios de prueba ni datos de ejemplo ejecutados al iniciar.

## Estructura y dependencias

~~~text
ConstructoraEderTorres.sln
src/
  Domain/                  ConstructoraEderTorres.Dominio
  Application/             ConstructoraEderTorres.Aplicacion
    CasosDeUso/            Seguridad, Obras, Personal, Asistencia,
                          SeguimientoObra, EPP, Herramientas, Sincronizacion
    Contratos/
    Comun/
  Infrastructure/          ConstructoraEderTorres.Infraestructura
    Configuracion/
    Diagnostico/
    Persistencia/
      PostgreSQL/
      Mapeos/
      Migraciones/
    Evidencias/
    Auditoria/
    Identidad/
  Presentation/            ConstructoraEderTorres.Presentacion
    Configuracion/
    Intermediarios/
    PuntosAcceso/Salud/
Tests/
  Integracion/
  Unitarias/
  Contratos/
~~~

Las referencias de proyectos son: Aplicacion depende de Dominio; Infraestructura depende de Aplicacion y Dominio; Presentacion compone Aplicacion e Infraestructura. Dominio permanece independiente de ASP.NET Core, EF Core y PostgreSQL. Los contratos HTTP se mantienen separados de las entidades.

Las carpetas de modulos estan preparadas para implementar cortes verticales por caso de uso. Se adopta DDD selectivo y separacion ligera de comandos/consultas cuando el caso lo requiera; esta base no incorpora un mediador, repositorio generico ni bus de eventos.

## PostgreSQL y credenciales

La clave de configuracion es `ConnectionStrings:Constructora`. Su valor versionado esta vacio para que ninguna credencial quede en el repositorio. Cuando se disponga del nombre real de la base existente y de un usuario autorizado, reemplazar todos los marcadores del ejemplo:

~~~powershell
dotnet user-secrets set "ConnectionStrings:Constructora" "Host=<servidor>;Port=<puerto>;Database=<base_existente>;Username=<usuario>;Password=<contraseña>" --project src/Presentation/ConstructoraEderTorres.Presentacion.csproj
~~~

User Secrets sirve para el desarrollo local. En otros entornos, configurar `ConnectionStrings__Constructora` mediante el mecanismo de secretos del hospedaje que se elija.

Si la cadena falta, la API inicia para comprobar su funcionamiento y consultar OpenAPI. La disponibilidad devuelve 503 y el contexto de persistencia queda pendiente de configuracion. Una cadena de conexion presente pero invalida se rechaza al registrar Infraestructura.

Cuando la cadena es valida, la comprobacion de disponibilidad abre una conexion y ejecuta unicamente `SELECT 1`. Esto verifica conectividad, autenticacion y respuesta de PostgreSQL; no certifica que el esquema de las 38 tablas este conciliado ni que existan permisos para cada operacion futura.

Antes de mapear entidades o crear migraciones:

1. Cotejar el esquema de la base actual con `Documentacion/CORTE I/Modelo Fisico Constructora Eder Torres.sql`.
2. Confirmar si se aplicaron `estado` en `incidencias_herramienta` y las restricciones entre obras solicitadas.
3. Incorporar los mapeos con nombres y tipos fieles al esquema conciliado.
4. Definir una linea de base para que EF Core gestione cambios posteriores sin recrear la base existente.

La herramienta local de EF Core se prepara con `dotnet tool restore`. No se debe generar una migracion inicial sobre el contexto todavia vacio. Los scripts originales permanecen como fuente documental.

## Puntos de acceso iniciales

| Solicitud | Resultado esperado |
|---|---|
| `GET /salud/viva` | 200 si la API puede responder. No consulta PostgreSQL. |
| `GET /salud/lista` | 200 si PostgreSQL responde; 503 si falta configuracion o no esta disponible. |
| `GET /openapi/v1.json` | Documento OpenAPI en desarrollo; 404 en los demas entornos. |

Las respuestas de salud utilizan `estado` y `componentes`, y se envian con `Cache-Control: no-store`. La API devuelve errores con formato Problem Details y un `identificador`; las excepciones internas no se publican en la respuesta.

`src/Presentation/ComprobarApi.http` permite probar estas solicitudes desde Visual Studio. Estos puntos de acceso son diagnostico tecnico, no nuevos requerimientos funcionales del producto.

## Pruebas y comprobaciones

Las seis pruebas de integracion se ejecutan en memoria con una configuracion de PostgreSQL vacia. Comprueban vitalidad, disponibilidad sin base configurada, OpenAPI valido en desarrollo, ausencia de OpenAPI fuera de desarrollo, errores 404 e inexistencia de mensajes internos en respuestas 500. No necesitan acceder a la base real.

Verificacion realizada el 27 de octubre de 2026: restauracion normal y bloqueada correctas, compilacion sin errores ni advertencias y seis pruebas aprobadas. Tambien se comprobo la API en HTTP: vitalidad 200, disponibilidad 503 por conexion aun no configurada y OpenAPI 200 con los dos puntos de acceso de salud. La herramienta local EF Core 10.0.14 se restauro correctamente.

Las versiones NuGet se fijan en `Directory.Packages.props`. Los archivos `packages.lock.json` permiten repetir la restauracion; despues de disponer de ellos se puede usar:

~~~powershell
dotnet restore ConstructoraEderTorres.sln --locked-mode
~~~

## Decisiones que siguen pendientes

Para el siguiente incremento se necesitan el esquema PostgreSQL conciliado y la configuracion local de conexion. Antes de implementar autenticacion se deben precisar hospedaje/origen, mecanismo de sesion, vencimiento, revocacion y matriz de permisos. Los umbrales GPS, aprobaciones de correcciones, conflictos offline y firma de actas siguen el registro de aclaraciones del contexto.

La autenticacion futura debe respetar las tablas `usuarios`, `roles` y `permisos` existentes. La inicializacion no incorpora tablas de ASP.NET Identity. SignalR, almacenamiento de evidencias y frontend quedan sujetos a los casos y decisiones documentados.

## Referencias tecnicas

- [Proveedor Npgsql para EF Core](https://www.npgsql.org/efcore/).
- [OpenAPI en ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0).
- [Comprobaciones de salud](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).
- [Seleccion de SDK mediante global.json](https://learn.microsoft.com/dotnet/core/tools/global-json).
- [Ejecucion dependiente del runtime y UseAppHost](https://learn.microsoft.com/dotnet/core/deploying/).
