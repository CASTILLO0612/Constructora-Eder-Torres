#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Api', 'Web')][string]$Componente,
    [ValidateRange(1,65535)][int]$PuertoApi = 5180,
    [ValidateRange(1,65535)][int]$PuertoHttps = 7180,
    [ValidateRange(1,65535)][int]$PuertoWeb = 4173
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$raizProyecto = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (@($PuertoApi, $PuertoHttps, $PuertoWeb | Select-Object -Unique).Count -ne 3) {
    throw 'Selecciona puertos distintos para API, HTTPS y web.'
}
if ($Componente -eq 'Api') {
    $proyectoApi = Join-Path $raizProyecto 'src/Presentation/ConstructoraEderTorres.Presentacion.csproj'
    $direccionesApi = 'https://localhost:' + $PuertoHttps + ';http://localhost:' + $PuertoApi
    Write-Host ('API local: http://localhost:' + $PuertoApi + ' | Scalar: https://localhost:' + $PuertoHttps + '/scalar')
    & dotnet run --project $proyectoApi --configuration Release --no-launch-profile -- --environment Development --urls $direccionesApi --contentRoot (Join-Path $raizProyecto 'src/Presentation')
    exit $LASTEXITCODE
}
$raizFrontend = Join-Path $raizProyecto 'frontend'
$destinoAnterior = $env:API_DESTINO
$puertoAnterior = $env:PUERTO_WEB
try {
    Push-Location -LiteralPath $raizFrontend
    if (-not (Test-Path -LiteralPath 'dist/index.html')) {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'La compilación de la PWA falló.' }
    }
    $env:API_DESTINO = 'http://localhost:' + $PuertoApi
    $env:PUERTO_WEB = $PuertoWeb.ToString([Globalization.CultureInfo]::InvariantCulture)
    & node scripts/ServidorIntegracion.mjs
}
finally {
    $env:API_DESTINO = $destinoAnterior
    $env:PUERTO_WEB = $puertoAnterior
    Pop-Location
}
