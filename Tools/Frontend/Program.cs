using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Pruebas.Integracion;
using Microsoft.EntityFrameworkCore;

var directorio = new DirectoryInfo(AppContext.BaseDirectory);
while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "ConstructoraEderTorres.sln"))) directorio = directorio.Parent;
var raiz = directorio?.FullName ?? throw new InvalidOperationException("No se encontró la solución.");
var frontend = Path.Combine(raiz, "frontend");
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_CONSTRUCTORAEDERTORRES_PRESENTACION", Path.Combine(raiz, "src", "Presentation"));
if (!File.Exists(Path.Combine(frontend, "dist", "index.html"))) throw new InvalidOperationException("Compila primero la PWA con npm run build.");

await using var cluster = new ClusterPostgreSqlDesechableFixture();
await cluster.InitializeAsync();
await using var entorno = await EntornoApiPruebas.CrearAsync(cluster, ajustes: new Dictionary<string, string?>
{
    ["Logging:LogLevel:Default"] = "Error",
    ["Logging:LogLevel:Microsoft"] = "Error"
    ,["Logging:LogLevel:Microsoft.AspNetCore"] = "Error"
});
var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
var otro = await entorno.CrearEscenarioAsync(12.1m, -86.1m, 150m);
await EntornoApiPruebas.CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente, "/api/personal/asignaciones-obra",
    new SolicitudAsignarTrabajadorObra(otro.Trabajador.IdTrabajador, escenario.Obra.IdObra, null, "FIJO", entorno.HoyLaboral.AddDays(-7)));
await entorno.CrearActividadAsync(escenario);
var actividadOffline=await entorno.CrearActividadAsync(escenario);
await entorno.CrearEppAsync();
await entorno.CrearHerramientaAsync();
var roles = (await entorno.Cliente.GetFromJsonAsync<List<RespuestaRol>>("/api/roles"))!;
var cuentas = new Dictionary<string, string> { ["gerencia"] = EntornoApiPruebas.NombreAdministrador };
foreach (var (nombre, rol, trabajador) in new (string, string, int?)[]
{
    ("trabajador", NombresRol.Trabajador, escenario.Trabajador.IdTrabajador),
    ("residente", NombresRol.IngenieroResidente, otro.Trabajador.IdTrabajador),
    ("seguridad", NombresRol.HigieneSeguridad, null),
    ("administrativo", NombresRol.Administrativo, null),
    ("operaciones", NombresRol.GerenteOperaciones, null)
})
{
    var usuario = "e2e." + nombre;
    await EntornoApiPruebas.CrearAsync<RespuestaUsuario>(entorno.Cliente, "/api/usuarios",
        new SolicitudCrearUsuario(usuario, EntornoApiPruebas.Contrasena, trabajador, [roles.Single(r => r.Nombre == rol).IdRol]));
    cuentas[nombre] = usuario;
}
await using var servidorApi = await ServidorKestrelPruebas.IniciarAsync(entorno);
using var escucha = new TcpListener(IPAddress.Loopback, 0);
escucha.Start(); var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port; escucha.Stop();
var datosEscenario = JsonSerializer.Serialize(new
{
    cuentas, contrasena = EntornoApiPruebas.Contrasena,
    idObra = escenario.Obra.IdObra, codigoObra = escenario.Obra.Codigo,
    idTrabajador = escenario.Trabajador.IdTrabajador,
    idAsignacion = escenario.Asignacion.IdAsignacion
    ,idActividadOffline=actividadOffline.IdActividad
});
var inicioWeb = new ProcessStartInfo("node") { WorkingDirectory = frontend, UseShellExecute = false, CreateNoWindow = true };
inicioWeb.ArgumentList.Add("scripts/ServidorIntegracion.mjs");
inicioWeb.Environment["API_DESTINO"] = servidorApi.Cliente.BaseAddress!.ToString();
inicioWeb.Environment["PUERTO_WEB"] = puerto.ToString(System.Globalization.CultureInfo.InvariantCulture);
using var servidorWeb = Process.Start(inicioWeb) ?? throw new InvalidOperationException("No se pudo iniciar la PWA.");
try
{
    using var http = new HttpClient();
    using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var url = $"http://127.0.0.1:{puerto}";
    while (true)
    {
        try { using var r = await http.GetAsync(url, limite.Token); if (r.IsSuccessStatusCode) break; }
        catch (HttpRequestException) { }
        await Task.Delay(200, limite.Token);
    }
    var ejecutar = new ProcessStartInfo("node")
    {
        WorkingDirectory = frontend, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    ejecutar.ArgumentList.Add("node_modules/@playwright/test/cli.js");
    ejecutar.ArgumentList.Add("test");
    ejecutar.Environment["CET_E2E_URL"] = url;
    ejecutar.Environment["CET_E2E_ESCENARIO"] = datosEscenario;
    using var pruebas = Process.Start(ejecutar) ?? throw new InvalidOperationException("No se pudo iniciar Playwright.");
    var salida = pruebas.StandardOutput.ReadToEndAsync(); var errores = pruebas.StandardError.ReadToEndAsync();
    using var tiempo = new CancellationTokenSource(TimeSpan.FromMinutes(12));
    try { await pruebas.WaitForExitAsync(tiempo.Token); }
    catch { pruebas.Kill(entireProcessTree: true); throw; }
    Console.Write(await salida); Console.Error.Write(await errores);
    Environment.ExitCode = pruebas.ExitCode;
    if (pruebas.ExitCode == 0)
    {
        var filas = await entorno.EnBaseAsync(c => c.Asistencias.Where(a => a.IdAsignacion == escenario.Asignacion.IdAsignacion).Select(a => new { a.UuidRegistro, a.Estado }).ToListAsync());
        if (filas.Count != 2 || filas.Select(a => a.UuidRegistro).Distinct().Count() != 2 || filas.Any(a => a.Estado != "COMPLETA"))
            throw new InvalidOperationException("La verificación de PostgreSQL detectó asistencia incompleta o duplicada.");
        Console.WriteLine("PostgreSQL desechable: dos jornadas completas con UUID únicos; base empresarial sin modificaciones.");
    }
}
finally
{
    if (!servidorWeb.HasExited) { servidorWeb.Kill(entireProcessTree: true); await servidorWeb.WaitForExitAsync(); }
}
