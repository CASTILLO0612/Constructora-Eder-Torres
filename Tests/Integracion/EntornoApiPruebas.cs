using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

internal sealed class TiempoControlado(DateTime? instanteInicialUtc = null) : TimeProvider
{
    private long _ticks = (instanteInicialUtc ?? DateTime.UtcNow).Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Avanzar(TimeSpan lapso) => Interlocked.Add(ref _ticks, lapso.Ticks);
}

internal sealed record EscenarioObra(RespuestaObra Obra, RespuestaTrabajador Trabajador,
    RespuestaAsignacionTrabajadorObra Asignacion);

internal sealed class EntornoApiPruebas : IAsyncDisposable
{
    public const string NombreAdministrador = "administrador.pruebas";
    public const string Contrasena = "ClaveDePruebasAPI2026!";
    public static byte[] FotoPng => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private readonly string _conexionControl;
    private readonly string _nombreBase;
    public string Conexion { get; }
    public string CarpetaEvidencias { get; }
    public WebApplicationFactory<Program> Fabrica { get; }
    public HttpClient Cliente { get; }
    public DateTime AhoraUtc => Fabrica.Services.GetRequiredService<IRelojSistema>().AhoraUtc;
    public DateOnly HoyLaboral => Fabrica.Services.GetRequiredService<ICalendarioLaboral>().Hoy;

    private EntornoApiPruebas(string conexionControl, string nombreBase, string conexion,
        string carpetaEvidencias, WebApplicationFactory<Program> fabrica)
    {
        _conexionControl = conexionControl;
        _nombreBase = nombreBase;
        Conexion = conexion;
        CarpetaEvidencias = carpetaEvidencias;
        Fabrica = fabrica;
        Cliente = ClienteApiPruebas.Crear(fabrica);
    }

