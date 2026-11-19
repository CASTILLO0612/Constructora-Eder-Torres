# PWA de Constructora Eder Torres

Cliente React + TypeScript conectado a la API ASP.NET Core. La dirección visual es blanco, negro y grises, tipografía Inter autoalojada e iconografía Lucide. Las funciones visibles dependen de los permisos de la sesión; la API valida nuevamente autorización y alcance.

## Iniciar en esta computadora

Requisitos: Node.js 20.19 o posterior compatible, npm y la API ejecutándose con su conexión PostgreSQL configurada fuera del repositorio.

Desde esta carpeta:

```powershell
npm ci
npm run dev
```

Abrir **http://localhost:5173**. El proxy envía `/api` y `/salud` a `http://localhost:5180`. Ingresar con una cuenta existente de la constructora, la misma que se utiliza en la API. Las cuentas se administran desde Usuarios y accesos; no hay registro público ni credenciales predefinidas de producción.

Para cambiar la dirección local de la API, copiar `.env.example` a `.env.local` y configurar `API_DESTINO`. Nunca incluir contraseñas, cadenas PostgreSQL o claves privadas en variables `VITE_*`: forman parte del código público del navegador.

## Probar la PWA compilada y su funcionamiento offline

```powershell
npm run build
npm run preview:integrada
```

Abrir **http://localhost:4173**. Este servidor local sirve `dist` y proxifica la API. La instalación y el service worker se activan en la compilación, no en el servidor de desarrollo Vite.

Iniciar sesión con conexión, abrir los datos de trabajo y usar **Preparar obra offline** para descargar los catálogos autorizados de una obra antes de salir a campo. La marcación propia carga sus asignaciones automáticamente. La PWA conserva las capturas en IndexedDB; indica si están locales, pendientes, rechazadas o confirmadas.

Se reintenta automáticamente con la aplicación abierta al recuperar red. Cerrar el navegador no elimina los pendientes, pero los navegadores no garantizan ejecutar tareas con la aplicación cerrada; se continúa al abrirla. Si la sesión vence, la cola se conserva y se exige autenticación para transmitir. El trabajador no puede obtener datos de otras personas.

### Prueba temporal en Android por USB

