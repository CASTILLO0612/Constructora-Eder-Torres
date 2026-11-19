using ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class ReglasCicloVidaPruebas
{
    private static readonly DateOnly Hoy = new(2026, 10, 30);
    private static RespuestaActividad Actividad(string estado = EstadosActividad.Planificada, decimal acumulado = 0) =>
        new(1, 1, null, "A", "Actividad", null, "m2", 100m, acumulado, Hoy.AddDays(-5), Hoy.AddDays(5), estado, []);
    private static SolicitudRegistrarAvance Avance(decimal cantidad = 20m) => new(1, Hoy, cantidad);

    [Theory]
    [InlineData(EstadosObra.Finalizada, EstadosObra.Activa)]
    [InlineData(EstadosObra.Finalizada, EstadosObra.Planificada)]
    [InlineData(EstadosObra.Activa, EstadosObra.Planificada)]
    [InlineData(EstadosObra.Planificada, EstadosObra.Finalizada)]
    [InlineData(EstadosObra.Planificada, EstadosObra.Suspendida)]
    public void NoSeReabreNiSeOmiteElInicioDeUnaObra(string anterior, string siguiente) =>
        Assert.False(PoliticaEstadosObra.PermiteTransicion(anterior, siguiente));

    [Theory]
    [InlineData(EstadosActividad.Completa, EstadosActividad.EnProceso)]
    [InlineData(EstadosActividad.Cancelada, EstadosActividad.Planificada)]
    [InlineData(EstadosActividad.Planificada, EstadosActividad.Completa)]
    [InlineData(EstadosActividad.EnProceso, EstadosActividad.Cancelada)]
    public void LaActividadRespetaLosEstadosTerminalesYSuSecuencia(string anterior, string siguiente) =>
        Assert.False(PoliticaEstadosActividad.PermiteTransicion(anterior, siguiente));

    [Fact]
    public void UnRegistroQueAlcanzaLaMetaCompletaLaActividad()
    {
        var resultado = PoliticaRegistroAvance.DeterminarEstado(Avance(35), Actividad(EstadosActividad.EnProceso, 80), EstadosAvance.Registrado, Hoy);
        Assert.True(resultado.EsExitoso);
        Assert.Equal(EstadosActividad.Completa, resultado.Valor);
    }

    [Fact]
    public void UnBorradorNoCompletaNiIniciaLaActividad()
    {
        var resultado = PoliticaRegistroAvance.DeterminarEstado(Avance(150), Actividad(), EstadosAvance.Borrador, Hoy);
        Assert.Equal(EstadosActividad.Planificada, resultado.Valor);
    }

    [Theory]
    [InlineData(EstadosActividad.Completa)]
    [InlineData(EstadosActividad.Cancelada)]
    public void UnaActividadTerminadaNoAdmiteNuevosAvances(string estado) =>
        Assert.Equal("actividad_cerrada", PoliticaRegistroAvance.DeterminarEstado(Avance(), Actividad(estado), EstadosAvance.Registrado, Hoy).Error?.Codigo);

    [Fact]
    public void UnaFechaFuturaNoEsAvanceEjecutado() =>
        Assert.Equal("fecha_avance_futura", PoliticaRegistroAvance.DeterminarEstado(Avance() with { FechaAvance = Hoy.AddDays(1) }, Actividad(), EstadosAvance.Registrado, Hoy).Error?.Codigo);

    [Fact]
    public void UnAltaNoPuedeSimularUnaAnulacion() =>
        Assert.Equal("estado_inicial_avance_invalido", PoliticaRegistroAvance.DeterminarEstado(Avance(), Actividad(), EstadosAvance.Anulado, Hoy).Error?.Codigo);

    [Fact]
    public void LaMetaNoPuedeSerCompletadaMedianteUnEstadoProporcionadoPorElCliente() =>
        Assert.Equal("meta_actividad_inconsistente", PoliticaRegistroAvance.DeterminarEstado(Avance() with { EstadoActividad = EstadosActividad.Completa }, Actividad(), EstadosAvance.Registrado, Hoy).Error?.Codigo);

    [Fact]
    public void ElAvanceFueraDePlazoExigeMotivo()
    {
        var actividad = Actividad() with { FechaFinPlan = Hoy.AddDays(-1) };
        Assert.Equal("justificacion_obligatoria", PoliticaRegistroAvance.DeterminarEstado(Avance(), actividad, EstadosAvance.Registrado, Hoy).Error?.Codigo);
        var valido = PoliticaRegistroAvance.DeterminarEstado(Avance() with { Observaciones = "Retraso por lluvia" }, actividad, EstadosAvance.Registrado, Hoy);
        Assert.Equal(EstadosActividad.Retrasada, valido.Valor);
    }
}
