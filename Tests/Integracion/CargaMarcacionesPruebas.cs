using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using static ConstructoraEderTorres.Pruebas.Integracion.EntornoApiPruebas;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class CargaMarcacionesPruebas(ClusterPostgreSqlDesechableFixture cluster, ITestOutputHelper salida)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task DoceTrabajadoresDistintosMarcanSimultaneamenteSinDuplicacionesNiRechazosPorColisionAjena()
    {
        await using var entorno = await CrearAsync(cluster);
        var escenario = await entorno.CrearEscenarioAsync(12m, -86m, 100m);
        var clientes = new List<HttpClient>();
        var solicitudes = new List<SolicitudRegistrarAsistencia>();
        try
        {
            for (var i = 0; i < 12; i++)
            {
                var trabajador = i == 0 ? escenario.Trabajador : await CrearAsync<RespuestaTrabajador>(entorno.Cliente,
                    "/api/personal/trabajadores", new SolicitudCrearTrabajador(escenario.Trabajador.IdCargo,
                        "CARGA-" + i, "Trabajador", null, "Prueba", null, null, null, null, entorno.HoyLaboral.AddDays(-1)));
                var asignacion = i == 0 ? escenario.Asignacion : await CrearAsync<RespuestaAsignacionTrabajadorObra>(entorno.Cliente,
                    "/api/personal/asignaciones-obra", new SolicitudAsignarTrabajadorObra(trabajador.IdTrabajador,
                        escenario.Obra.IdObra, null, TiposContratacion.Fijo, entorno.HoyLaboral.AddDays(-1)));
                clientes.Add(await entorno.CrearUsuarioAsync(NombresRol.Trabajador, trabajador.IdTrabajador));
                var hora = entorno.AhoraUtc.AddMinutes(-1);
                solicitudes.Add(new(asignacion.IdAsignacion, CalendarioPruebas.Fecha(hora), hora,
                    LatitudEntrada: 12m, LongitudEntrada: -86m, UuidRegistro: Guid.NewGuid(), PrecisionGpsEntradaMetros: 5m));
            }
            var tiempos = await Task.WhenAll(clientes.Select(async (cliente, i) =>
            {
                var reloj = Stopwatch.StartNew();
                using var respuesta = await cliente.PostAsJsonAsync("/api/asistencias", solicitudes[i]);
                Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
                return reloj.Elapsed.TotalMilliseconds;
            }));
            Assert.Equal(12, await entorno.EnBaseAsync(c => c.Asistencias.CountAsync()));
            Assert.Equal(12, await entorno.EnBaseAsync(c => c.Auditorias.CountAsync(a => a.Entidad == "asistencias")));
            var reenvios = await Task.WhenAll(clientes.Select((cliente,i) => CrearAsync<RespuestaAsistencia>(cliente, "/api/asistencias", solicitudes[i])));
            Assert.All(reenvios, r => Assert.True(r.FueIdempotente));
            var p95 = tiempos.Order().ElementAt((int)Math.Ceiling(tiempos.Length*.95)-1);
            salida.WriteLine($"Marcación de 12 trabajadores distintos: p95={p95:F2} ms.");
            Assert.True(p95 < 5000, $"El p95 de marcación fue {p95:F2} ms.");
        }
        finally { foreach (var cliente in clientes) cliente.Dispose(); }
    }
}