    public static async Task<EntornoApiPruebas> CrearAsync(ClusterPostgreSqlDesechableFixture cluster,
        TiempoControlado? tiempo = null, bool iniciarSesion = true, bool inicializar = true,
        IReadOnlyDictionary<string, string?>? ajustes = null,
        Action<IServiceCollection>? configurarServicios = null)
    {
        // Nombres generados únicamente por el fixture; nunca recibe un nombre de base del usuario.
        var nombre = "pruebas_" + Guid.NewGuid().ToString("N");
        var control = new NpgsqlConnectionStringBuilder(cluster.CadenaConexion) { Pooling = false }.ConnectionString;
        await using (var conexion = new NpgsqlConnection(control))
        {
            await conexion.OpenAsync();
            await using var crear = new NpgsqlCommand($"CREATE DATABASE {nombre} TEMPLATE postgres", conexion);
            await crear.ExecuteNonQueryAsync();
        }
        var cadena = new NpgsqlConnectionStringBuilder(cluster.CadenaConexion) { Database = nombre }.ConnectionString;
        var carpeta = Path.Combine(cluster.CarpetaEvidenciasPruebas, nombre);
        var fabrica = cluster.CrearFabricaWeb().WithWebHostBuilder(constructor =>
        {
            constructor.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Constructora"] = cadena,
                    ["Evidencias:RutaRaiz"] = carpeta
                });
                if (ajustes is not null) configuracion.AddInMemoryCollection(ajustes);
            });
            if (tiempo is not null)
                constructor.ConfigureServices(servicios => { servicios.RemoveAll<TimeProvider>(); servicios.AddSingleton<TimeProvider>(tiempo); });
            if (configurarServicios is not null) constructor.ConfigureServices(configurarServicios);
        });
        var entorno = new EntornoApiPruebas(control, nombre, cadena, carpeta, fabrica);
        try
        {
            if (inicializar)
            {
                await using var alcance = fabrica.Services.CreateAsyncScope();
                var resultado = await alcance.ServiceProvider.GetRequiredService<IInicializadorSeguridad>()
                    .CrearAdministradorInicialAsync(NombreAdministrador, Contrasena);
                Assert.True(resultado.EsExitoso, resultado.Error?.Mensaje);
            }
            if (iniciarSesion) await AutenticarAsync(entorno.Cliente, NombreAdministrador, Contrasena);
            return entorno;
        }
        catch { await entorno.DisposeAsync(); throw; }
    }

    public static async Task AutenticarAsync(HttpClient cliente, string nombre, string contrasena)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/iniciar", new SolicitudIniciarSesion(nombre, contrasena));
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
    }

    public static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object solicitud)
    {
        using var respuesta = await cliente.PostAsJsonAsync(ruta, solicitud);
        Assert.True(respuesta.IsSuccessStatusCode, $"{ruta}: {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Created && respuesta.Headers.Location is { } ubicacion)
        {
            using var consulta = await cliente.GetAsync(ubicacion);
            Assert.Equal(System.Net.HttpStatusCode.OK, consulta.StatusCode);
        }
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }

    public async Task<T> EnBaseAsync<T>(Func<ContextoConstructora, Task<T>> consulta)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await consulta(alcance.ServiceProvider.GetRequiredService<ContextoConstructora>());
    }

    public async Task EjecutarSqlAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(Conexion);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<EscenarioObra> CrearEscenarioAsync(decimal latitud = 12m, decimal longitud = -86m, decimal radioMetros = 50000m,
        string? nombreCargo = null)
    {
        var codigo = Guid.NewGuid().ToString("N")[..10];
        var hoy = HoyLaboral;
        var cargo = await CrearAsync<RespuestaCargo>(Cliente, "/api/personal/cargos", new SolicitudCargo(nombreCargo ?? "Residente " + codigo, null));
        var trabajador = await CrearAsync<RespuestaTrabajador>(Cliente, "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(cargo.IdCargo, "PR-" + codigo, "Persona", null, "Prueba", null, null, null, null, hoy.AddDays(-30)));
        var obra = await CrearAsync<RespuestaObra>(Cliente, "/api/obras",
            new SolicitudCrearObra("OB-" + codigo, "Obra de pruebas", null, "Dirección de pruebas", hoy.AddDays(-30), hoy.AddMonths(3), EstadosObra.Activa,
                latitud, longitud, radioMetros));
        var asignacion = await CrearAsync<RespuestaAsignacionTrabajadorObra>(Cliente, "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabajador.IdTrabajador, obra.IdObra, null, TiposContratacion.Fijo, hoy.AddDays(-30)));
        return new(obra, trabajador, asignacion);
    }

    public Task<RespuestaActividad> CrearActividadAsync(EscenarioObra escenario) => CrearAsync<RespuestaActividad>(Cliente,
        "/api/seguimiento/actividades", new SolicitudCrearActividad(escenario.Obra.IdObra, escenario.Asignacion.IdAsignacion,
            "A-" + Guid.NewGuid().ToString("N")[..8], "Actividad de pruebas", null, "m2", 10000m,
            escenario.Obra.FechaInicioPlan, escenario.Obra.FechaFinPlan));

    public async Task<RespuestaEpp> CrearEppAsync(int? idTipo = null)
    {
        var tipo = idTipo ?? (await CrearAsync<RespuestaTipoEpp>(Cliente, "/api/epp/tipos",
            new SolicitudCrearTipoEpp("Casco " + Guid.NewGuid().ToString("N")[..8], null, null))).IdTipoEpp;
        return await CrearAsync<RespuestaEpp>(Cliente, "/api/epp/entradas",
            new SolicitudRegistrarEntradaEpp(tipo, "E-" + Guid.NewGuid().ToString("N")[..10], null, CalendarioPruebas.Hoy));
    }

    public async Task<RespuestaHerramienta> CrearHerramientaAsync()
    {
        var categoria = await CrearAsync<RespuestaCategoriaHerramienta>(Cliente, "/api/herramientas/categorias",
            new SolicitudCrearCategoriaHerramienta("Categoría " + Guid.NewGuid().ToString("N")[..8], null));
        var tipo = await CrearAsync<RespuestaTipoHerramienta>(Cliente, "/api/herramientas/tipos",
            new SolicitudCrearTipoHerramienta(categoria.IdCategoria, "Taladro", null));
        return await CrearAsync<RespuestaHerramienta>(Cliente, "/api/herramientas",
            new SolicitudRegistrarHerramienta(tipo.IdTipoHerramienta, "H-" + Guid.NewGuid().ToString("N")[..10], null, null, null, CalendarioPruebas.Hoy));
    }

    public async Task<HttpClient> CrearUsuarioAsync(string rol, int? trabajador = null)
    {
        var roles = (await Cliente.GetFromJsonAsync<List<RespuestaRol>>("/api/roles"))!;
        var usuario = "usuario." + Guid.NewGuid().ToString("N")[..8];
        await CrearAsync<RespuestaUsuario>(Cliente, "/api/usuarios",
            new SolicitudCrearUsuario(usuario, Contrasena, trabajador, [roles.Single(r => r.Nombre == rol).IdRol]));
        var cliente = ClienteApiPruebas.Crear(Fabrica);
        await AutenticarAsync(cliente, usuario, Contrasena);
        return cliente;
    }

    public async ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        await Fabrica.DisposeAsync();
        await using var conexion = new NpgsqlConnection(_conexionControl);
        await conexion.OpenAsync();
        await using var eliminar = new NpgsqlCommand($"DROP DATABASE {_nombreBase} WITH (FORCE)", conexion);
        await eliminar.ExecuteNonQueryAsync();
    }
}
