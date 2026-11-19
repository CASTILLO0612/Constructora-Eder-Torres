using System.Security.Cryptography;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Asistencia;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.EPP;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Herramientas;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Obras;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Personal;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.CasosDeUso.Seguridad;
using ConstructoraEderTorres.Aplicacion.Comun;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructoraEderTorres.Simulacion;

public sealed class ConjuntoDemostracion(IServiceProvider servicios)
{
    public const string PrimerCodigoObra = "SIM-CET-01";
    private const string Nota = "SIMULACIÓN: registro ficticio para pruebas de interfaz. No corresponde a una operación real de la empresa.";
    private readonly List<AccesoDemostracion> _accesos = [];
    private readonly Dictionary<string, int> _cantidades = [];
    private readonly TimeZoneInfo _zonaJornada = TimeZoneInfo.FindSystemTimeZoneById(
        servicios.GetRequiredService<OpcionesCapturaAsistencia>().ZonaHorariaJornada);
    private ContextoConstructora BaseDatos => servicios.GetRequiredService<ContextoConstructora>();
    private DateOnly Hoy => servicios.GetRequiredService<ICalendarioLaboral>().Hoy;
    private T Servicio<T>() where T : notnull => servicios.GetRequiredService<T>();
    private static async Task<T> Exigir<T>(Task<Resultado<T>> tarea)
    {
        var resultado = await tarea;
        return resultado.EsExitoso ? resultado.Valor! : throw new InvalidOperationException($"Simulación rechazada: {resultado.Error!.Codigo}: {resultado.Error.Mensaje}");
    }
    private DateTime Instante(DateOnly dia, int hora, int minuto = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(new TimeOnly(hora, minuto)), _zonaJornada);
    public async Task<ResultadoDemostracion> CargarAsync()
    {
        var personal = Servicio<ManejadorPersonal>();
        var seguridad = Servicio<ManejadorSeguridad>();
        var obras = Servicio<ManejadorObras>();
        var seguimiento = Servicio<ManejadorSeguimientoObra>();
        var epp = Servicio<ManejadorEpp>();
        var herramientas = Servicio<ManejadorHerramientas>();
        var asistencia = Servicio<ManejadorAsistencia>();
        var roles = await BaseDatos.Roles.Where(r => r.Activo).ToDictionaryAsync(r => r.Nombre, r => r.IdRol);
        var cargos = new List<int>();
        foreach (var nombre in new[] { "Ingeniero Residente", "Oficial de albañilería", "Ayudante de construcción", "Electricista", "Fontanero", "Maestro de obras" })
        {
            var existente = await BaseDatos.Cargos.FirstOrDefaultAsync(c => c.Nombre == nombre);
            cargos.Add(existente?.IdCargo ?? (await Exigir(personal.CrearCargoAsync(new(nombre, Nota)))).IdCargo);
        }
        var especialidades = new List<int>();
        foreach (var nombre in new[] { "Estructuras de concreto", "Albañilería", "Electricidad", "Fontanería", "Acabados" })
        {
            var existente = await BaseDatos.Especialidades.FirstOrDefaultAsync(c => c.Nombre == nombre);
            especialidades.Add(existente?.IdEspecialidad ?? (await Exigir(personal.CrearEspecialidadAsync(new(nombre, Nota)))).IdEspecialidad);
        }
        var escenarios = new[] {
            ("Urbanización Los Olivos · Simulación", "Managua · ubicación ficticia de demostración", 12.1079m,-86.2558m),
            ("Clínica Las Flores · Simulación", "Granada · ubicación ficticia de demostración",11.9297m,-85.9560m),
            ("Bodega Industrial Norte · Simulación", "Tipitapa · ubicación ficticia de demostración",12.1947m,-86.0960m),
            ("Centro Comunitario · Simulación", "San Rafael del Sur · ubicación ficticia de demostración",11.8483m,-86.4384m),
            ("Ampliación Comercial · Simulación", "Masaya · ubicación ficticia de demostración",11.9744m,-86.0942m),
        };
        var proyectos = new List<RespuestaObra>();
        for (var i = 0; i < escenarios.Length; i++)
        {
            var (nombre, direccion, latitud, longitud) = escenarios[i];
            proyectos.Add(await Exigir(obras.CrearObraAsync(new($"SIM-CET-{i + 1:00}", nombre, Nota, direccion,
                i == 4 ? Hoy.AddDays(10) : Hoy.AddDays(-50), Hoy.AddDays(70 + i * 15), i == 4 ? "PLANIFICADA" : "ACTIVA", latitud, longitud, 180m))));
        }
        var nombres = new[] { ("Carlos", "Molina"), ("Lucía", "Vargas"), ("José", "Castillo"), ("María", "López"), ("Andrés", "Gutiérrez"), ("Rosa", "Mendoza"), ("Manuel", "Torres"), ("Elena", "Rivas"), ("Daniel", "Pérez"), ("Ana", "Salazar"), ("Luis", "Romero"), ("Paola", "Duarte"), ("Sergio", "Morales"), ("Marta", "Reyes"), ("Javier", "Castro"), ("Laura", "Mejía"), ("Pedro", "Navarro"), ("Diana", "Soto") };
        var trabajadores = new List<RespuestaTrabajador>();
        var asignaciones = new List<RespuestaAsignacionTrabajadorObra>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var (nombre, apellido) = nombres[i];
            var residente = i < 16 && i % 4 == 0;
            var cargo = cargos[residente ? 0 : 1 + i % 5];
            var trabajador = await Exigir(personal.CrearTrabajadorAsync(new(cargo, $"SIM-CET-{i + 1:000}", nombre, null, apellido, null, null, $"persona{i + 1}@example.invalid", Nota, Hoy.AddMonths(-5), [new(especialidades[i % especialidades.Count])])));
            trabajadores.Add(trabajador);
            if (i < 16)
            {
                var idObra = proyectos[i / 4].IdObra;
                asignaciones.Add(await Exigir(personal.AsignarTrabajadorAObraAsync(new(trabajador.IdTrabajador, idObra, especialidades[i % especialidades.Count], i % 2 == 0 ? "FIJO" : "TEMPORAL", Hoy.AddDays(-45), EsResponsable: residente))));
            }
            await CrearAccesoAsync(seguridad, roles, residente ? NombresRol.IngenieroResidente : NombresRol.Trabajador, $"sim.persona{i + 1:00}", trabajador.IdTrabajador);
        }
        foreach (var (usuario, rol) in new[] { ("sim.gerencia", NombresRol.GerenteGeneral), ("sim.operaciones", NombresRol.GerenteOperaciones), ("sim.higiene", NombresRol.HigieneSeguridad), ("sim.administrativo", NombresRol.Administrativo) })
            await CrearAccesoAsync(seguridad, roles, rol, usuario, null);
        for (var i = 0; i < 4; i++)
        {
            var cuadrilla = await Exigir(personal.CrearCuadrillaAsync(new($"SIM-CUA-{i + 1:00}", $"Cuadrilla {i + 1} · Simulación", Nota)));
            var vinculo = await Exigir(personal.AsignarCuadrillaAObraAsync(new(cuadrilla.IdCuadrilla, proyectos[i].IdObra, Hoy.AddDays(-40))));
            foreach (var asignacion in asignaciones.Where(a => a.IdObra == proyectos[i].IdObra))
                await Exigir(personal.AsignarTrabajadorACuadrillaAsync(new(asignacion.IdAsignacion, vinculo.IdCuadrillaObra, Hoy.AddDays(-40))));
        }
        var actividades = new List<RespuestaActividad>();
        for (var i = 0; i < proyectos.Count; i++)
        {
            var proyecto = proyectos[i];
            for (var n = 0; n < 4; n++)
            {
                var nombresActividad = new[] { "Cimentación y zapatas", "Levantado de mampostería", "Instalación eléctrica", "Acabados y repello" };
                var meta = new[] { 120m, 240m, 80m, 320m }[n];
                var participantes = asignaciones.Where(a => a.IdObra == proyecto.IdObra).Select(a => new SolicitudParticipanteActividad(a.IdAsignacion)).ToArray();
                var actividad = await Exigir(seguimiento.CrearActividadAsync(new(proyecto.IdObra, asignaciones.FirstOrDefault(a => a.IdObra == proyecto.IdObra)?.IdAsignacion, $"SIM-ACT-{i + 1}-{n + 1}", nombresActividad[n], Nota, n == 0 ? "m3" : "m2", meta, i == 4 ? Hoy.AddDays(10) : Hoy.AddDays(-30), Hoy.AddDays(30 + n * 8), Participantes: participantes)));
                actividades.Add(actividad);
                if (i == 4) continue;
                var porcentaje = new[] { 0.85m, 0.55m, 0.25m, 0m }[(n + i) % 4];
                if (porcentaje == 0) continue;
                for (var d = 0; d < 3; d++) await Exigir(seguimiento.RegistrarAvanceAsync(new(actividad.IdActividad, Hoy.AddDays(-5 + d), decimal.Round(meta * porcentaje / 3, 2), Nota, UuidRegistro: Guid.NewGuid(), EsSincronizacionDiferida: true)));
            }
        }
        var jornadas = 0;
        foreach (var asignacion in asignaciones)
        {
            var obra = proyectos.Single(o => o.IdObra == asignacion.IdObra);
            for (var d = 1; d <= 7; d++)
            {
                var dia = Hoy.AddDays(-d);
                if (dia.DayOfWeek == DayOfWeek.Sunday || (asignacion.IdTrabajador + d) % 5 == 0) continue;
                await Exigir(asistencia.RegistrarAsistenciaAsync(new(asignacion.IdAsignacion, dia, Instante(dia, 7, (asignacion.IdTrabajador % 3) * 5), Instante(dia, 15 + asignacion.IdTrabajador % 2), obra.Latitud, obra.Longitud, obra.Latitud, obra.Longitud, Guid.NewGuid(), true, 5m, 5m)));
                jornadas++;
            }
        }
        var equipos = new List<RespuestaEpp>();
        foreach (var (nombre, indice) in new[] { ("Casco de seguridad", 0), ("Chaleco reflectivo", 1), ("Guantes anticorte", 2), ("Botas de seguridad", 3), ("Lentes de protección", 4) })
        {
            var tipoExistente = await BaseDatos.TiposEpp.FirstOrDefaultAsync(t => t.Nombre == nombre);
            var idTipo = tipoExistente?.IdTipoEpp ?? (await Exigir(epp.CrearTipoEppAsync(new(nombre, Nota, 180, 3)))).IdTipoEpp;
            for (var n = 0; n < 8; n++) equipos.Add(await Exigir(epp.RegistrarEntradaAutorizadaAsync(new(idTipo, $"SIM-EPP-{indice + 1}-{n + 1:00}", null, Hoy.AddDays(-60), Observacion: Nota))));
        }
        var entregas = new List<RespuestaEntregaEpp>();
        for (var i = 0; i < 12; i++) entregas.Add(await Exigir(epp.RegistrarEntregaAsync(new(equipos[i].IdEpp, asignaciones[i % 8].IdAsignacion, Instante(Hoy.AddDays(-5), 8), UuidRegistro: Guid.NewGuid(), EsSincronizacionDiferida: true))));
        for (var i = 0; i < 3; i++) await Exigir(epp.DevolverEppAsync(entregas[i].IdEntregaEpp, new(Instante(Hoy.AddDays(-1), 15), "BUENO", Nota, Guid.NewGuid())));
        var categorias = new List<int>();
        foreach (var nombre in new[] { "Herramienta eléctrica", "Herramienta manual", "Medición" })
        {
            var existente = await BaseDatos.CategoriasHerramienta.FirstOrDefaultAsync(c => c.Nombre == nombre);
            categorias.Add(existente?.IdCategoria ?? (await Exigir(herramientas.CrearCategoriaAsync(new(nombre, Nota)))).IdCategoria);
        }
        var inventario = new List<RespuestaHerramienta>();
        foreach (var (nombre, categoria, indice) in new[] { ("Taladro percutor", 0, 0), ("Amoladora angular", 0, 1), ("Martillo de uña", 1, 2), ("Pala", 1, 3), ("Nivel láser", 2, 4), ("Cinta métrica", 2, 5) })
        {
            var existente = await BaseDatos.TiposHerramienta.FirstOrDefaultAsync(t => t.Nombre == nombre && t.IdCategoria == categorias[categoria]);
            var idTipo = existente?.IdTipoHerramienta ?? (await Exigir(herramientas.CrearTipoAsync(new(categorias[categoria], nombre, Nota)))).IdTipoHerramienta;
            for (var n = 0; n < 3; n++) inventario.Add(await Exigir(herramientas.RegistrarHerramientaAsync(new(idTipo, $"SIM-HER-{indice + 1}-{n + 1}", null, "Demostración", $"Modelo {indice + 1}", Hoy.AddDays(-60)))));
        }
        var prestamos = new List<RespuestaAsignacionHerramienta>();
        for (var i = 0; i < 6; i++) prestamos.Add(await Exigir(herramientas.AsignarHerramientaAsync(new(inventario[i].IdHerramienta, asignaciones[i % 8].IdAsignacion, Instante(Hoy.AddDays(-4), 8), UuidRegistro: Guid.NewGuid(), EsSincronizacionDiferida: true))));
        await Exigir(herramientas.RegistrarDevolucionOIncidenciaAsync(new(prestamos[0].IdAsignacionHerramienta, "DEVOLUCION", Instante(Hoy.AddDays(-1), 15), Nota, "BUENA", UuidRegistro: Guid.NewGuid())));
        var incidente = await Exigir(seguimiento.RegistrarIncidenciaObraAsync(new(proyectos[0].IdObra, "CLIMA", Instante(Hoy.AddDays(-2), 9), Instante(Hoy.AddDays(-2), 11), 2m, "Lluvia intensa · Simulación", Nota, IdsActividadesAfectadas: [actividades[0].IdActividad], UuidRegistro: Guid.NewGuid(), EsSincronizacionDiferida: true)));
        for (var i = 0; i < 2; i++)
        {
            var ids = await BaseDatos.Avances.Where(a => a.Estado == "REGISTRADO" && BaseDatos.Actividades.Any(t => t.IdActividad == a.IdActividad && t.IdObra == proyectos[i].IdObra)).Select(a => a.IdAvance).ToArrayAsync();
            var acta = await Exigir(seguimiento.ElaborarActaSemanalAsync(new(proyectos[i].IdObra, $"SIM-ACTA-{i + 1:00}", Hoy.AddDays(-6), Hoy, Nota, "Mejorar la secuencia de suministro · Simulación", null, true, ids, i == 0 ? [incidente.IdIncidencia] : [], asignaciones.Where(a => a.IdObra == proyectos[i].IdObra).Select(a => new SolicitudParticipanteActa(a.IdAsignacion, "ASISTENTE")).ToArray(), Guid.NewGuid())));
            if (i == 0) await Exigir(seguimiento.ResolverActaSemanalAsync(acta.IdActa, new("APROBADA", Nota)));
        }
        await Exigir(obras.CambiarEstadoObraAsync(proyectos[2].IdObra, new("SUSPENDIDA")));
        await Exigir(obras.CambiarEstadoObraAsync(proyectos[3].IdObra, new("FINALIZADA", Hoy.AddDays(-45), Hoy.AddDays(-1))));
        _cantidades["obras"] = proyectos.Count; _cantidades["trabajadores"] = trabajadores.Count; _cantidades["cuadrillas"] = 4; _cantidades["asignaciones"] = asignaciones.Count; _cantidades["actividades"] = actividades.Count; _cantidades["asistencias"] = jornadas; _cantidades["epp"] = equipos.Count; _cantidades["herramientas"] = inventario.Count; _cantidades["cuentas"] = _accesos.Count;
        return new(_accesos, _cantidades);
    }
    private async Task CrearAccesoAsync(ManejadorSeguridad seguridad, Dictionary<string, int> roles, string rol, string nombre, int? trabajador)
    {
        var clave = "Sim." + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "a9!";
        await Exigir(seguridad.CrearUsuarioAsync(new(nombre, clave, trabajador, [roles[rol]])));
        _accesos.Add(new(nombre, rol, clave));
    }
}
public sealed record AccesoDemostracion(string Usuario, string Rol, string Contrasena);
public sealed record ResultadoDemostracion(IReadOnlyList<AccesoDemostracion> Accesos, IReadOnlyDictionary<string, int> Cantidades);
