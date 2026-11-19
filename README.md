# Constructora Eder Torres

Sistema de información web para el seguimiento y control operativo de obras de Constructora Eder Torres, Managua, Nicaragua.

La solución integra una API ASP.NET Core, una aplicación web progresiva (PWA) y PostgreSQL. Permite administrar obras y personal, registrar asistencia con validación de ubicación, documentar avances y evidencias, elaborar actas semanales y controlar equipos de protección personal y herramientas.

## Contenido

- [Funcionalidades](#funcionalidades)
- [Tecnologías y arquitectura](#tecnologías-y-arquitectura)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Requisitos](#requisitos)
- [Instalación local](#instalación-local)
- [Acceso y seguridad](#acceso-y-seguridad)
- [Pruebas](#pruebas)
- [Compilación y despliegue](#compilación-y-despliegue)
- [Documentación técnica](#documentación-técnica)
- [Control de versiones](#control-de-versiones)

## Funcionalidades

| Módulo | Operaciones principales |
|---|---|
| Obras | Registro, estados, ubicación geográfica, radio de asistencia y consulta en mapa. |
| Personal | Trabajadores, cargos, especialidades, cuadrillas y asignaciones por obra. |
| Asistencia | Marcación propia y asistida, entradas y salidas, validación GPS, correcciones autorizadas y consolidados. |
| Seguimiento | Actividades, cantidades planificadas y ejecutadas, fotografías, incidencias y actas semanales. |
| Protección personal | Existencias, entregas, reposiciones, devoluciones e historial de EPP. |
| Herramientas | Inventario, asignaciones, devoluciones e incidencias. |
| Seguridad | Usuarios, roles, permisos, sesiones y auditoría de operaciones. |
| Sincronización | Capturas compatibles sin conexión, almacenamiento local y confirmación individual por el servidor. |

La interfaz se adapta a teléfonos, tabletas y computadoras. Incluye temas claro, oscuro y del sistema, gráficos operativos y una vista gerencial de obras con Google Maps.

## Tecnologías y arquitectura

| Componente | Tecnología |
|---|---|
| API | C#, ASP.NET Core y .NET 10. |
| Persistencia | PostgreSQL 16, Entity Framework Core y Npgsql. |
| Frontend | React, TypeScript y Vite. |
| Estado y operación offline | TanStack Query e IndexedDB. |
| Interfaz | Tipografía Inter autoalojada e iconografía Lucide. |
| Pruebas | xUnit, Vitest y Playwright con API y PostgreSQL reales en entornos aislados. |
| Despliegue Linux | Nginx, systemd y HTTPS. |

El backend es un monolito modular organizado por capas. `Domain` define las entidades y reglas; `Application`, los casos de uso y contratos; `Infrastructure`, la persistencia y los adaptadores; y `Presentation`, los puntos de acceso HTTP y la configuración de la aplicación.

El frontend consume la API y presenta las acciones permitidas para cada sesión. La autorización, el alcance por obra y las reglas de negocio se comprueban en el servidor.

## Estructura del repositorio

```text
├── .config/                  Herramientas locales de .NET
├── .github/workflows/        Integración continua del backend y frontend
├── database/                 Esquema PostgreSQL y scripts de actualización
├── docs/                     Integración, operación y despliegue
├── frontend/
│   ├── public/               Iconos y manifiesto de la PWA
│   ├── scripts/              Contratos, service worker y proxy local
│   ├── src/                  Aplicación React
│   └── tests/                Pruebas unitarias y de navegador
├── src/
│   ├── Domain/               Entidades y reglas del negocio
│   ├── Application/          Casos de uso y contratos
│   ├── Infrastructure/       PostgreSQL, identidad y evidencias
│   └── Presentation/         API ASP.NET Core
├── Tests/Integracion/        Pruebas funcionales, seguridad y persistencia
└── Tools/
    ├── Despliegue/           Publicación y operación de la VM
    ├── Frontend/             Entorno E2E aislado de la PWA
    ├── InspeccionEsquema/    Consulta de metadatos PostgreSQL
    ├── Operacion/            Inicio local, permisos y entrega
    ├── Respaldos/            Copias coordinadas y restauración
    └── Simulacion/           Carga opcional de datos ficticios locales
```

## Requisitos

- **SDK .NET:** versión indicada en [`global.json`](global.json).
- **Node.js:** 20.19 o posterior compatible con el proyecto; el flujo de CI utiliza Node.js 22.
- **npm:** para instalar las versiones de `frontend/package-lock.json`.
- **PostgreSQL 16:** servidor local y utilidades de línea de comandos.
- **PowerShell 7:** para los scripts operativos de Windows.
- **Microsoft Edge:** para las pruebas E2E de navegador en Windows.

Las pruebas de integración y los scripts de comprobación local utilizan binarios PostgreSQL para Windows. El despliegue de la aplicación también admite Linux; las instrucciones operativas están en [`docs/DespliegueAzureVm.md`](docs/DespliegueAzureVm.md).

## Instalación local

### 1. Restaurar las dependencias

Desde la raíz del repositorio:

```powershell
dotnet restore ConstructoraEderTorres.sln --locked-mode
dotnet tool restore
npm --prefix frontend ci
```

### 2. Preparar PostgreSQL

Crear una base dedicada y preparar su esquema siguiendo [`database/README.md`](database/README.md). La API utiliza una base existente: no crea tablas ni ejecuta migraciones al arrancar.

### 3. Configurar la conexión

En Visual Studio, abrir **Administrar secretos de usuario** del proyecto `ConstructoraEderTorres.Presentacion` y definir la clave `ConnectionStrings:Constructora`:

```json
{
  "ConnectionStrings": {
    "Constructora": "Host=localhost;Port=5432;Database=constructora_eder;Username=USUARIO_LOCAL;Password=CONTRASENA_LOCAL"
  }
}
```

Los valores anteriores son marcadores de configuración. La contraseña real debe permanecer en User Secrets o en el gestor de secretos del entorno.

Las fotografías se guardan fuera de `wwwroot`, en la ruta `Evidencias:RutaRaiz`. Para conservar los datos locales fuera del código, establecer en User Secrets una ruta absoluta de almacenamiento persistente.

### 4. Iniciar la API

```powershell
dotnet run --project src/Presentation/ConstructoraEderTorres.Presentacion.csproj --launch-profile http
```

| Recurso local | Dirección |
|---|---|
| API | `http://localhost:5180` |
| Scalar | `http://localhost:5180/scalar` |
| OpenAPI | `http://localhost:5180/openapi/v1.json` |
| Salud del proceso | `http://localhost:5180/salud/viva` |
| Salud de PostgreSQL | `http://localhost:5180/salud/lista` |

Scalar y OpenAPI se habilitan únicamente en Development. El perfil `https` utiliza `https://localhost:7180` y requiere un certificado de desarrollo confiable.

### 5. Inicializar la primera cuenta

Solo cuando la base preparada todavía no contiene usuarios:

```powershell
dotnet run --project src/Presentation/ConstructoraEderTorres.Presentacion.csproj --launch-profile http -- --inicializar-administrador
```

El comando solicita el nombre de usuario y la contraseña con confirmación. Las cuentas posteriores se administran desde **Usuarios y accesos**. No existe registro público ni una contraseña predeterminada.

### 6. Iniciar el frontend

En una segunda terminal:

```powershell
cd frontend
npm run dev
```

Abrir `http://localhost:5173`. El proxy local envía las solicitudes de API a `http://localhost:5180`.

Para personalizar el entorno, copiar [`frontend/.env.example`](frontend/.env.example) a `frontend/.env.local`. La clave de Google Maps se configura mediante `VITE_GOOGLE_MAPS_API_KEY` y debe restringirse por dominio y API. Las variables `VITE_*` son públicas y forman parte de la compilación del navegador.

## Acceso y seguridad

- Autenticación mediante cookies `HttpOnly`, protegidas con Data Protection.
- Cierre por 30 minutos de inactividad y duración máxima de ocho horas, configurables.
- Protección CSRF en las escrituras y autorización por permisos y obra.
- Validación GPS en las marcaciones y al sincronizar, considerando la precisión del dispositivo.
- Auditoría de operaciones, límites de solicitudes y respuestas de error sin detalles privados de persistencia.
- Validación de archivos y descarga autenticada de evidencias.
- En Production: HTTPS, cookies Secure, hosts explícitos, claves persistentes cifradas y cuenta PostgreSQL de ejecución con privilegios limitados.

Los perfiles de acceso incluyen Gerente General, Gerente de Operaciones, Administrativo, Ingeniero Residente, Higiene y Seguridad y Trabajador. La visibilidad de la interfaz depende de los permisos efectivos de la cuenta.

## Pruebas

### Backend

```powershell
dotnet test ConstructoraEderTorres.sln --configuration Release
```

Las pruebas levantan clústeres PostgreSQL desechables bajo `artifacts/`. Localizan los binarios en las instalaciones habituales o mediante `PG_BIN`; no utilizan la base operativa `constructora_eder`.

Para comprobar inspección, scripts SQL y recuperación real:

```powershell
$env:PG_BIN = 'C:\Program Files\PostgreSQL\16\bin'
& ./Tests/Integracion/VerificarPersistenciaPostgreSql.ps1 -BinariosPostgreSql $env:PG_BIN -Configuracion Release
```

Adaptar `PG_BIN` si PostgreSQL está instalado en otra ubicación.

### Frontend

```powershell
npm --prefix frontend test
npm --prefix frontend run test:e2e
```

El entorno E2E inicia API, PostgreSQL y servidor web de pruebas con datos sintéticos. Las capturas, trazas y resultados se generan bajo `artifacts/` y quedan excluidos de Git.

## Compilación y despliegue

```powershell
dotnet build ConstructoraEderTorres.sln --configuration Release --no-restore
npm --prefix frontend run build
```

El frontend compilado queda en `frontend/dist`. Para preparar un paquete Linux de API, PWA, respaldos y recursos operativos:

```powershell
& ./Tools/Despliegue/PrepararPublicacionLinux.ps1
```

El paquete se genera bajo `artifacts/DespliegueLinux`, con inventario y SHA-256. Su preparación no instala ni publica el sistema automáticamente.

El despliegue utiliza un origen HTTPS común para frontend y API, con Nginx como proxy y systemd para supervisar el proceso. PostgreSQL, evidencias y claves de sesión se conservan en almacenamiento persistente. Los respaldos incluyen base y fotografías; la recuperación de sesiones requiere custodiar también su certificado y anillo de claves.

## Documentación técnica

| Documento | Contenido |
|---|---|
| [`database/README.md`](database/README.md) | Preparación del esquema, línea base y migraciones. |
| [`docs/IntegracionApi.md`](docs/IntegracionApi.md) | Sesiones, CSRF, contratos, sincronización y archivos. |
| [`docs/OperacionYRestauracion.md`](docs/OperacionYRestauracion.md) | Configuración, respaldos y restauración. |
| [`docs/DespliegueAzureVm.md`](docs/DespliegueAzureVm.md) | Instalación y operación del entorno Azure. |
| [`frontend/README.md`](frontend/README.md) | Desarrollo de la PWA, mapa y pruebas móviles. |

## Control de versiones

El repositorio contiene código, pruebas, contratos, scripts de base de datos, configuraciones de ejemplo y documentación técnica. Las dependencias se reproducen con los archivos de bloqueo de NuGet y npm.

`.gitignore` excluye compilaciones, cachés, dependencias instaladas, respaldos, resultados de pruebas, certificados, credenciales, archivos de entorno local y fotografías operativas. La documentación académica se administra fuera del repositorio.
