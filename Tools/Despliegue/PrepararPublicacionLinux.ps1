#requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$raiz = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$version = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$salida = Join-Path $raiz "artifacts/DespliegueLinux/$version"
[void](New-Item -ItemType Directory -Path $salida)

Push-Location $raiz
try {
    foreach ($proyecto in @('src/Presentation/ConstructoraEderTorres.Presentacion.csproj', 'Tools/Respaldos/ConstructoraEderTorres.Respaldos.csproj')) {
        & dotnet restore $proyecto --locked-mode
        if ($LASTEXITCODE -ne 0) { throw "Falló restore de $proyecto." }
        $nombre = if ($proyecto.StartsWith('src/')) { 'Api' } else { 'Respaldos' }
        & dotnet publish $proyecto --configuration Release --no-restore --output (Join-Path $salida $nombre)
        if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $proyecto." }
        foreach ($archivoLocal in @('appsettings.Development.json', 'web.config')) {
            $rutaLocal = Join-Path $salida "$nombre/$archivoLocal"
            if (Test-Path -LiteralPath $rutaLocal) { Remove-Item -LiteralPath $rutaLocal }
        }
    }

    # La API se consume bajo el mismo origen del proxy HTTPS.
    $apiBaseAnterior = [Environment]::GetEnvironmentVariable('VITE_API_BASE', 'Process')
    $env:VITE_API_BASE = ''
    Push-Location (Join-Path $raiz 'frontend')
    try {
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del frontend.' }
    }
    finally {
        Pop-Location
        [Environment]::SetEnvironmentVariable('VITE_API_BASE', $apiBaseAnterior, 'Process')
    }
    Copy-Item -LiteralPath (Join-Path $raiz 'frontend/dist') -Destination (Join-Path $salida 'Web') -Recurse
    $operacion = Join-Path $salida 'Operacion'
    [void](New-Item -ItemType Directory -Path $operacion)
    foreach ($nombre in @('EsquemaConciliadoPostgreSql.sql', 'RegistrarLineaBaseExistente.sql',
        'ActualizarAsistenciaTrabajadores.sql', 'ActualizarCapturaAsistencia.sql', 'ActualizarSesionesRevocables.sql')) {
        Copy-Item -LiteralPath (Join-Path $raiz "database/$nombre") -Destination $operacion
    }
    foreach ($nombre in @('ConfigurarRolEjecucion.sql', 'DepurarSesionesVencidas.sql')) {
        Copy-Item -LiteralPath (Join-Path $raiz "Tools/Operacion/$nombre") -Destination $operacion
    }
    foreach ($nombre in @('InspeccionarServidor.sh', 'InstalarInstancia.py', 'ConfigurarHttps.py', 'RespaldarInstancia.py',
        'constructora-eder-torres.service', 'constructora-eder-torres-respaldo.service', 'constructora-eder-torres-respaldo.timer',
        'nginx.conf.template', 'entorno.example')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $nombre) -Destination $operacion
    }
    & node (Join-Path $PSScriptRoot 'GenerarCabecerasNginx.mjs') (Join-Path $operacion 'cabeceras-web.conf')
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron generar las cabeceras del frontend.' }

    $archivos = @(Get-ChildItem -LiteralPath $salida -File -Recurse)
    if (@($archivos | Where-Object { $_.Name -match '^(secrets|appsettings\.Development)\.json$' -or
        $_.Name -like '.env*' -or $_.Extension -in @('.pfx','.p12','.pem','.key','.dump') }).Count -gt 0) {
        throw 'El paquete contiene configuración local o material privado; no se debe subir.'
    }
    $revision = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo obtener la revisión Git.' }
    $cambios = @(& git status --porcelain)
    $inventario = @($archivos | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Ruta = [IO.Path]::GetRelativePath($salida, $_.FullName).Replace('\','/'); Bytes = $_.Length;
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    [ordered]@{ Version = $version; Plataforma = 'linux-x64'; RequiereRuntime = 'Microsoft.AspNetCore.App 10.0';
        RevisionGit = $revision.Trim(); ArbolConCambios = ($cambios.Count -gt 0); Archivos = $inventario } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $salida 'manifiesto.json') -Encoding utf8NoBOM
    $archivo = "$salida.tar.gz"
    & tar -czf $archivo -C $salida .
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compresión de la publicación.' }
    $huella = (Get-FileHash -LiteralPath $archivo -Algorithm SHA256).Hash.ToLowerInvariant()
    "$huella  $([IO.Path]::GetFileName($archivo))" | Set-Content -LiteralPath "$archivo.sha256" -Encoding ascii
    [ordered]@{ Version = $version; Carpeta = $salida; Archivo = $archivo; Sha256 = $huella } | ConvertTo-Json
}
finally { Pop-Location }
