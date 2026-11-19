# Despliegue de demostración en Azure VM

## Destino y estado

Destino seleccionado: VM Ubuntu 24.04 x64 existente `vm-ruta-emocional-qa`, tamaño B2als v2 (2 vCPU, 4 GiB). Nombre DNS: `constructora-eder-torres-demo.northcentralus.cloudapp.azure.com`.

El 18 de noviembre de 2026 se publicó la aplicación en [el sitio HTTPS de demostración](https://constructora-eder-torres-demo.northcentralus.cloudapp.azure.com/ingresar). La IP es estática, el DNS está aplicado, HTTP/HTTPS están permitidos y SSH está limitado a la IP actual del operador. El usuario SSH es `azureuser`. La VM conserva su apagado automático a las 23:00 de América Central.

Versión de API/frontend instalada: `20261118T232804Z-7e580249`, paquete SHA-256 `e656abdc1674c76646d6af92f8212a5b71e41b814377164014752e33810fa4b3`. Se verificaron sus 81 archivos. Runtime instalado desde Ubuntu: ASP.NET Core 10.0.15. La base del sitio público es `constructora_eder_demo`, restaurada desde una instantánea coordinada de `constructora_eder` local; las operaciones del sitio público se guardan en la base de la VM.

Actualización visual publicada el 19 de noviembre de 2026, 00:00 UTC: `20261119T000021Z-222e84eb`, paquete del frontend SHA-256 `edcf0b50ac7b0c17e13234dd88fafc89509da1dfe2bd69103d335bb6b187e7f9`. Se simplificó el login a la marca de texto y sus líneas arquitectónicas, visibles en escritorio y teléfono; se retiraron el escudo, eslóganes y ayuda del login, el monograma del menú y el aviso de simulación. Las etiquetas heredadas se omiten al presentar nombres/direcciones. Los registros originales y los valores de edición permanecen en la base: este ajuste visual no convierte los datos de ejemplo en registros reales de operación.

`Tools/Despliegue/ActualizarFrontend.py` crea una versión independiente, comprueba que API y respaldos coincidan con sus hashes anteriores y sustituye atómicamente el enlace `actual`. Conserva los chunks anteriores para pestañas abiertas. La compilación y las 44 pruebas unitarias del frontend pasaron; se verificaron ingreso, menú y nombres de obras en el sitio publicado, con capturas a 1440 × 900 y 390 × 844 en `artifacts/ResultadosDespliegue/login-limpio-escritorio.jpg`, `login-limpio-movil.jpg` y `menu-marca-texto.jpg`.

Ruta Emocional mantiene su servicio y sitio existentes en el puerto interno 5000. Eder Torres utiliza 5180; ambos servicios y Nginx se comprobaron activos y el sitio anterior respondió HTTP 200 después del despliegue.

Esta VM se reutiliza para demostración académica. El encendido consume recursos de cómputo; el crédito estudiantil y los costos deben revisarse en Azure Cost Management. La desasignación evita el cargo de cómputo, pero no elimina los posibles cargos de discos y red. No se crean servicios de pago adicionales mediante los scripts de este directorio.

## Organización elegida

Frontend y API se publican bajo un único origen HTTPS. Nginx sirve la compilación estática y envía `/api/` y `/salud/` a Kestrel por loopback. La API utiliza Production, cookies Secure y la validación de permisos PostgreSQL ya implementada. PostgreSQL y Kestrel no necesitan puertos públicos.

| Elemento | Ubicación o identidad |
|---|---|
| Versiones del paquete | `/opt/constructora-eder-torres/releases/<version>/` |
| Enlace a versión activa | `/opt/constructora-eder-torres/actual` |
| Usuario sin inicio de sesión para la API | `cet-api` |
| Servicio | `constructora-eder-torres.service` |
| Configuración privada | `/etc/constructora-eder-torres/entorno` |
| Certificado privado de Data Protection | `/etc/constructora-eder-torres/sesiones.pfx` |
| Anillo cifrado de sesiones | `/var/lib/constructora-eder-torres/claves` |
| Fotografías y evidencias | `/var/lib/constructora-eder-torres/evidencias` |
| Desafío del certificado HTTPS | `/var/lib/constructora-eder-torres/acme` |
| Respaldos locales | `/var/backups/constructora-eder-torres` |

Las fotografías continúan usando el adaptador local existente. Se almacenan en el disco persistente de la VM y se descargan mediante la API autenticada; no se publican como archivos de Nginx. Cambiar de versión de código no debe mover ni sustituir evidencias, certificados o claves.

La base de Eder Torres debe estar separada de Ruta Emocional, con propietario de esquema y rol de ejecución distintos. El rol de la API no debe ser superusuario, propietario ni administrador. `ConfigurarRolEjecucion.sql` aplica los privilegios y el arranque productivo los verifica. No se restauran datos sobre una base existente.

## Preparación local

Desde la raíz del repositorio, con PowerShell 7, SDK .NET de `global.json`, Node compatible y las dependencias bloqueadas:

```powershell
& ./Tools/Despliegue/PrepararPublicacionLinux.ps1
```

El script compila API, herramienta de respaldo y frontend; conserva el estado del árbol Git en el manifiesto y genera un paquete `.tar.gz` con su SHA-256 bajo `artifacts/DespliegueLinux`. El paquete requiere runtime ASP.NET Core 10 en el servidor. No representa una instalación ni una prueba en Linux.

La compilación fuerza API relativa al mismo origen y conserva la configuración pública de Vite. La clave del navegador de Google Maps se incorpora a JavaScript por diseño: debe estar restringida en Google Cloud al origen HTTPS publicado y a Maps JavaScript API. No se deben incorporar claves privadas, cadenas PostgreSQL o credenciales de usuarios en variables `VITE_*`.

El paquete excluye configuración Development, secretos, certificados, volcados de base y archivos de entorno. Las cabeceras Nginx se generan desde la política vigente del frontend para mantener los dominios necesarios para Google Maps.

## Inspección previa del servidor

Con la VM encendida y el usuario SSH existente, ejecutar `InspeccionarServidor.sh`. Solo consulta sistema, memoria, disco, puertos, servicios, nombres de aplicaciones, certificados y bases. No imprime variables de entorno, contraseñas ni registros personales.

Antes de instalar, comprobar si Ruta Emocional ocupa los puertos 80/443, qué proxy utiliza, qué sitios atiende y qué PostgreSQL tiene disponible. Elegir un puerto loopback libre para la API y conservar los archivos de los otros sitios. Si hay un proxy diferente, adaptar la configuración al proxy existente antes de instalar Nginx. No detener ni sustituir servicios de otro proyecto.

Instalar el runtime ASP.NET Core 10 desde los repositorios oficiales compatibles con Ubuntu, y usar las utilidades PostgreSQL de una versión compatible con el respaldo. No instalar el SDK o Node en la VM si solo se ejecutan los paquetes ya compilados.

## Configuración y publicación

1. Transferir por SSH el paquete y el SHA-256; verificar la huella antes de extraerlo en una carpeta nueva de versiones.
2. Crear las identidades y carpetas dedicadas. La API puede escribir solo en sus carpetas de evidencias y claves. Código y frontend pertenecen al administrador y no son modificables por `cet-api`.
3. Crear una base vacía dedicada. Para datos locales existentes, transferir un respaldo coordinado de base y evidencias, verificarlo y restaurarlo en ese destino nuevo. No ejecutar el esquema inicial sobre una base poblada. Reaplicar permisos y cerrar sesiones restauradas antes de publicar.
4. Crear un certificado RSA privado vigente para cifrar el anillo de Data Protection. Este certificado es independiente del certificado HTTPS. Conservarlo junto con las copias de claves de sesión, con acceso restringido y fuera de Git.
5. Completar `entorno.example`, sustituyendo dominio, puerto y conexión. Instalarlo como `/etc/constructora-eder-torres/entorno`, propietario root y modo 0600. El archivo es de systemd y no debe ejecutarse con `source`.
6. Obtener el certificado HTTPS para el DNS publicado. Validar primero el desafío HTTP y luego configurar HTTPS con renovación automática. Conservar la política de renovación de los demás sitios.
7. Completar `nginx.conf.template` con el dominio y el puerto elegido; instalar el fragmento de cabeceras generado y el sitio dedicado. Validar con `nginx -t` antes de recargar. No publicar archivos de configuración ni directorios de evidencias.
8. Instalar la unidad systemd y seleccionar la versión mediante el enlace `actual`. Arrancar la API, comprobar su estado y después habilitar el sitio. Un error de validación de Production debe corregirse, sin desactivar los controles de seguridad.
9. Verificar HTTPS, redirección HTTP, carga de aplicación, salud de PostgreSQL, inicio de sesión, cookie Secure, GPS desde un teléfono, representación de obras en el mapa y subida/visualización de fotografías. Confirmar marcaciones offline y sincronización con GPS disponible y sesión válida.

`entorno.example`, el servicio y el sitio Nginx son plantillas operativas. Requieren completar el destino y verificar las dependencias existentes; copiarlos sin esa revisión no deja el despliegue funcionando.

## Verificaciones efectuadas

- Compilación Release de API, respaldos y frontend.
- Diez pruebas operativas de respaldos aprobadas en el entorno local.
- Corrección de la entrada de `pg_restore` en Linux: el archivo se pasa como argumento; Windows conserva la transmisión de bytes para rutas Unicode. La verificación y los respaldos reales se ejecutaron correctamente en Ubuntu.
- Restauración en una base nueva, con 1 fotografía y coincidencia SHA-256; las sesiones importadas se revocaron antes de publicar.
- Respuesta HTTPS 200 de aplicación y `/salud/lista`, con PostgreSQL saludable.
- Inicio de sesión, cookie Secure/HttpOnly, consulta según permisos y cierre para seis roles: Gerente General, Gerente de Operaciones, Administrativo, Ingeniero Residente, Higiene y Seguridad y Trabajador. El trabajador recibió 403 al consultar obras y pudo consultar sus asignaciones propias.
- Rechazo de una solicitud de escritura sin CSRF. Las solicitudes autenticadas de comprobación no alteraron registros operativos.
- Descarga autenticada de fotografía y coincidencia de su hash con la base.
- Comprobación visual del panel y Google Maps, con 8 ubicaciones; selección y radio de una obra de simulación.
- Renovación de certificado simulada con Certbot completada correctamente.
- Respaldo coordinado y material de recuperación de sesiones verificados en la VM y en una copia local externa a ella.

Evidencias: `artifacts/ResultadosDespliegue/verificacion-publicacion.json`, `respaldos-linux-ajuste.trx`, `panel-azure.jpg` y `mapa-azure.jpg`. `Tools/Despliegue/VerificarPublicacion.ps1` recibe el archivo privado de accesos de simulación; el informe no contiene contraseñas, cookies ni tokens.

Estas comprobaciones cubren publicación, acceso, consultas y recuperación. El GPS real y el flujo offline completo deben comprobarse desde un teléfono en la obra; no se registraron asistencias ficticias para simular presencia física. No se realizó una prueba de capacidad máxima de esta VM compartida.

## Recuperación y operación

Antes de actualizar, crear y verificar un respaldo coordinado con la herramienta `Respaldos`. Mantener una copia fuera de la VM. El dump PostgreSQL por sí solo no contiene los bytes de las fotografías ni los certificados privados.

Ante un fallo de una versión de código, devolver `actual` a la versión anterior y reiniciar exclusivamente `constructora-eder-torres.service`. No revertir datos o migraciones automáticamente. Si cambió el esquema, comprobar primero la compatibilidad del código anterior.

Revisar uso de disco, memoria, errores del servicio, vencimiento del certificado HTTPS y resultado de respaldos. El apagado automático vuelve indisponible la aplicación a partir de su horario; antes de una demostración hay que iniciar la VM.

El temporizador `constructora-eder-torres-respaldo.timer` ejecuta la copia diariamente a las 22:30 de `America/Managua`, con recuperación de una ejecución omitida al encender la VM. Retención: siete días, conservando al menos dos conjuntos verificados. El rol `cet_respaldo` tiene lectura y conexión a la base dedicada, sin permisos de escritura ni administración. El job usa un archivo de entorno root 0600 y se ejecuta con prioridad reducida.

`RespaldarInstancia.py` copia base y fotografías de la misma instantánea con la herramienta existente, verifica sus hashes y conserva certificado/anillo de sesiones en la subcarpeta privada `seguridad`. Este material necesita custodia al trasladarlo. La copia externa verificada del despliegue se guardó en `%LOCALAPPDATA%\ConstructoraEderTorres\RespaldosAzure\azure-cet-respaldo-20261118T234434Z.tar.gz`, con acceso limitado al usuario local y SYSTEM. Las copias diarias posteriores se guardan en la VM; su traslado externo posterior requiere ejecutarse o automatizarse en el equipo/destino elegido.

Certbot tiene renovación programada y un hook que valida/recarga Nginx al renovar. El certificado HTTPS y el certificado privado de Data Protection son independientes. El certificado de Data Protection requiere renovación antes de su vencimiento, conservando el anterior mientras existan claves o respaldos que lo utilicen.

## Referencias técnicas

- [Microsoft: hospedaje ASP.NET Core en Linux con Nginx](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0).
- [Microsoft: instalación de .NET en Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?tabs=dotnet10).
- [Azure: estados y facturación de máquinas virtuales](https://learn.microsoft.com/en-us/azure/virtual-machines/states-billing).
- [Let's Encrypt: certificados y clientes ACME](https://letsencrypt.org/getting-started/).
