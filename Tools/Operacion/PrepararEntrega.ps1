#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuracion = 'Release')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$raizProyecto = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$identificador = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$carpetaEntrega = Join-Path $raizProyecto ('artifacts/Entregas/' + $identificador)
[void](New-Item -ItemType Directory -Path $carpetaEntrega)

function PublicarProyecto {
    param([string]$Proyecto, [string]$Destino)
    & dotnet publish (Join-Path $raizProyecto $Proyecto) --configuration $Configuracion --no-build --no-restore --output $Destino
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar uno de los componentes de entrega.' }
}

PublicarProyecto 'src/Presentation/ConstructoraEderTorres.Presentacion.csproj' (Join-Path $carpetaEntrega 'Api')
PublicarProyecto 'Tools/Respaldos/ConstructoraEderTorres.Respaldos.csproj' (Join-Path $carpetaEntrega 'Respaldos')
[void](New-Item -ItemType Directory -Path (Join-Path $carpetaEntrega 'Operacion'))
$documentos = @('OperacionYRestauracion.md', 'IntegracionApi.md', 'DespliegueAzureVm.md', 'ConfiguracionProduccion.ejemplo.json')
foreach ($nombre in $documentos) {
    Copy-Item -LiteralPath (Join-Path $raizProyecto ('docs/' + $nombre)) -Destination (Join-Path $carpetaEntrega 'Operacion')
}
foreach ($archivoSql in Get-ChildItem -LiteralPath (Join-Path $raizProyecto 'database') -File -Filter '*.sql') {
    Copy-Item -LiteralPath $archivoSql.FullName -Destination (Join-Path $carpetaEntrega 'Operacion')
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ConfigurarRolEjecucion.sql') -Destination (Join-Path $carpetaEntrega 'Operacion')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DepurarSesionesVencidas.sql') -Destination (Join-Path $carpetaEntrega 'Operacion')
$archivos = @(Get-ChildItem -LiteralPath $carpetaEntrega -File -Recurse)
if (@($archivos | Where-Object { $_.Name -eq 'secrets.json' -or $_.Extension -in @('.pfx','.p12','.pem','.key') }).Count -gt 0) {
    throw 'La entrega contiene un archivo de secretos o certificado; revise las reglas de publicación.'
}
$revision = & git -C $raizProyecto rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'No se pudo identificar la revisión Git.' }
$cambios = @(& git -C $raizProyecto status --porcelain)
$inventario = @($archivos | Sort-Object FullName | ForEach-Object {
    [ordered]@{ Ruta = [IO.Path]::GetRelativePath($carpetaEntrega, $_.FullName).Replace('\','/'); Bytes = $_.Length;
        Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
[ordered]@{ Version = 1; CreadaUtc = (Get-Date).ToUniversalTime().ToString('O'); RevisionGit = $revision.Trim();
    ArbolConCambios = ($cambios.Count -gt 0); Configuracion = $Configuracion; Archivos = $inventario } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $carpetaEntrega 'manifiesto-entrega.json') -Encoding utf8NoBOM
$archivoZip = $carpetaEntrega + '.zip'
Compress-Archive -LiteralPath $carpetaEntrega -DestinationPath $archivoZip -CompressionLevel Optimal
[ordered]@{ Carpeta = $carpetaEntrega; Archivo = $archivoZip; Sha256 = (Get-FileHash -LiteralPath $archivoZip -Algorithm SHA256).Hash.ToLowerInvariant() } |
    ConvertTo-Json | Write-Output
