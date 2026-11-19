using System.Net;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Configuracion;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using ConstructoraEderTorres.Simulacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

if (args is not ["--aplicar"])
{
    Console.WriteLine("Carga explícita e idempotente de datos ficticios en constructora_eder local: dotnet run --project Tools/Simulacion -- --aplicar");
    return 2;
}
try
{
    var configuracion = new ConfigurationBuilder()
        .AddJsonFile(Path.GetFullPath("src/Presentation/appsettings.json"))
        .AddUserSecrets<ConjuntoDemostracion>(optional: true).AddEnvironmentVariables().Build();
    var cadena = configuracion.GetConnectionString("Constructora") ?? throw new InvalidOperationException("Configure ConnectionStrings:Constructora fuera del repositorio.");
    var destino = new NpgsqlConnectionStringBuilder(cadena);
    if (destino.Database != "constructora_eder" || !(destino.Host == "localhost" || IPAddress.TryParse(destino.Host, out var ip) && IPAddress.IsLoopback(ip)))
        throw new InvalidOperationException("Esta herramienta solo admite la base local constructora_eder.");
    var servicios = new ServiceCollection().AddLogging(o => o.AddConsole().SetMinimumLevel(LogLevel.Warning));
    servicios.AddSingleton<IConfiguration>(configuracion);
    servicios.AddSingleton(TimeProvider.System);
    servicios.AddSingleton(new PaginacionConsulta(1, 1000));
    servicios.AddScoped<ContextoSimulacion>();
    servicios.AddScoped<IContextoUsuarioActual>(p => p.GetRequiredService<ContextoSimulacion>());
    servicios.AgregarInfraestructura(configuracion, Path.GetFullPath("src/Presentation"));
    await using var proveedor = servicios.BuildServiceProvider();
    await using var alcance = proveedor.CreateAsyncScope();
    var baseDatos = alcance.ServiceProvider.GetRequiredService<ContextoConstructora>();
    await using var transaccion = await baseDatos.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    await baseDatos.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('cet.simulacion.v1'))");
    if (await baseDatos.Obras.AnyAsync(o => o.Codigo == ConjuntoDemostracion.PrimerCodigoObra))
    {
        Console.WriteLine("El conjunto SIM-CET ya está cargado. Se conservan datos y accesos existentes; no se duplica ni reinicia la simulación.");
        return 0;
    }
    var hoy = alcance.ServiceProvider.GetRequiredService<ICalendarioLaboral>().Hoy;
    var idActor = await (from u in baseDatos.Usuarios
                         join ur in baseDatos.UsuariosRol on u.IdUsuario equals ur.IdUsuario
                         join r in baseDatos.Roles on ur.IdRol equals r.IdRol
                         where u.Estado == EstadosUsuario.Activo && r.Activo && r.Nombre == NombresRol.GerenteGeneral
                           && ur.FechaInicio <= hoy && (ur.FechaFin == null || ur.FechaFin >= hoy)
                         orderby u.IdUsuario
                         select u.IdUsuario).FirstOrDefaultAsync();
    if (idActor == 0) throw new InvalidOperationException("Se requiere un administrador activo existente; la herramienta no crea ni altera la primera cuenta.");
    var actor = await alcance.ServiceProvider.GetRequiredService<IPuertoSeguridad>().ConstruirContextoUsuarioActivoAsync(idActor, hoy, IPAddress.Loopback);
    alcance.ServiceProvider.GetRequiredService<ContextoSimulacion>().Usuario = actor ?? throw new InvalidOperationException("No se pudo resolver el administrador existente.");
    var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConstructoraEderTorres", "Simulacion");
    Directory.CreateDirectory(carpeta);
    var rutaAccesos = Path.Combine(carpeta, "accesos-demo.json");
    var pendiente = rutaAccesos + ".pendiente";
    if (File.Exists(rutaAccesos) || File.Exists(pendiente))
        throw new InvalidOperationException("Ya existe un archivo local de accesos de otra carga. Consérvelo antes de preparar una simulación nueva.");
    var conjunto = new ConjuntoDemostracion(alcance.ServiceProvider);
    var resultado = await conjunto.CargarAsync();
    await File.WriteAllTextAsync(pendiente, JsonSerializer.Serialize(resultado.Accesos, new JsonSerializerOptions { WriteIndented = true }));
    await transaccion.CommitAsync();
    File.Move(pendiente, rutaAccesos, overwrite: false);
    Console.WriteLine(JsonSerializer.Serialize(resultado.Cantidades, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Accesos ficticios guardados solo en el archivo local: {rutaAccesos}");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error is ExcepcionPersistencia persistencia ? $"{persistencia.Error.Codigo}: {persistencia.Error.Mensaje}" : error.Message);
    return 1;
}

namespace ConstructoraEderTorres.Simulacion
{
    internal sealed class ContextoSimulacion : IContextoUsuarioActual
    {
        public UsuarioAutenticado? Usuario { get; set; }
        public UsuarioAutenticado? ObtenerUsuario() => Usuario;
    }
}
