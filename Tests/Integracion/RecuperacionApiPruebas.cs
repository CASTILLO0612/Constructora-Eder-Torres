using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Infraestructura.Evidencias;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class RecuperacionApiPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    private sealed class AcusePerdido : DbTransactionInterceptor
    {
        private int _armado;
        public void Armar() => Interlocked.Exchange(ref _armado, 1);
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => Interlocked.Exchange(ref _armado, 0) == 1
                ? Task.FromException(new IOException("Se perdió el acuse después de confirmar la transacción.")) : Task.CompletedTask;
    }

    private sealed class AlmacenamientoInterrumpido(IAlmacenamientoEvidencias original, Func<Task> interrumpir) : IAlmacenamientoEvidencias
    {
        private int _primero = 1;
        public long MaximoBytes => original.MaximoBytes;
        public async Task<(string RutaRelativa, string HuellaSha256)> GuardarEvidenciaAsync(int idObra, long idAvance,
            string nombreArchivoOriginal, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
        {
            var resultado = await original.GuardarEvidenciaAsync(idObra, idAvance, nombreArchivoOriginal, contenido, cancelacion);
            if (Interlocked.Exchange(ref _primero, 0) == 1) await interrumpir();
            return resultado;
        }
        public Task EliminarAsync(string rutaRelativa, CancellationToken cancelacion = default) => original.EliminarAsync(rutaRelativa, cancelacion);
        public Stream? AbrirLectura(string rutaRelativa) => original.AbrirLectura(rutaRelativa);
    }

    private static int Archivos(string carpeta) => Directory.Exists(carpeta)
        ? Directory.EnumerateFiles(carpeta, "*", SearchOption.AllDirectories).Count() : 0;

    private static async Task<SolicitudRegistrarEvidencia> PrepararFotoAsync(EntornoApiPruebas entorno)
    {
        var escenario = await entorno.CrearEscenarioAsync();
        var actividad = await entorno.CrearActividadAsync(escenario);
        var avance = await CrearAsync<RespuestaAvance>(entorno.Cliente, "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(actividad.IdActividad, entorno.HoyLaboral, 1m));
        return new(avance.IdAvance, TiposEvidencia.Fotografia, "captura.png", FotoPng,
            CapturadaEn: entorno.AhoraUtc, Latitud: 12m, Longitud: -86m, UuidRegistro: Guid.NewGuid());
    }

    [Fact]
    public async Task AcuseDeCommitPerdidoConservaLaEvidenciaYElReenvioRecuperaLaConfirmacion()
    {
        var fallo = new AcusePerdido();
        await using var entorno = await CrearAsync(cluster, configurarServicios:
            servicios => servicios.AddDbContext<ContextoConstructora>(opciones => opciones.AddInterceptors(fallo)));
        var foto = await PrepararFotoAsync(entorno);
        fallo.Armar();
        using var interrumpida = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", foto);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, interrumpida.StatusCode);
        using var error = await JsonDocument.ParseAsync(await interrumpida.Content.ReadAsStreamAsync());
        Assert.Equal("confirmacion_no_verificable", error.RootElement.GetProperty("codigo").GetString());
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
        Assert.Equal(1, Archivos(entorno.CarpetaEvidencias));
        var reenvio = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", foto);
        Assert.True(reenvio.FueIdempotente);
        using var descarga = await entorno.Cliente.GetAsync($"/api/seguimiento/evidencias/{reenvio.IdEvidencia}/archivo");
        Assert.Equal(FotoPng, await descarga.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "evidencias")));
    }

    [Fact]
    public async Task CaidaRealDePostgreSqlDespuesDeGuardarElArchivoRevierteDatosYLimpiaLaEvidencia()
    {
        await using var entorno = await CrearAsync(cluster, configurarServicios: servicios =>
            servicios.Replace(ServiceDescriptor.Singleton<IAlmacenamientoEvidencias>(proveedor =>
                new AlmacenamientoInterrumpido(new AlmacenamientoEvidenciasLocalConfigurable(
                    proveedor.GetRequiredService<IConfiguration>(), proveedor.GetRequiredService<IWebHostEnvironment>().ContentRootPath), cluster.DetenerAsync))));
        var foto = await PrepararFotoAsync(entorno);
        try
        {
            using var respuesta = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", foto);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            Assert.Equal(0, Archivos(entorno.CarpetaEvidencias));
        }
        finally { await cluster.ReiniciarAsync(); }
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "evidencias")));
        var reenvio = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", foto);
        Assert.False(reenvio.FueIdempotente);
        Assert.Equal(1, Archivos(entorno.CarpetaEvidencias));
    }

    [Fact]
    public async Task AlmacenamientoInaccesibleDevuelve503YElMismoUuidFuncionaAlRecuperarse()
    {
        var ruta = Path.Combine(cluster.CarpetaEvidenciasPruebas, Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(ruta, "Un archivo ocupa el destino que debe ser una carpeta.");
        await using var entorno = await CrearAsync(cluster, ajustes: new Dictionary<string,string?> { ["Evidencias:RutaRaiz"] = ruta });
        var foto = await PrepararFotoAsync(entorno);
        using var fallo = await entorno.Cliente.PostAsJsonAsync("/api/seguimiento/evidencias", foto);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, fallo.StatusCode);
        Assert.Equal(0, await entorno.EnBaseAsync(c => c.Evidencias.CountAsync()));
        File.Delete(ruta);
        Directory.CreateDirectory(ruta);
        var reenvio = await CrearAsync<RespuestaEvidencia>(entorno.Cliente, "/api/seguimiento/evidencias", foto);
        Assert.False(reenvio.FueIdempotente);
        Assert.Equal(1, Archivos(ruta));
    }

    [Fact]
    public async Task ReiniciarLaApiConservaLaAsistenciaYPermiteReenviarLaOperacionPorHttpReal()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        var entrada = entorno.AhoraUtc.AddMinutes(-1);
        var solicitud = new SolicitudRegistrarAsistencia(escenario.Asignacion.IdAsignacion, CalendarioPruebas.Fecha(entrada), entrada,
            LatitudEntrada: 12m, LongitudEntrada: -86m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m);
        long id;
        await using (var servidor = await ServidorKestrelPruebas.IniciarAsync(entorno))
        {
            await AutenticarConTokenAsync(servidor.Cliente);
            id = (await CrearAsync<RespuestaAsistencia>(servidor.Cliente, "/api/asistencias", solicitud)).IdAsistencia;
        }
        await using (var servidor = await ServidorKestrelPruebas.IniciarAsync(entorno))
        {
            await AutenticarConTokenAsync(servidor.Cliente);
            var reenvio = await CrearAsync<RespuestaAsistencia>(servidor.Cliente, "/api/asistencias", solicitud);
            Assert.True(reenvio.FueIdempotente);
            Assert.Equal(id, reenvio.IdAsistencia);
        }
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
    }

    [Fact]
    public async Task PostgreSqlNoDisponibleResponde503YSeRecuperaSinRecrearLaBase()
    {
        await using var entorno = await CrearAsync(cluster);
        using var cliente = entorno.Fabrica.CreateClient();
        await AutenticarConTokenAsync(cliente);
        try
        {
            await cluster.DetenerAsync();
            using var respuesta = await cliente.GetAsync("/api/usuarios");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        }
        finally { await cluster.ReiniciarAsync(); }
        using var recuperada = await cliente.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.OK, recuperada.StatusCode);
        Assert.Equal(1, await entorno.EnBaseAsync(c => c.Usuarios.CountAsync()));
    }

    private static async Task AutenticarConTokenAsync(HttpClient cliente)
    {
        var token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
        await AutenticarAsync(cliente, NombreAdministrador, Contrasena);
        cliente.DefaultRequestHeaders.Remove(token.Cabecera);
        token = (await cliente.GetFromJsonAsync<RespuestaAntifalsificacion>("/api/sesion/antifalsificacion"))!;
        cliente.DefaultRequestHeaders.Add(token.Cabecera, token.Token);
    }
}
