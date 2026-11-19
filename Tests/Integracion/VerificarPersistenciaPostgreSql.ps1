[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BinariosPostgreSql,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuracion = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$raizProyecto = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$binarios = (Resolve-Path -LiteralPath $BinariosPostgreSql).Path
foreach ($ejecutable in @('initdb.exe', 'pg_ctl.exe', 'psql.exe', 'pg_dump.exe', 'pg_restore.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $binarios $ejecutable))) {
        throw "No se encontró $ejecutable en la carpeta de binarios."
    }
}

$carpeta = Join-Path $raizProyecto ('artifacts\VerificacionInspeccion\' + [Guid]::NewGuid().ToString('N'))
$datos = Join-Path $carpeta 'datos'
[void](New-Item -ItemType Directory -Path $carpeta)
$registro = Join-Path $carpeta 'postgresql.log'
$herramienta = Join-Path $raizProyecto "Tools\InspeccionEsquema\bin\$Configuracion\net10.0\ConstructoraEderTorres.InspeccionEsquema.dll"
if (-not (Test-Path -LiteralPath $herramienta)) {
    throw "Compila primero la solución en $Configuracion."
}

$reservaPuerto = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$reservaPuerto.Start()
$puerto = $reservaPuerto.LocalEndpoint.Port
$reservaPuerto.Stop()

$conexionAnterior = $env:ConnectionStrings__Constructora
$opcionesAnteriores = $env:PGOPTIONS
$inicioIntentado = $false

function EjecutarConsulta {
    param([string]$Consulta)
    & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d postgres -v ON_ERROR_STOP=1 -c $Consulta
    if ($LASTEXITCODE -ne 0) { throw 'Falló una consulta de preparación/verificación en la instancia desechable.' }
}

function Comprobar {
    param([bool]$Condicion, [string]$Mensaje)
    if (-not $Condicion) { throw $Mensaje }
}

function NormalizarComprobaciones {
    param($Inventario)
    # PostgreSQL reubica los casts de estos arrays al restaurar su propio pg_dump.
    # Solo se admite esa reescritura concreta; las otras diferencias se conservan.
    $patron = '\(ARRAY\[((?:\(''[A-Z0-9_]+''::character varying\)::text)(?:, \(''[A-Z0-9_]+''::character varying\)::text)*)\]\)'
    foreach ($tabla in $Inventario.tablas) {
        foreach ($restriccion in ($tabla.restricciones | Where-Object tipo -eq 'c')) {
            $restriccion.definicion = [regex]::Replace($restriccion.definicion, $patron, [System.Text.RegularExpressions.MatchEvaluator]{
                param($coincidencia)
                $valores = [regex]::Replace($coincidencia.Groups[1].Value, '\((''[A-Z0-9_]+'')::character varying\)::text', '$1::character varying')
                return '((ARRAY[' + $valores + '])::text[])'
            })
        }
    }
}

try {
    # Este clúster nuevo contiene únicamente el DDL y datos técnicos de prueba.
    $env:PGOPTIONS = $null
    & (Join-Path $binarios 'initdb.exe') -D $datos -U postgres -A trust --no-locale --encoding=UTF8
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar la instancia PostgreSQL desechable.' }

    $inicioIntentado = $true
    $inicio = Start-Process -FilePath (Join-Path $binarios 'pg_ctl.exe') -WindowStyle Hidden -PassThru -ArgumentList @(
        'start', '-D', ('"' + $datos + '"'), '-l', ('"' + $registro + '"'),
        '-o', ('"-h 127.0.0.1 -p ' + $puerto + ' -c shared_buffers=16MB -c max_connections=10"'),
        '-w', '-t', '30'
    )
    if (-not $inicio.WaitForExit(35000) -or $inicio.ExitCode -ne 0) { throw 'No se pudo iniciar la instancia desechable; revisa el registro local.' }

    $modelo = Join-Path $raizProyecto 'Tests\Integracion\Datos\EsquemaOriginalPostgreSql.sql'
    & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d postgres -v ON_ERROR_STOP=1 -f $modelo
    if ($LASTEXITCODE -ne 0) { throw 'El DDL de referencia no se pudo cargar en la instancia desechable.' }
    EjecutarConsulta 'CREATE ROLE inspector_pruebas LOGIN;'

    # El inspector no recibe permisos sobre las filas de las tablas de negocio.
    # Se intenta desactivar solo lectura en la cadena para comprobar que la herramienta lo impone.
    $env:ConnectionStrings__Constructora = "Host=127.0.0.1;Port=$puerto;Database=postgres;Username=inspector_pruebas;Options=-c default_transaction_read_only=off"
    $salidaInicial = Join-Path $carpeta 'inventario-inicial.json'
    & dotnet $herramienta --esquema public --salida $salidaInicial
    Comprobar ($LASTEXITCODE -eq 0) 'Falló la inspección del DDL de referencia con el rol limitado.'
    $inicial = Get-Content -LiteralPath $salidaInicial -Raw | ConvertFrom-Json
    Comprobar ($inicial.soloLectura) 'La transacción no se ejecutó como solo lectura.'
    Comprobar ($inicial.tablas.Count -eq 38) 'El inventario no identificó las 38 tablas de referencia.'
    $incidencias = $inicial.tablas | Where-Object nombre -eq 'incidencias_herramienta'
    Comprobar (@($incidencias.columnas | Where-Object nombre -eq 'estado').Count -eq 0) 'El inventario atribuyó al DDL original una columna que no contiene.'
    $rolesPermiso = $inicial.tablas | Where-Object nombre -eq 'roles_permiso'
    Comprobar (@($rolesPermiso.restricciones | Where-Object { $_.tipo -eq 'p' -and $_.columnas.Count -eq 2 }).Count -eq 1) 'No se conservó la clave primaria compuesta.'
    $cuadrilla = $inicial.tablas | Where-Object nombre -eq 'trabajadores_cuadrilla'
    Comprobar (@($cuadrilla.indices | Where-Object { $_.nombre -eq 'ux_trabajador_cuadrilla_activa' -and $_.definicion -match 'WHERE' }).Count -eq 1) 'No se conservó el índice parcial.'

    $cambiosPrueba = @'
ALTER TABLE public.incidencias_herramienta ADD COLUMN estado varchar(20) NOT NULL DEFAULT 'ABIERTA';
ALTER TABLE public.incidencias_herramienta ADD CONSTRAINT ck_estado_prueba CHECK (estado IN ('ABIERTA','RESUELTA')) NOT VALID;
CREATE FUNCTION public.disparador_inspeccion_prueba() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La inspeccion no debe ejecutar disparadores';
END;
$$;
CREATE TRIGGER disparador_inspeccion_prueba BEFORE INSERT OR UPDATE ON public.trabajadores_cuadrilla
FOR EACH ROW EXECUTE FUNCTION public.disparador_inspeccion_prueba();
ALTER TABLE public.trabajadores_cuadrilla DISABLE TRIGGER disparador_inspeccion_prueba;
'@
    EjecutarConsulta $cambiosPrueba
    $salidaPosterior = Join-Path $carpeta 'inventario-posterior.json'
    & dotnet $herramienta --esquema public --salida $salidaPosterior
    Comprobar ($LASTEXITCODE -eq 0) 'Falló la inspección de cambios posteriores simulados.'
    $posterior = Get-Content -LiteralPath $salidaPosterior -Raw | ConvertFrom-Json
    $incidencias = $posterior.tablas | Where-Object nombre -eq 'incidencias_herramienta'
    Comprobar (@($incidencias.columnas | Where-Object { $_.nombre -eq 'estado' -and $_.obligatoria }).Count -eq 1) 'No se detectó estado obligatorio.'
    Comprobar (@($incidencias.restricciones | Where-Object { $_.nombre -eq 'ck_estado_prueba' -and -not $_.validada }).Count -eq 1) 'No se detectó una restricción sin validar.'
    $cuadrilla = $posterior.tablas | Where-Object nombre -eq 'trabajadores_cuadrilla'
    Comprobar (@($cuadrilla.disparadores | Where-Object { $_.nombre -eq 'disparador_inspeccion_prueba' -and $_.habilitacion -eq 'D' }).Count -eq 1) 'No se detectó el disparador deshabilitado.'
    Comprobar (@($posterior.funcionesDisparador | Where-Object { $_.definicion -match 'no debe ejecutar disparadores' }).Count -eq 1) 'No se incluyó la definición de la función del disparador.'

    $huella = (Get-FileHash -LiteralPath $salidaPosterior -Algorithm SHA256).Hash
    & dotnet $herramienta --esquema public --salida $salidaPosterior
    Comprobar ($LASTEXITCODE -eq 2) 'La herramienta no rechazó sobrescribir una evidencia existente.'
    Comprobar ((Get-FileHash -LiteralPath $salidaPosterior -Algorithm SHA256).Hash -eq $huella) 'La evidencia anterior fue modificada.'

    $salidaInvalida = Join-Path $carpeta 'inventario-invalido.json'
    & dotnet $herramienta --esquema "public'; DROP TABLE public.cargos; --" --salida $salidaInvalida
    Comprobar ($LASTEXITCODE -eq 1) 'No se rechazó un nombre de esquema inexistente.'
    Comprobar (-not (Test-Path -LiteralPath $salidaInvalida)) 'Se publicó un inventario inválido.'
    EjecutarConsulta "SELECT count(*) FROM pg_catalog.pg_class AS c JOIN pg_catalog.pg_namespace AS n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','p');"

    $salidaFinal = Join-Path $carpeta 'inventario-final.json'
    & dotnet $herramienta --esquema public --salida $salidaFinal
    Comprobar ($LASTEXITCODE -eq 0) 'Falló la verificación final del esquema.'
    $final = Get-Content -LiteralPath $salidaFinal -Raw | ConvertFrom-Json
    $estructuraAnterior = $posterior | Select-Object -ExcludeProperty capturadoEnUtc | ConvertTo-Json -Depth 100 -Compress
    $estructuraFinal = $final | Select-Object -ExcludeProperty capturadoEnUtc | ConvertTo-Json -Depth 100 -Compress
    Comprobar ($estructuraAnterior -ceq $estructuraFinal) 'La estructura cambió durante las inspecciones.'

    EjecutarConsulta 'CREATE DATABASE verificacion_conciliada;'
    $conciliado = Join-Path $raizProyecto 'database\EsquemaConciliadoPostgreSql.sql'
    & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada -v ON_ERROR_STOP=1 -f $conciliado
    Comprobar ($LASTEXITCODE -eq 0) 'El esquema conciliado no se pudo restaurar en una base vacía desechable.'
    $env:ConnectionStrings__Constructora = "Host=127.0.0.1;Port=$puerto;Database=verificacion_conciliada;Username=inspector_pruebas;Options=-c default_transaction_read_only=off"
    $salidaConciliada = Join-Path $carpeta 'inventario-conciliado.json'
    & dotnet $herramienta --esquema public --salida $salidaConciliada
    Comprobar ($LASTEXITCODE -eq 0) 'No se pudo inspeccionar el esquema conciliado restaurado.'
    $restaurado = Get-Content -LiteralPath $salidaConciliada -Raw | ConvertFrom-Json
    NormalizarComprobaciones $restaurado
    Comprobar ($restaurado.tablas.Count -eq 38) 'La línea base debe conservar sus 38 tablas de negocio.'

    $lineaBase = Join-Path $raizProyecto 'database\RegistrarLineaBaseExistente.sql'
    foreach ($intento in 1..2) {
        & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada -v ON_ERROR_STOP=1 -f $lineaBase
        Comprobar ($LASTEXITCODE -eq 0) 'El registro idempotente de la línea base falló en la instancia desechable.'
    }
    $marcas = & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada -v ON_ERROR_STOP=1 -A -t -c 'SELECT count(*) FROM public."__EFMigrationsHistory";'
    Comprobar ($LASTEXITCODE -eq 0 -and [int]$marcas -eq 1) 'La línea base no se registró una sola vez.'
    $salidaLineaBase = Join-Path $carpeta 'inventario-linea-base.json'
    & dotnet $herramienta --esquema public --salida $salidaLineaBase
    Comprobar ($LASTEXITCODE -eq 0) 'No se pudo verificar el esquema después de registrar la línea base.'
    $conLineaBase = Get-Content -LiteralPath $salidaLineaBase -Raw | ConvertFrom-Json
    NormalizarComprobaciones $conLineaBase
    Comprobar ($conLineaBase.tablas.Count -eq $restaurado.tablas.Count + 1) 'El registro de línea base no añadió únicamente su tabla de historial.'
    $conLineaBase.tablas = @($conLineaBase.tablas | Where-Object nombre -ne '__EFMigrationsHistory')
    $estructuraLineaBase = $conLineaBase | Select-Object -ExcludeProperty capturadoEnUtc | ConvertTo-Json -Depth 100 -Compress
    $estructuraRestaurada = $restaurado | Select-Object -ExcludeProperty capturadoEnUtc | ConvertTo-Json -Depth 100 -Compress
    Comprobar ($estructuraRestaurada -ceq $estructuraLineaBase) 'El registro de línea base alteró la estructura de negocio.'

    # Se verifica la evolución real desde el DDL histórico, sin modificarlo para aparentar una instalación nueva.
    foreach ($nombreScript in @('ActualizarAsistenciaTrabajadores.sql', 'ActualizarCapturaAsistencia.sql', 'ActualizarSesionesRevocables.sql')) {
        $actualizacionAsistencia = Join-Path $raizProyecto ('database\' + $nombreScript)
        foreach ($intento in 1..2) {
            & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada -v ON_ERROR_STOP=1 -f $actualizacionAsistencia
            Comprobar ($LASTEXITCODE -eq 0) 'Falló la actualización idempotente de asistencia propia y precisión GPS.'
        }
    }
    $salidaActualizada = Join-Path $carpeta 'inventario-asistencia-actualizada.json'
    & dotnet $herramienta --esquema public --salida $salidaActualizada
    Comprobar ($LASTEXITCODE -eq 0) 'No se pudo inspeccionar la actualización de asistencia.'
    $actualizado = Get-Content -LiteralPath $salidaActualizada -Raw | ConvertFrom-Json
    NormalizarComprobaciones $actualizado
    $sesiones = @($actualizado.tablas | Where-Object nombre -eq 'sesiones_usuario')
    Comprobar ($sesiones.Count -eq 1 -and $sesiones[0].columnas.Count -eq 5) 'No se creó la tabla técnica de sesiones con sus cinco campos.'
    Comprobar (@($sesiones[0].restricciones | Where-Object { $_.tipo -eq 'f' -and $_.tablaReferenciada -eq 'public.usuarios' }).Count -eq 1) 'La sesión no conserva la FK a usuarios.'
    $actualizado.tablas = @($actualizado.tablas | Where-Object { $_.nombre -notin @('__EFMigrationsHistory','sesiones_usuario') })
    $referencia = Get-Content -LiteralPath (Join-Path $raizProyecto 'Tests\Integracion\Datos\EsquemaPostgreSql.json') -Raw | ConvertFrom-Json
    Comprobar (($actualizado.tablas | ConvertTo-Json -Depth 100 -Compress) -ceq ($referencia.tablas | ConvertTo-Json -Depth 100 -Compress)) 'El esquema actualizado difiere del inventario confirmado.'

    # Respaldo real de una base con datos sintéticos y restauración en otra base nueva.
    & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada -v ON_ERROR_STOP=1 -c "INSERT INTO public.cargos(nombre,descripcion) VALUES ('Respaldo de prueba',U&'Dato sint\00E9tico para comprobar recuperaci\00F3n');"
    Comprobar ($LASTEXITCODE -eq 0) 'No se pudo preparar el dato de recuperación.'
    $respaldo = Join-Path $carpeta 'recuperacion.dump'
    & (Join-Path $binarios 'pg_dump.exe') -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_conciliada --format=custom --no-owner --no-acl --file $respaldo
    Comprobar ($LASTEXITCODE -eq 0) 'Falló pg_dump de la base desechable.'
    $huellaRespaldo = (Get-FileHash -LiteralPath $respaldo -Algorithm SHA256).Hash
    EjecutarConsulta 'CREATE DATABASE verificacion_recuperada;'
    & (Join-Path $binarios 'pg_restore.exe') -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_recuperada --exit-on-error --no-owner --no-acl $respaldo
    Comprobar ($LASTEXITCODE -eq 0) 'Falló pg_restore en la segunda base desechable.'
    $datoRecuperado = & (Join-Path $binarios 'psql.exe') -X -q -w -h 127.0.0.1 -p $puerto -U postgres -d verificacion_recuperada -v ON_ERROR_STOP=1 -A -t -c "SELECT count(*) FROM public.cargos WHERE nombre='Respaldo de prueba' AND descripcion=U&'Dato sint\00E9tico para comprobar recuperaci\00F3n';"
    Comprobar ($LASTEXITCODE -eq 0 -and [int]$datoRecuperado -eq 1) 'El dato no se recuperó correctamente.'
    $env:ConnectionStrings__Constructora = "Host=127.0.0.1;Port=$puerto;Database=verificacion_recuperada;Username=inspector_pruebas"
    $salidaRecuperada = Join-Path $carpeta 'inventario-recuperado.json'
    & dotnet $herramienta --esquema public --salida $salidaRecuperada
    Comprobar ($LASTEXITCODE -eq 0) 'No se pudo inspeccionar la base recuperada.'
    $recuperado = Get-Content -LiteralPath $salidaRecuperada -Raw | ConvertFrom-Json
    NormalizarComprobaciones $recuperado
    Comprobar (@($recuperado.tablas | Where-Object nombre -eq 'sesiones_usuario').Count -eq 1) 'La recuperación perdió la tabla de sesiones.'
    $recuperado.tablas = @($recuperado.tablas | Where-Object { $_.nombre -notin @('__EFMigrationsHistory','sesiones_usuario') })
    Comprobar (($recuperado.tablas | ConvertTo-Json -Depth 100 -Compress) -ceq ($referencia.tablas | ConvertTo-Json -Depth 100 -Compress)) 'La recuperación alteró tablas, claves, índices o disparadores.'
    Comprobar ((Get-FileHash -LiteralPath $respaldo -Algorithm SHA256).Hash -eq $huellaRespaldo) 'El respaldo fue modificado durante la recuperación.'

    Write-Output 'Verificación PostgreSQL aprobada: 38 tablas de negocio, inspección, línea base, migración de asistencia idempotente y recuperación real pg_dump/pg_restore con datos sintéticos.'
    Write-Output "Evidencias de la instancia desechable: $carpeta"
}
finally {
    $env:ConnectionStrings__Constructora = $conexionAnterior
    $env:PGOPTIONS = $opcionesAnteriores
    if ($inicioIntentado) {
        $estado = Start-Process -FilePath (Join-Path $binarios 'pg_ctl.exe') -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
            'status', '-D', ('"' + $datos + '"')
        )
        if ($estado.ExitCode -eq 0) {
            $parada = Start-Process -FilePath (Join-Path $binarios 'pg_ctl.exe') -WindowStyle Hidden -PassThru -Wait -ArgumentList @(
                'stop', '-D', ('"' + $datos + '"'), '-m', 'fast', '-w', '-t', '30'
            )
            if ($parada.ExitCode -ne 0) { throw 'No se pudo detener la instancia desechable; revisa su registro local.' }
        }
    }
}