Mantener la API y `npm run preview:integrada` activos en la computadora. En Chrome de escritorio, usar `chrome://inspect/#devices`, conectar/autorizar el teléfono y configurar el reenvío del puerto `4173` a `localhost:4173`. En Chrome de Android abrir `http://localhost:4173`. El mismo origen envía `/api` al proxy local y permite comprobar GPS desde localhost. El cable y el proceso que mantiene el reenvío deben permanecer activos. Referencia: [Chrome DevTools](https://developer.chrome.com/docs/devtools/remote-debugging/local-server).

«Red disponible» expresa el estado que informa el navegador; no garantiza que la API responda. Si una apertura recupera el perfil local por una falla temporal, se muestra «Sesión local sin verificar». Con la PWA visible se vuelve a consultar la sesión cada 15 segundos (configurable mediante `VITE_SESION_REINTENTO_MS`), al recuperar foco/visibilidad y con **Verificar conexión**. Una confirmación válida recarga los datos y permite continuar la sincronización; un 401 exige ingresar nuevamente. Se conservan las capturas locales.

El transporte combina cancelación y plazo con `AbortController`, incluyendo navegadores sin los métodos estáticos `AbortSignal.any` y `AbortSignal.timeout`. Se mantiene el tiempo máximo configurado de las solicitudes. Tras actualizar la compilación local, recargar la página y aceptar el aviso de nueva versión si aparece.

## Pruebas

```powershell
npm test
npm run test:e2e
```

La segunda orden compila la PWA y ejecuta `Tools/Frontend/EntornoPruebas.csproj`. El entorno inicia PostgreSQL desechable, siembra datos sintéticos, arranca la DLL real de la API y un servidor web local, y prueba la interfaz con Playwright/Edge. Al terminar detiene los procesos y elimina la base de pruebas. No utiliza `constructora_eder` ni las cuentas empresariales.

El recorrido E2E requiere Windows con Microsoft Edge, el SDK de `global.json` y los binarios PostgreSQL (`initdb.exe`, `pg_ctl.exe`, `psql.exe`). Se pueden localizar mediante `PG_BIN`. Los informes, capturas y trazas se guardan en `../artifacts/Frontend/`, fuera de Git.

## Organización

- `src/app`: composición, sesión, tema y formatos.
- `src/api`: transporte, contratos, permisos y catálogos generados.
- `src/components`: botones, tablas, formularios, selección de referencias, cámara y preparación offline.
- `src/features`: módulos del negocio en español.
- `src/offline`: persistencia por usuario y motor de sincronización.
- `src/hooks`: consultas y estado compartido.
- `src/styles`: estilos adaptables y temas monocromáticos.
- `tests`: pruebas de contratos, reglas de captura, persistencia y navegador.
- `scripts`: generación de contratos, service worker y servidor local de integración.

Los nombres propios del negocio y los símbolos de la aplicación están en español. Los nombres de paquetes y APIs externas mantienen su forma oficial.

## Contratos y configuración

`contrato-api.json` es una instantánea sin datos del OpenAPI vigente. Para regenerar los tipos y metadatos:

```powershell
npm run contratos
```

El generador también admite `API_OPENAPI` con una ruta o URL de un documento OpenAPI. Los permisos y estados se derivan de los catálogos C# del repositorio; no se fijan IDs numéricos de usuarios, roles u obras.

`VITE_API_BASE` debe quedar vacío cuando PWA y API se publiquen bajo el mismo origen. Si se separan, configurar HTTPS, cookies y el origen CORS exacto siguiendo [IntegracionApi.md](../docs/IntegracionApi.md). Los límites locales de sesión, tamaño de archivo y tiempos de espera se configuran en `.env.local`; mantenerlos coordinados con el servidor.

## Navegación y resumen

El encabezado solo contiene el botón del menú lateral plegable. En teléfonos, el último destino de la barra inferior es **Ajustes**: apariencia Claro/Oscuro/Sistema, cuenta, estado de conexión, verificación y cierre de sesión. La advertencia de desconexión se mantiene visible fuera del encabezado cuando corresponde.

La lista del menú se desplaza de forma independiente y conserva el perfil/cierre de sesión visibles en el pie, con área pulsable de 44 px y soporte para altura dinámica y áreas seguras. Los filtros se distribuyen en cuadrícula y en una columna en teléfonos, con etiquetas y controles uniformes; las acciones de actualizar/cambiar vista ocupan su propia fila. La revisión de diseño cubre vistas principales, pestañas de los módulos y formularios.

El resumen incluye gráficos SVG accesibles por controles HTML: estados de obras, asistencia del período, avance por actividad y existencias EPP. Las consultas y acciones conservan los permisos del servidor. Los gráficos indican cuando alcanzan el límite de registros consultados; no representan un total empresarial ilimitado. Las horas solo incluyen intervalos válidos de jornadas cerradas y no anuladas.

Los encabezados de las vistas contienen solo el título y sus acciones. `Cargando` utiliza el componente `EsqueletoCarga` para adaptar la espera al contenido de cada módulo y a la sesión inicial. Incluye estados para tablas, opciones, formularios, gráficos y lectura de pendientes locales, sin retardos añadidos. Los lectores de pantalla reciben un estado de carga y la preferencia de movimiento reducido desactiva el pulso. La actualización de datos ya disponibles conserva el contenido; guardar o enviar mantiene su indicador en el botón.

La tipografía de campos y buscadores se centraliza en los tokens `--fuente-interfaz`, `--tamano-texto-control`, `--peso-texto-control` y `--interlineado-control`. Los controles usan Inter local, 1 rem y peso 400. Los placeholders heredan la tipografía del campo; se eliminan tamaños particulares para selectores de referencias y controles de gráficos.

Para llenar los selectores y probar con registros ficticios en la base local, consultar [Tools/Simulacion/LEEME.md](../Tools/Simulacion/LEEME.md). La carga es explícita y no forma parte del arranque de la aplicación.

## Mapa gerencial

La vista gerencial utiliza Google Maps JavaScript API con las coordenadas registradas de las obras. Los marcadores señalan el centro de cada obra y, cuando está definido, se dibuja su radio de asistencia. El mapa sigue la preferencia claro/oscuro/sistema de la aplicación. Al seleccionar una obra se puede abrir su ubicación en Google Maps; la lista permanece disponible si el servicio cartográfico no carga.

La clave del mapa se configura en `.env.local` mediante `VITE_GOOGLE_MAPS_API_KEY`. En desarrollo puede usarse la Maps Demo Key; para publicar, crear una clave propia, restringirla a los dominios autorizados y a Maps JavaScript API, y revisar facturación y uso. Las variables `VITE_*` se incluyen en el cliente del navegador y no deben contener claves privadas de servidor. No se configuran mosaicos de Google en Leaflet.

El mapa no rastrea trabajadores ni se utiliza para decidir una marcación. La geocerca se vuelve a validar en la API, incluyendo precisión GPS y asignación.

## Publicación

`npm run build` produce archivos estáticos en `dist`. Servirlos con HTTPS y una ruta alternativa a `index.html` para las rutas de la PWA, manteniendo `/api` y `/salud` dirigidas a ASP.NET Core. Configurar el proxy confiable y las cookies de producción en la API. Los secretos de PostgreSQL permanecen exclusivamente en el servidor.

`scripts/ServidorIntegracion.mjs` es un servidor local de desarrollo/pruebas. La configuración de Nginx, systemd, certificados y respaldos del entorno Azure está en [DespliegueAzureVm.md](../docs/DespliegueAzureVm.md).

## Dependencias y licencias

Se conservan las licencias de React, React Router, TanStack Query, idb, Lucide e Inter en sus paquetes. Lucide usa ISC e Inter OFL-1.1; revisar los avisos completos en las dependencias al preparar la distribución. Google Maps muestra su propia atribución dentro del mapa.

Los informes, capturas y trazas de cada ejecución se generan bajo `artifacts/`, excluido del control de versiones. La documentación académica se conserva por separado.
