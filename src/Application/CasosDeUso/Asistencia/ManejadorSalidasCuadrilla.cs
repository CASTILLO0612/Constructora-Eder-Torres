using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Dominio.Seguridad;

namespace ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;

public sealed partial class ManejadorAsistencia
{
    public async Task<Resultado<IReadOnlyList<RespuestaAsistencia>>> RegistrarSalidasCuadrillaAsync(
        SolicitudRegistrarSalidasCuadrilla solicitud, CancellationToken cancelacion = default)
    {
        var permiso = AutorizadorOperacion.ExigirPermisoYObra(contextoUsuario, CodigosPermiso.RegistrarAsistencia, solicitud.IdObra);
        if (!permiso.EsExitoso) return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(permiso.Error!);
        if (solicitud.IdsAsistencias is null || solicitud.IdsAsistencias.Count == 0
            || solicitud.IdsAsistencias.Any(id => id <= 0)
            || solicitud.IdsAsistencias.Distinct().Count() != solicitud.IdsAsistencias.Count)
            return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(ErrorNegocio.Validacion(
                "salidas_vacias_o_duplicadas", "Seleccione registros distintos de asistencia."));
        var respuestas = new List<RespuestaAsistencia>();
        // Un orden estable de bloqueos evita interbloqueos entre listas concurrentes.
        foreach (var id in solicitud.IdsAsistencias.Order())
        {
            await puertoAsistencia.BloquearAsistenciaAsync(id, cancelacion);
            var asistencia = await puertoAsistencia.ObtenerPorIdAsync(id, cancelacion);
            if (asistencia is null || asistencia.IdObra != solicitud.IdObra)
                return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(ErrorNegocio.Conflicto(
                    "asistencia_otra_obra", "Todas las asistencias deben pertenecer a la obra seleccionada."));
            var respuesta = await RegistrarSalidaAsync(id, new SolicitudRegistrarSalidaAsistencia(solicitud.SalidaEn,
                solicitud.LatitudLectura, solicitud.LongitudLectura, asistencia.UuidRegistro,
                solicitud.EsSincronizacionDiferida, solicitud.PrecisionGpsMetros), cancelacion);
            if (!respuesta.EsExitoso) return Resultado<IReadOnlyList<RespuestaAsistencia>>.Fallo(respuesta.Error!);
            respuestas.Add(respuesta.Valor!);
        }
        return Resultado<IReadOnlyList<RespuestaAsistencia>>.Exito(respuestas);
    }
}
