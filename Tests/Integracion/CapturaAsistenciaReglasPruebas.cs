using ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Infraestructura.Configuracion;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

public sealed class CapturaAsistenciaReglasPruebas
{
    private static readonly DateTime Ahora = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

    private sealed class TiempoFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Ahora);
    }

    private static (PoliticaCapturaAsistencia Politica, CalendarioLaboral Calendario) Crear()
    {
        var opciones = new OpcionesCapturaAsistencia();
        var reloj = new RelojSistemaUtc(new TiempoFijo());
        var calendario = new CalendarioLaboral(opciones, reloj);
        return (new(reloj, calendario, opciones), calendario);
    }

    [Fact]
    public void ManaguaConservaSuDiaCuandoUtcYaCambioDeFecha()
    {
        var (_, calendario) = Crear();
        Assert.Equal(new DateOnly(2026, 10, 29), calendario.Hoy);
        Assert.Equal(new DateOnly(2026, 10, 29), calendario.ObtenerFecha(new DateTime(2026, 10, 30, 5, 59, 59, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 10, 30), calendario.ObtenerFecha(new DateTime(2026, 10, 30, 6, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(-86400, true)]
    [InlineData(0, true)]
    [InlineData(119, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void CapturaFuturaRespetaExactamenteLosDosMinutos(int diferenciaSegundos, bool aceptada)
    {
        var (politica, _) = Crear();
        var resultado = politica.ValidarInstantes(Ahora.AddSeconds(diferenciaSegundos), null);
        Assert.Equal(aceptada, resultado.EsExitoso);
        if (!aceptada) Assert.Equal("captura_futura", resultado.Error!.Codigo);
    }

    [Fact]
    public void JornadaUsaElDiaDeEntradaLocalAunqueLaSalidaCruceMedianoche()
    {
        var (politica, _) = Crear();
        var entrada = new DateTime(2026, 10, 29, 4, 0, 0, DateTimeKind.Utc); // 22:00 del 28 en Managua.
        var salida = entrada.AddHours(8);
        Assert.True(politica.ValidarInstantes(entrada, salida).EsExitoso);
        Assert.True(politica.ValidarJornada(new DateOnly(2026, 10, 28), entrada).EsExitoso);
        Assert.Equal("fecha_jornada_inconsistente", politica.ValidarJornada(new DateOnly(2026, 10, 29), entrada).Error!.Codigo);
    }

    [Fact]
    public void CapturaHistoricaNoSeSustituyePorLaHoraDeSincronizacion()
    {
        var (politica, _) = Crear();
        var entrada = Ahora.AddDays(-10);
        Assert.True(politica.ValidarInstantes(entrada, entrada.AddHours(8)).EsExitoso);
        Assert.Equal("horas_asistencia_invalidas", politica.ValidarInstantes(entrada, entrada.AddSeconds(-1)).Error!.Codigo);
    }

    [Theory]
    [InlineData(0, 100, true)]
    [InlineData(0, 100.001, false)]
    [InlineData(0.0008, 10, true)]
    [InlineData(0.0008, 12, false)]
    [InlineData(0.001, 1, false)]
    public void RadioIncluyeLaIncertidumbreGpsSinExtenderLaObra(double longitud, double precision, bool aceptada)
    {
        var resultado = PoliticaCapturaAsistencia.ValidarUbicacion(new GeocercaObra(0, 0, 100), 0, (decimal)longitud,
            PoliticaCapturaAsistencia.NormalizarPrecision((decimal)precision));
        Assert.Equal(aceptada, resultado.EsExitoso);
        if (!aceptada) Assert.Equal(longitud >= .001 ? "ubicacion_fuera_de_obra" : "gps_impreciso", resultado.Error!.Codigo);
    }

    [Fact]
    public void PrecisionNoSeReduceAlPersistirYNuncaSePresumeSiElDispositivoNoLaEnvia()
    {
        Assert.Equal(10.01m, PoliticaCapturaAsistencia.NormalizarPrecision(10.000001m));
        Assert.Equal(10m, PoliticaCapturaAsistencia.NormalizarPrecision(10m));
        Assert.Equal("precision_gps_obligatoria", PoliticaCapturaAsistencia.ValidarPrecision(null).Error!.Codigo);
        Assert.False(PoliticaCapturaAsistencia.ValidarPrecision(-1m).EsExitoso);
        Assert.False(PoliticaCapturaAsistencia.ValidarPrecision(100_000_000m).EsExitoso);
        Assert.True(PoliticaCapturaAsistencia.ValidarPrecision(0m).EsExitoso);
    }

    [Fact]
    public void ConfiguracionInvalidaSeRechazaSinCambiarSilenciosamenteLaZona()
    {
        Assert.Throws<InvalidOperationException>(() => new OpcionesCapturaAsistencia { ToleranciaRelojFuturo = TimeSpan.FromSeconds(-1) }.Validar());
        Assert.Throws<TimeZoneNotFoundException>(() => new OpcionesCapturaAsistencia { ZonaHorariaJornada = "Zona/Inexistente" }.Validar());
        var (politica, calendario) = Crear();
        Assert.False(politica.ValidarInstantes(DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified), null).EsExitoso);
        Assert.Throws<ArgumentException>(() => calendario.ObtenerFecha(DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified)));
    }
}
