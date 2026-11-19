#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$Origen,
    [Parameter(Mandatory)][string]$AccesosSimulacion,
    [Parameter(Mandatory)][string]$Resultado
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Origen.Scheme -ne 'https' -or $Origen.AbsolutePath -ne '/' -or $Origen.UserInfo) {
    throw 'La comprobación requiere un origen HTTPS sin credenciales ni ruta.'
}
$cuentas = @(Get-Content -Raw -LiteralPath $AccesosSimulacion | ConvertFrom-Json | Group-Object Rol | ForEach-Object { $_.Group[0] })
$resultados = [Collections.Generic.List[object]]::new()
$fotoComprobada = $false

function Solicitar {
    param([Net.Http.HttpClient]$Cliente, [string]$Metodo, [string]$Ruta, [int]$Esperado,
        [object]$Cuerpo = $null, [object]$Token = $null)
    $mensaje = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Metodo), [uri]::new($Origen, $Ruta))
    if ($null -ne $Cuerpo) {
        $mensaje.Content = [Net.Http.StringContent]::new(($Cuerpo | ConvertTo-Json -Depth 6 -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    if ($Token) { [void]$mensaje.Headers.TryAddWithoutValidation($Token.cabecera, [string]$Token.token) }
    try {
        $respuesta = $Cliente.Send($mensaje)
        if ([int]$respuesta.StatusCode -ne $Esperado) {
            $estado = [int]$respuesta.StatusCode
            $respuesta.Dispose()
            throw "Código HTTP inesperado en $Metodo $Ruta ($estado, esperado $Esperado)."
        }
        return $respuesta
    }
    finally { $mensaje.Dispose() }
}

foreach ($cuenta in $cuentas) {
    $controlador = [Net.Http.HttpClientHandler]::new()
    $controlador.CookieContainer = [Net.CookieContainer]::new()
    $cliente = [Net.Http.HttpClient]::new($controlador)
    $cliente.Timeout = [TimeSpan]::FromSeconds(30)
    try {
        $anonima = Solicitar $cliente GET '/api/sesion/actual' 401
        $anonima.Dispose()
        if ($resultados.Count -eq 0) {
            $sinToken = Solicitar $cliente POST '/api/sesion/iniciar' 400 @{ nombreUsuario=$cuenta.Usuario; contrasena=$cuenta.Contrasena }
            $sinToken.Dispose()
        }
        $csrf = Solicitar $cliente GET '/api/sesion/antifalsificacion' 200
        $token = $csrf.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $csrf.Dispose()
        $inicio = Solicitar $cliente POST '/api/sesion/iniciar' 200 @{ nombreUsuario=$cuenta.Usuario; contrasena=$cuenta.Contrasena } $token
        $perfil = $inicio.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $inicio.Dispose()
        if ($perfil.roles -notcontains $cuenta.Rol) { throw 'El rol autenticado no coincide con el acceso de simulación.' }
        $cookie = $controlador.CookieContainer.GetCookies($Origen) | Where-Object Name -eq 'cet_sesion'
        if (-not $cookie -or -not $cookie.Secure -or -not $cookie.HttpOnly) { throw 'La cookie de sesión no cumple Secure/HttpOnly.' }
        $actual = Solicitar $cliente GET '/api/sesion/actual' 200
        $actual.Dispose()
        $obrasHttp = if ($perfil.permisos -contains 'obras.consultar') { 200 } else { 403 }
        $obras = Solicitar $cliente GET '/api/obras' $obrasHttp
        $listaObras = if ($obrasHttp -eq 200) { @($obras.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json) } else { @() }
        $obras.Dispose()
        $asignacionesVerificadas = $false
        if ($perfil.permisos -contains 'asistencia.propia.consultar') {
            $asignaciones = Solicitar $cliente GET '/api/asistencias/mis-asignaciones' 200
            $asignaciones.Dispose()
            $asignacionesVerificadas = $true
        }
        if (-not $fotoComprobada -and $cuenta.Rol -eq 'GERENTE_GENERAL') {
            foreach ($obra in $listaObras) {
                $avances = Solicitar $cliente GET "/api/seguimiento/obras/$($obra.idObra)/avances" 200
                $listaAvances = @($avances.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
                $avances.Dispose()
                foreach ($avance in $listaAvances) {
                    $evidencias = Solicitar $cliente GET "/api/seguimiento/avances/$($avance.idAvance)/evidencias" 200
                    $listaEvidencias = @($evidencias.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
                    $evidencias.Dispose()
                    foreach ($evidencia in $listaEvidencias) {
                        $archivo = Solicitar $cliente GET "/api/seguimiento/evidencias/$($evidencia.idEvidencia)/archivo" 200
                        $bytes = $archivo.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                        $huella = [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($bytes))
                        $archivo.Dispose()
                        if ($huella -ne $evidencia.huellaArchivo.ToLowerInvariant()) { throw 'La fotografía no coincide con su huella de base.' }
                        $fotoComprobada = $true
                        break
                    }
                    if ($fotoComprobada) { break }
                }
                if ($fotoComprobada) { break }
            }
        }
        $csrf = Solicitar $cliente GET '/api/sesion/antifalsificacion' 200
        $token = $csrf.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $csrf.Dispose()
        $cierre = Solicitar $cliente POST '/api/sesion/cerrar' 204 -Token $token
        $cierre.Dispose()
        $cerrada = Solicitar $cliente GET '/api/sesion/actual' 401
        $cerrada.Dispose()
        $resultados.Add([ordered]@{ Rol=$cuenta.Rol; InicioSesion=200; CookieSegura=$true;
            ConsultaObras=$obrasHttp; AsignacionesPropias=$asignacionesVerificadas; CierreSesion=204; SesionCerrada=401 })
    }
    finally {
        $cliente.Dispose()
        $cuenta.Contrasena = $null
        $token = $null
    }
}
if (-not $fotoComprobada) { throw 'No se comprobó ninguna fotografía restaurada.' }
$informe = [ordered]@{ InstanteUtc=[DateTime]::UtcNow.ToString('O'); Origen=$Origen.AbsoluteUri;
    RechazoEscrituraSinCsrf=400; RolesComprobados=$resultados; FotografiaDescargadaYHashVerificado=$fotoComprobada;
    Alcance='Sesión, CSRF, permisos de consulta y fotografía por HTTPS; no modifica registros operativos ni valida GPS real del teléfono.' }
$informe | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Resultado -Encoding utf8NoBOM
$informe | ConvertTo-Json -Depth 6
