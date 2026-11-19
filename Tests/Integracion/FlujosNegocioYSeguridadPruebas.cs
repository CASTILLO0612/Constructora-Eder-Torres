using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ConstructoraEderTorres.Aplicacion.Contratos.Asistencia;
using ConstructoraEderTorres.Aplicacion.Contratos.EPP;
using ConstructoraEderTorres.Aplicacion.Contratos.Herramientas;
using ConstructoraEderTorres.Aplicacion.Contratos.Obras;
using ConstructoraEderTorres.Aplicacion.Contratos.Personal;
using ConstructoraEderTorres.Aplicacion.Contratos.SeguimientoObra;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;
using ConstructoraEderTorres.Aplicacion.Contratos.Sincronizacion;
using ConstructoraEderTorres.Dominio.Reglas;
using ConstructoraEderTorres.Dominio.Seguridad;
using ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ConstructoraEderTorres.Pruebas.Integracion;

[Collection(ColeccionPruebasRendimiento.Nombre)]
public sealed class FlujosNegocioYSeguridadPruebas(ClusterPostgreSqlDesechableFixture cluster)
    : IClassFixture<ClusterPostgreSqlDesechableFixture>
{
    [Fact]
    public async Task FlujoIntegralMultiobraSeguridadAsistenciaSeguimientoEppHerramientasYSincronizacion()
    {
        await using var fabrica = cluster.CrearFabricaWeb();
        await SembrarAdministradorInicialAsync(fabrica.Services);

        using var clienteAnonimo = CrearClienteSinCookies(fabrica);
        using var clienteAdmin = CrearClienteConCookies(fabrica);
        using var clienteGerente = CrearClienteConCookies(fabrica);
        using var clienteDirectorObra1 = CrearClienteConCookies(fabrica);
        using var clienteMaestroObra1 = CrearClienteConCookies(fabrica);
        using var clienteAlmacen = CrearClienteConCookies(fabrica);

        // 1. E02 (CET-19..22): Autenticación, cookies HttpOnly, rechazo de anónimos, roles, permisos y auditoría
        var respSinSesion = await clienteAnonimo.GetAsync("/api/obras");
        Assert.Equal(HttpStatusCode.Unauthorized, respSinSesion.StatusCode);

        var respLoginInvalido = await clienteAnonimo.PostAsJsonAsync(
            "/api/sesion/iniciar",
            new SolicitudIniciarSesion("admin.inicial", "ContrasenaIncorrecta123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, respLoginInvalido.StatusCode);

        var respLoginAdmin = await clienteAdmin.PostAsJsonAsync(
            "/api/sesion/iniciar",
            new SolicitudIniciarSesion("admin.inicial", "AdminSeguro2026!"));
        Assert.Equal(HttpStatusCode.OK, respLoginAdmin.StatusCode);
        Assert.Contains(respLoginAdmin.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));

        var sesionAdmin = await respLoginAdmin.Content.ReadFromJsonAsync<RespuestaSesionActiva>();
        Assert.NotNull(sesionAdmin);
        Assert.Contains(NombresRol.GerenteGeneral, sesionAdmin.Roles);
        Assert.True(sesionAdmin.TieneAlcanceGlobal);

        var rolesResp = await clienteAdmin.GetFromJsonAsync<List<RespuestaRol>>("/api/roles");
        Assert.NotNull(rolesResp);
        Assert.True(rolesResp.Count >= 5);

        var permisosResp = await clienteAdmin.GetFromJsonAsync<List<RespuestaPermiso>>("/api/permisos");
        Assert.NotNull(permisosResp);
        Assert.Equal(CodigosPermiso.CatalogoBase.Count, permisosResp.Count);

        int IdRolDe(string nombre) => rolesResp.Single(r => r.Nombre == nombre).IdRol;

        // 2. E03 (CET-23..25): Obras, cargos, especialidades, trabajadores, cuadrillas y asignaciones
        var cargoMaestro = await PostExitosoAsync<SolicitudCargo, RespuestaCargo>(
            clienteAdmin,
            "/api/personal/cargos",
            new SolicitudCargo("MAESTRO DE OBRA", "Supervisión operativa de frente"));

        var cargoDirector = await PostExitosoAsync<SolicitudCargo, RespuestaCargo>(
            clienteAdmin,
            "/api/personal/cargos",
            new SolicitudCargo("INGENIERO RESIDENTE", "Dirección técnica de obra"));

        var cargoAlbanil = await PostExitosoAsync<SolicitudCargo, RespuestaCargo>(
            clienteAdmin,
            "/api/personal/cargos",
            new SolicitudCargo("ALBANIL", "Oficial de albañilería"));

        var espAlbanileria = await PostExitosoAsync<SolicitudEspecialidad, RespuestaEspecialidad>(
            clienteAdmin,
            "/api/personal/especialidades",
            new SolicitudEspecialidad("ALBANILERIA ESTRUCTURAL", "Mampostería y concreto"));

        var hoy = new DateOnly(2026, 3, 2);

        var trabDirector1 = await PostExitosoAsync<SolicitudCrearTrabajador, RespuestaTrabajador>(
            clienteAdmin,
            "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(
                cargoDirector.IdCargo,
                "001-100190-0001A",
                "Carlos",
                "Alberto",
                "Mendoza",
                "Rivas",
                "8888-1111",
                "carlos.mendoza@constructora.ni",
                "Managua",
                hoy));

        var trabMaestro1 = await PostExitosoAsync<SolicitudCrearTrabajador, RespuestaTrabajador>(
            clienteAdmin,
            "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(
                cargoMaestro.IdCargo,
                "001-150288-0002B",
                "Jorge",
                "Luis",
                "Pineda",
                "Soto",
                "8888-2222",
                "jorge.pineda@constructora.ni",
                "Managua",
                hoy,
                [new SolicitudEspecialidadTrabajador(espAlbanileria.IdEspecialidad, NivelesEspecialidad.Certificado, hoy.AddYears(-1), hoy.AddYears(2))]));

        var trabAlbanil1 = await PostExitosoAsync<SolicitudCrearTrabajador, RespuestaTrabajador>(
            clienteAdmin,
            "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(
                cargoAlbanil.IdCargo,
                "001-200395-0003C",
                "Pedro",
                "Antonio",
                "Lopez",
                "Diaz",
                "8888-3333",
                null,
                "Masaya",
                hoy,
                [new SolicitudEspecialidadTrabajador(espAlbanileria.IdEspecialidad, NivelesEspecialidad.Avanzado)]));

        var trabAlbanil2 = await PostExitosoAsync<SolicitudCrearTrabajador, RespuestaTrabajador>(
            clienteAdmin,
            "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(
                cargoAlbanil.IdCargo,
                "001-250496-0004D",
                "Mateo",
                null,
                "Gomez",
                "Vega",
                "8888-4444",
                null,
                "Granada",
                hoy));

        // FRQ-0006/CU-06 actualizado: el albañil dispone de cuenta individual con permisos propios.
        var respAutoservicioAlbanil = await clienteAdmin.PostAsJsonAsync(
            "/api/usuarios",
            new SolicitudCrearUsuario("pedro.albanil", "ClaveSegura2026!", trabAlbanil1.IdTrabajador, [IdRolDe(NombresRol.Trabajador)]));
        Assert.Equal(HttpStatusCode.Created, respAutoservicioAlbanil.StatusCode);

        // Crear usuarios para los demás roles
        var usuarioGerente = await PostExitosoAsync<SolicitudCrearUsuario, RespuestaUsuario>(
            clienteAdmin,
            "/api/usuarios",
            new SolicitudCrearUsuario("gerente.proyectos", "ClaveSegura2026!", null, [IdRolDe(NombresRol.GerenteOperaciones)]));

        var usuarioDirector1 = await PostExitosoAsync<SolicitudCrearUsuario, RespuestaUsuario>(
            clienteAdmin,
            "/api/usuarios",
            new SolicitudCrearUsuario("director.obra1", "ClaveSegura2026!", trabDirector1.IdTrabajador, [IdRolDe(NombresRol.GerenteOperaciones)]));

        var usuarioMaestro1 = await PostExitosoAsync<SolicitudCrearUsuario, RespuestaUsuario>(
            clienteAdmin,
            "/api/usuarios",
            new SolicitudCrearUsuario("maestro.obra1", "ClaveSegura2026!", trabMaestro1.IdTrabajador, [IdRolDe(NombresRol.IngenieroResidente)]));

        var usuarioAlmacen = await PostExitosoAsync<SolicitudCrearUsuario, RespuestaUsuario>(
            clienteAdmin,
            "/api/usuarios",
            new SolicitudCrearUsuario("encargado.almacen", "ClaveSegura2026!", null, [IdRolDe(NombresRol.HigieneSeguridad)]));

        // Crear Obra 1 y Obra 2
        var obra1 = await PostExitosoAsync<SolicitudCrearObra, RespuestaObra>(
            clienteAdmin,
            "/api/obras",
            new SolicitudCrearObra("OB-NORTE-01", "Residencial Las Colinas", "Etapa 1", "Km 10 Carretera Masaya", hoy, hoy.AddMonths(6), EstadosObra.Activa,
                12m, -86m, 50000m));

        var obra2 = await PostExitosoAsync<SolicitudCrearObra, RespuestaObra>(
            clienteAdmin,
            "/api/obras",
            new SolicitudCrearObra("OB-SUR-02", "Bodega Industrial Sur", "Nave principal", "Km 15 Carretera Sur", hoy, hoy.AddMonths(8), EstadosObra.Activa,
                12m, -86m, 50000m));

        // Asignar personal a Obra 1 y Obra 2
        var asigDirectorObra1 = await PostExitosoAsync<SolicitudAsignarTrabajadorObra, RespuestaAsignacionTrabajadorObra>(
            clienteAdmin,
            "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabDirector1.IdTrabajador, obra1.IdObra, null, TiposContratacion.Fijo, hoy, null, true));

        var asigMaestroObra1 = await PostExitosoAsync<SolicitudAsignarTrabajadorObra, RespuestaAsignacionTrabajadorObra>(
            clienteAdmin,
            "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabMaestro1.IdTrabajador, obra1.IdObra, espAlbanileria.IdEspecialidad, TiposContratacion.Fijo, hoy, null, true));

        var asigAlbanil1Obra1 = await PostExitosoAsync<SolicitudAsignarTrabajadorObra, RespuestaAsignacionTrabajadorObra>(
            clienteAdmin,
            "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabAlbanil1.IdTrabajador, obra1.IdObra, espAlbanileria.IdEspecialidad, TiposContratacion.Temporal, hoy));

        var asigAlbanil2Obra2 = await PostExitosoAsync<SolicitudAsignarTrabajadorObra, RespuestaAsignacionTrabajadorObra>(
            clienteAdmin,
            "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabAlbanil2.IdTrabajador, obra2.IdObra, null, TiposContratacion.Subcontratado, hoy));

        // CET-24: Trabajador inactivo no se permite en nuevas asignaciones
        var trabInactivo = await PostExitosoAsync<SolicitudCrearTrabajador, RespuestaTrabajador>(
            clienteAdmin,
            "/api/personal/trabajadores",
            new SolicitudCrearTrabajador(cargoAlbanil.IdCargo, "001-990199-0099Z", "Mario", null, "Ruiz", null, null, null, null, hoy));

        var respInactivarTrab = await clienteAdmin.PatchAsJsonAsync(
            $"/api/personal/trabajadores/{trabInactivo.IdTrabajador}/estado",
            new SolicitudCambiarEstadoTrabajador(EstadosTrabajador.Inactivo));
        Assert.Equal(HttpStatusCode.OK, respInactivarTrab.StatusCode);

        var respAsignarInactivo = await clienteAdmin.PostAsJsonAsync(
            "/api/personal/asignaciones-obra",
            new SolicitudAsignarTrabajadorObra(trabInactivo.IdTrabajador, obra1.IdObra, null, TiposContratacion.Temporal, hoy));
        Assert.Equal(HttpStatusCode.Conflict, respAsignarInactivo.StatusCode);

        // Iniciar sesión con cada rol
        await IniciarSesionAsync(clienteGerente, "gerente.proyectos", "ClaveSegura2026!");
        await IniciarSesionAsync(clienteDirectorObra1, "director.obra1", "ClaveSegura2026!");
        await IniciarSesionAsync(clienteMaestroObra1, "maestro.obra1", "ClaveSegura2026!");
        await IniciarSesionAsync(clienteAlmacen, "encargado.almacen", "ClaveSegura2026!");

        // CET-49: Verificar aislamiento de alcance por obra para DIRECTOR_OBRA y MAESTRO_OBRA
        var obrasVisiblesMaestro = await clienteMaestroObra1.GetFromJsonAsync<List<RespuestaObra>>("/api/obras");
        Assert.NotNull(obrasVisiblesMaestro);
        Assert.Single(obrasVisiblesMaestro);
        Assert.Equal(obra1.IdObra, obrasVisiblesMaestro[0].IdObra);

        var respObra2DesdeMaestro = await clienteMaestroObra1.GetAsync($"/api/obras/{obra2.IdObra}");
        Assert.Equal(HttpStatusCode.Forbidden, respObra2DesdeMaestro.StatusCode);

        // CET-25: Cuadrillas y protección multiobra en asignaciones a cuadrillas
        var cuadrilla1 = await PostExitosoAsync<SolicitudCuadrilla, RespuestaCuadrilla>(
            clienteDirectorObra1,
            "/api/personal/cuadrillas",
            new SolicitudCuadrilla("CUAD-EST-01", "Cuadrilla Estructura Norte", "Frente A"));

        var cuadrillaObra1 = await PostExitosoAsync<SolicitudAsignarCuadrillaObra, RespuestaCuadrillaObra>(
            clienteDirectorObra1,
            "/api/personal/cuadrillas-obra",
            new SolicitudAsignarCuadrillaObra(cuadrilla1.IdCuadrilla, obra1.IdObra, hoy));

        var cuadrillaObra2 = await PostExitosoAsync<SolicitudAsignarCuadrillaObra, RespuestaCuadrillaObra>(
            clienteAdmin,
            "/api/personal/cuadrillas-obra",
            new SolicitudAsignarCuadrillaObra(cuadrilla1.IdCuadrilla, obra2.IdObra, hoy));

        var miembroCuadrilla1 = await PostExitosoAsync<SolicitudAsignarTrabajadorCuadrilla, RespuestaTrabajadorCuadrilla>(
            clienteDirectorObra1,
            "/api/personal/trabajadores-cuadrilla",
            new SolicitudAsignarTrabajadorCuadrilla(asigAlbanil1Obra1.IdAsignacion, cuadrillaObra1.IdCuadrillaObra, hoy));
        Assert.Equal(obra1.IdObra, miembroCuadrilla1.IdObra);

        // Intento inválido de cruzar trabajador de Obra 1 con cuadrilla de Obra 2 (CRQ-0003)
        var respCruceCuadrilla = await clienteAdmin.PostAsJsonAsync(
            "/api/personal/trabajadores-cuadrilla",
            new SolicitudAsignarTrabajadorCuadrilla(asigAlbanil1Obra1.IdAsignacion, cuadrillaObra2.IdCuadrillaObra, hoy));
        Assert.Equal(HttpStatusCode.Conflict, respCruceCuadrilla.StatusCode);

        // 3. E04 (CET-26..27): Asistencia individual y por cuadrilla, GPS, idempotencia, correcciones y consolidado
        var entradaManana = new DateTime(2026, 3, 2, 13, 0, 0, DateTimeKind.Utc);
        var salidaTarde = new DateTime(2026, 3, 2, 22, 0, 0, DateTimeKind.Utc);
        var uuidAsistencia1 = Guid.NewGuid();

        var asistencia1 = await PostExitosoAsync<SolicitudRegistrarAsistencia, RespuestaAsistencia>(
            clienteMaestroObra1,
            "/api/asistencias",
            new SolicitudRegistrarAsistencia(
                asigAlbanil1Obra1.IdAsignacion,
                hoy,
                entradaManana,
                salidaTarde,
                12.136389m,
                -86.251389m,
                12.136400m,
                -86.251400m,
                uuidAsistencia1, PrecisionGpsEntradaMetros: 5m, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(EstadosAsistencia.Completa, asistencia1.Estado);
        Assert.False(asistencia1.FueIdempotente);

        // Reenvío del mismo UUID debe ser idempotente sin duplicar
        var respAsistenciaIdempotente = await clienteMaestroObra1.PostAsJsonAsync(
            "/api/asistencias",
            new SolicitudRegistrarAsistencia(
                asigAlbanil1Obra1.IdAsignacion,
                hoy,
                entradaManana,
                salidaTarde,
                12.136389m,
                -86.251389m,
                12.136400m,
                -86.251400m,
                uuidAsistencia1, PrecisionGpsEntradaMetros: 5m, PrecisionGpsSalidaMetros: 5m));
        Assert.Equal(HttpStatusCode.OK, respAsistenciaIdempotente.StatusCode);
        var asistenciaIdemp = await respAsistenciaIdempotente.Content.ReadFromJsonAsync<RespuestaAsistencia>();
        Assert.NotNull(asistenciaIdemp);
        Assert.True(asistenciaIdemp.FueIdempotente);
        Assert.Equal(asistencia1.IdAsistencia, asistenciaIdemp.IdAsistencia);

        // Solicitar corrección (MAESTRO_OBRA) y aprobarla (DIRECTOR_OBRA)
        var correccion = await PostExitosoAsync<SolicitudCrearCorreccionAsistencia, RespuestaCorreccionAsistencia>(
            clienteMaestroObra1,
            "/api/asistencias/correcciones",
            new SolicitudCrearCorreccionAsistencia(
                asistencia1.IdAsistencia,
                entradaManana.AddMinutes(-15),
                salidaTarde,
                "El trabajador ingresó 15 minutos antes en portón norte."));
        Assert.Equal(EstadosCorreccionAsistencia.Pendiente, correccion.Estado);

        // Maestro de obra NO tiene permiso para aprobar correcciones (CET-49)
        var respAprobarPorMaestro = await clienteMaestroObra1.PatchAsJsonAsync(
            $"/api/asistencias/correcciones/{correccion.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        Assert.Equal(HttpStatusCode.Forbidden, respAprobarPorMaestro.StatusCode);

        var respAprobarPorDirector = await clienteDirectorObra1.PatchAsJsonAsync(
            $"/api/asistencias/correcciones/{correccion.IdCorreccion}/resolucion",
            new SolicitudResolverCorreccionAsistencia(EstadosCorreccionAsistencia.Aprobada));
        Assert.Equal(HttpStatusCode.OK, respAprobarPorDirector.StatusCode);
        var correccionAprobada = await respAprobarPorDirector.Content.ReadFromJsonAsync<RespuestaCorreccionAsistencia>();
        Assert.NotNull(correccionAprobada);
        Assert.Equal(EstadosCorreccionAsistencia.Aprobada, correccionAprobada.Estado);

        var consolidado = await clienteDirectorObra1.GetFromJsonAsync<List<ItemConsolidadoAsistencia>>(
            $"/api/asistencias/consolidado?idObra={obra1.IdObra}");
        Assert.NotNull(consolidado);
        Assert.Single(consolidado);
        Assert.Equal(1, consolidado[0].JornadasCorregidas);
        Assert.Equal(9.25m, consolidado[0].HorasTrabajadasTotales);

        // 4. E05 (CET-28..33): Seguimiento de obra (actividades, avances, evidencias, retrasos con justificación, incidencias y actas)
        var actividadObra1 = await PostExitosoAsync<SolicitudCrearActividad, RespuestaActividad>(
            clienteDirectorObra1,
            "/api/seguimiento/actividades",
            new SolicitudCrearActividad(
                obra1.IdObra,
                asigMaestroObra1.IdAsignacion,
                "ACT-CIM-01",
                "Excavación y fundición de zapatas",
                "Zapatas aisladas eje A-D",
                "m3",
                120m,
                hoy,
                hoy.AddDays(15),
                EstadosActividad.Planificada,
                [new SolicitudParticipanteActividad(asigAlbanil1Obra1.IdAsignacion, "OFICIAL")]));

        var actividadObra2 = await PostExitosoAsync<SolicitudCrearActividad, RespuestaActividad>(
            clienteAdmin,
            "/api/seguimiento/actividades",
            new SolicitudCrearActividad(
                obra2.IdObra,
                asigAlbanil2Obra2.IdAsignacion,
                "ACT-SUR-01",
                "Nivelación de terreno",
                null,
                "m2",
                500m,
                hoy,
                hoy.AddDays(10)));

        // Avance normal + evidencia con archivo binario y huella SHA-256
        var avance1 = await PostExitosoAsync<SolicitudRegistrarAvance, RespuestaAvance>(
            clienteMaestroObra1,
            "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(
                actividadObra1.IdActividad,
                hoy,
                25.5m,
                "Excavación de 6 zapatas del eje A"));

        var bytesFoto = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        var evidencia1 = await PostExitosoAsync<SolicitudRegistrarEvidencia, RespuestaEvidencia>(
            clienteMaestroObra1,
            "/api/seguimiento/evidencias",
            new SolicitudRegistrarEvidencia(
                avance1.IdAvance,
                TiposEvidencia.Fotografia,
                NombreArchivo: "zapatas_eje_a.png",
                ContenidoArchivo: bytesFoto,
                CapturadaEn: DateTime.UtcNow,
                Latitud: 12.136389m,
                Longitud: -86.251389m));
        Assert.Equal(64, evidencia1.HuellaArchivo.Length);

        // CET-32 (CRQ-0001): Avance incompleto/retrasado sin justificación es rechazado
        var respAvanceSinJustificacion = await clienteMaestroObra1.PostAsJsonAsync(
            "/api/seguimiento/avances",
            new SolicitudRegistrarAvance(
                actividadObra1.IdActividad,
                hoy.AddDays(1),
                5m,
                Observaciones: "   ",
                EsIncompletoORetrasado: true));
        Assert.Equal(HttpStatusCode.BadRequest, respAvanceSinJustificacion.StatusCode);

        // CET-30: Registrar incidencia de obra y vincularla al justificar retraso
        var incidenciaObra1 = await PostExitosoAsync<SolicitudRegistrarIncidenciaObra, RespuestaIncidenciaObra>(
            clienteMaestroObra1,
            "/api/seguimiento/incidencias",
            new SolicitudRegistrarIncidenciaObra(
                obra1.IdObra,
                TiposIncidenciaObra.Clima,
                entradaManana.AddDays(1),
                entradaManana.AddDays(1).AddHours(3),
                3m,
                "Lluvia intensa detuvo excavación a cielo abierto",
                "Mitigado con bombeo de achique",
                EstadosIncidencia.Abierta,
                [actividadObra1.IdActividad]));

        var avanceJustificado = await PostExitosoAsync<SolicitudJustificarActividad, RespuestaAvance>(
            clienteMaestroObra1,
            "/api/seguimiento/avances/justificar-retraso",
            new SolicitudJustificarActividad(
                actividadObra1.IdActividad,
                hoy.AddDays(1),
                8m,
                "Retraso por lluvia intensa y saturación de zanja.",
                incidenciaObra1.IdIncidencia));
        Assert.Equal(8m, avanceJustificado.CantidadEjecutada);

        // CET-31 y CET-33: Elaboración de acta semanal y aprobación por Gerente de Proyectos
        var acta1 = await PostExitosoAsync<SolicitudElaborarActa, RespuestaActa>(
            clienteDirectorObra1,
            "/api/seguimiento/actas",
            new SolicitudElaborarActa(
                obra1.IdObra,
                "ACTA-SEM-001",
                hoy,
                hoy.AddDays(6),
                "Semana inicial de cimentaciones",
                "Incrementar bombeo preventivo",
                null,
                EnviarARevision: true,
                IdsAvances: [avance1.IdAvance, avanceJustificado.IdAvance],
                IdsIncidencias: [incidenciaObra1.IdIncidencia],
                Participantes:
                [
                    new SolicitudParticipanteActa(asigDirectorObra1.IdAsignacion, "ELABORA"),
                    new SolicitudParticipanteActa(asigMaestroObra1.IdAsignacion, "SUPERVISA")
                ]));
        Assert.Equal(EstadosActa.Pendiente, acta1.Estado);
        Assert.Equal(2, acta1.AvancesConsolidados.Count);
        Assert.Single(acta1.EvidenciasConsolidadas);
        Assert.Single(acta1.IncidenciasConsolidadas);

        // Ingeniero residente NO puede aprobar actas (solo GERENTE_OPERACIONES / GERENTE_GENERAL)
        var respAprobarActaPorResidente = await clienteMaestroObra1.PatchAsJsonAsync(
            $"/api/seguimiento/actas/{acta1.IdActa}/resolucion",
            new SolicitudResolverActa(DecisionesActa.Aprobada, "Intento no autorizado"));
        Assert.Equal(HttpStatusCode.Forbidden, respAprobarActaPorResidente.StatusCode);

        var respAprobarActaGerente = await clienteGerente.PatchAsJsonAsync(
            $"/api/seguimiento/actas/{acta1.IdActa}/resolucion",
            new SolicitudResolverActa(DecisionesActa.Aprobada, "Acta semanal verificada y aprobada por gerencia."));
        Assert.Equal(HttpStatusCode.OK, respAprobarActaGerente.StatusCode);
        var actaAprobada = await respAprobarActaGerente.Content.ReadFromJsonAsync<RespuestaActa>();
        Assert.NotNull(actaAprobada);
        Assert.Equal(EstadosActa.Aprobada, actaAprobada.Estado);
        Assert.Single(actaAprobada.Aprobaciones);

        // 5. E06 (CET-34..38): EPP (catálogo, entradas, stock mínimo, entregas, reposición con comprobación CRQ-0002, historial y reportes)
        var tipoCasco = await PostExitosoAsync<SolicitudCrearTipoEpp, RespuestaTipoEpp>(
            clienteAlmacen,
            "/api/epp/tipos",
            new SolicitudCrearTipoEpp("CASCO DIELECTRICO CLASE E", "Casco con barbiquejo", 365, ExistenciaMinima: 2));

        var casco1 = await PostExitosoAsync<SolicitudRegistrarEntradaEpp, RespuestaEpp>(
            clienteAlmacen,
            "/api/epp/entradas",
            new SolicitudRegistrarEntradaEpp(tipoCasco.IdTipoEpp, "EPP-CAS-001", "SER-CAS-001", hoy, CondicionesEpp.Nuevo));

        var casco2 = await PostExitosoAsync<SolicitudRegistrarEntradaEpp, RespuestaEpp>(
            clienteAlmacen,
            "/api/epp/entradas",
            new SolicitudRegistrarEntradaEpp(tipoCasco.IdTipoEpp, "EPP-CAS-002", "SER-CAS-002", hoy, CondicionesEpp.Nuevo));

        var entregaCasco1 = await PostExitosoAsync<SolicitudRegistrarEntregaEpp, RespuestaEntregaEpp>(
            clienteAlmacen,
            "/api/epp/entregas",
            new SolicitudRegistrarEntregaEpp(casco1.IdEpp, asigAlbanil1Obra1.IdAsignacion, entradaManana));
        Assert.Equal(EstadosEntregaEpp.Activa, entregaCasco1.Estado);

        var resumenStock = await clienteAlmacen.GetFromJsonAsync<List<ResumenExistenciaTipoEpp>>("/api/epp/existencias/resumen");
        Assert.NotNull(resumenStock);
        var resumenCasco = Assert.Single(resumenStock, r => r.IdTipoEpp == tipoCasco.IdTipoEpp);
        Assert.Equal(1, resumenCasco.TotalDisponibles);
        Assert.True(resumenCasco.BajoExistenciaMinima);

        // CET-36 (CRQ-0002): Reposición por DANO sin devolver el equipo dañado (equipoDevuelto = false) debe rechazarse
        var respReposicionSinDevolucion = await clienteAlmacen.PostAsJsonAsync(
            "/api/epp/reposiciones",
            new SolicitudRegistrarReposicionEpp(
                entregaCasco1.IdEntregaEpp,
                casco2.IdEpp,
                MotivosReposicionEpp.Dano,
                "Fisura por caída de material",
                EquipoDevuelto: false));
        Assert.Equal(HttpStatusCode.BadRequest, respReposicionSinDevolucion.StatusCode);

        // Reposición válida con comprobación de devolución y entrega de sustituto
        var reposicionValida = await PostExitosoAsync<SolicitudRegistrarReposicionEpp, RespuestaReposicionEpp>(
            clienteAlmacen,
            "/api/epp/reposiciones",
            new SolicitudRegistrarReposicionEpp(
                entregaCasco1.IdEntregaEpp,
                casco2.IdEpp,
                MotivosReposicionEpp.Dano,
                "Fisura por caída de material; equipo físico recibido en bodega",
                EquipoDevuelto: true));
        Assert.Equal(EstadosReposicionEpp.Ejecutada, reposicionValida.Estado);
        Assert.NotNull(reposicionValida.IdEntregaNueva);

        var historialEpp = await clienteDirectorObra1.GetFromJsonAsync<List<EventoHistorialEppTrabajador>>(
            $"/api/epp/trabajadores/{trabAlbanil1.IdTrabajador}/historial");
        Assert.NotNull(historialEpp);
        Assert.True(historialEpp.Count >= 3);

        var reporteEpp = await clienteGerente.GetFromJsonAsync<List<ItemReporteEpp>>(
            $"/api/epp/reportes?idObra={obra1.IdObra}");
        Assert.NotNull(reporteEpp);
        var itemReporte = Assert.Single(reporteEpp);
        Assert.Equal(2, itemReporte.CantidadEntregada);
        Assert.Equal(1, itemReporte.CantidadActiva);
        Assert.Equal(1, itemReporte.CantidadReemplazada);

        // 6. E07 (CET-39..41): Herramientas (catálogo, inventario, asignación activa única y devolución/incidencia)
        var catElectrica = await PostExitosoAsync<SolicitudCrearCategoriaHerramienta, RespuestaCategoriaHerramienta>(
            clienteAlmacen,
            "/api/herramientas/categorias",
            new SolicitudCrearCategoriaHerramienta("HERRAMIENTAS ELECTRICAS", "Equipos rotativos y de corte"));

        var tipoTaladro = await PostExitosoAsync<SolicitudCrearTipoHerramienta, RespuestaTipoHerramienta>(
            clienteAlmacen,
            "/api/herramientas/tipos",
            new SolicitudCrearTipoHerramienta(catElectrica.IdCategoria, "ROTOMARTILLO SDS PLUS", "800W"));

        var taladro1 = await PostExitosoAsync<SolicitudRegistrarHerramienta, RespuestaHerramienta>(
            clienteAlmacen,
            "/api/herramientas",
            new SolicitudRegistrarHerramienta(tipoTaladro.IdTipoHerramienta, "HER-ROT-001", "SER-ROT-001", "Bosch", "GBH 2-26", hoy));

        var asigTaladro1 = await PostExitosoAsync<SolicitudAsignarHerramienta, RespuestaAsignacionHerramienta>(
            clienteAlmacen,
            "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(taladro1.IdHerramienta, asigAlbanil1Obra1.IdAsignacion, entradaManana));
        Assert.Equal(EstadosAsignacionHerramienta.Activa, asigTaladro1.Estado);

        // Doble asignación simultánea de la misma herramienta es rechazada (CET-40)
        var respDobleAsignacion = await clienteAlmacen.PostAsJsonAsync(
            "/api/herramientas/asignaciones",
            new SolicitudAsignarHerramienta(taladro1.IdHerramienta, asigMaestroObra1.IdAsignacion, entradaManana.AddHours(1)));
        Assert.Equal(HttpStatusCode.Conflict, respDobleAsignacion.StatusCode);

        // Devolución de herramienta liberándola a DISPONIBLE (CET-41)
        var eventoDevolucion = await PostExitosoAsync<SolicitudRegistrarEventoHerramienta, RespuestaIncidenciaHerramienta>(
            clienteAlmacen,
            "/api/herramientas/eventos",
            new SolicitudRegistrarEventoHerramienta(
                asigTaladro1.IdAsignacionHerramienta,
                TiposIncidenciaHerramienta.Devolucion,
                salidaTarde,
                "Devolución al cierre de jornada en buen estado",
                CondicionesHerramienta.Buena));
        Assert.Equal(EstadosAsignacionHerramienta.Devuelta, eventoDevolucion.EstadoAsignacionResultante);
        Assert.Equal(EstadosHerramienta.Disponible, eventoDevolucion.EstadoHerramientaResultante);

        // 7. E08 (CET-44..47): Sincronización por lotes offline, idempotencia por UUID y manejo parcial de conflictos
        var uuidOfflineAsistencia = Guid.NewGuid();
        var uuidOfflineAvance = Guid.NewGuid();
        var uuidOfflineIncidencia = Guid.NewGuid();
        var uuidOfflineConflictoObra2 = Guid.NewGuid();

        var loteOffline = new SolicitudSincronizacionLote(
            Asistencias:
            [
                new SolicitudRegistrarAsistencia(
                    asigAlbanil1Obra1.IdAsignacion,
                    hoy.AddDays(2),
                    entradaManana.AddDays(2),
                    salidaTarde.AddDays(2),
                    12.136389m,
                    -86.251389m,
                    12.136389m,
                    -86.251389m,
                    uuidOfflineAsistencia, PrecisionGpsEntradaMetros: 5m, PrecisionGpsSalidaMetros: 5m)
            ],
            Avances:
            [
                new SolicitudRegistrarAvance(
                    actividadObra1.IdActividad,
                    hoy.AddDays(2),
                    18m,
                    "Avance capturado sin conexión",
                    UuidRegistro: uuidOfflineAvance),
                // Operación sobre Obra 2 enviada por Maestro asignado solo a Obra 1 -> debe rechazarse individualmente sin abortar el lote
                new SolicitudRegistrarAvance(
                    actividadObra2.IdActividad,
                    hoy.AddDays(2),
                    10m,
                    "Intento fuera de alcance en lote",
                    UuidRegistro: uuidOfflineConflictoObra2)
            ],
            IncidenciasObra:
            [
                new SolicitudRegistrarIncidenciaObra(
                    obra1.IdObra,
                    TiposIncidenciaObra.Tecnica,
                    entradaManana.AddDays(2),
                    null,
                    1.5m,
                    "Ajuste de niveles topográficos en campo",
                    null,
                    UuidRegistro: uuidOfflineIncidencia)
            ]);

        var respLote1 = await PostExitosoAsync<SolicitudSincronizacionLote, RespuestaSincronizacionLote>(
            clienteMaestroObra1,
            "/api/sincronizacion/lote",
            loteOffline);
        Assert.Equal(4, respLote1.TotalRecibidas);
        Assert.Equal(3, respLote1.TotalConfirmadas);
        Assert.Equal(0, respLote1.TotalIdempotentes);
        Assert.Equal(1, respLote1.TotalRechazadas);
        Assert.Contains(
            respLote1.Acuses,
            a => a.UuidRegistro == uuidOfflineConflictoObra2
                 && a.EstadoAcuse == EstadosAcuseSincronizacion.RechazadoAutorizacion);

        // Reenvío del mismo lote (reintento de red tras microcorte): las 3 operaciones válidas responden IDEMPOTENTE_PREVIO
        var respLote2 = await PostExitosoAsync<SolicitudSincronizacionLote, RespuestaSincronizacionLote>(
            clienteMaestroObra1,
            "/api/sincronizacion/lote",
            loteOffline);
        Assert.Equal(4, respLote2.TotalRecibidas);
        Assert.Equal(0, respLote2.TotalConfirmadas);
        Assert.Equal(3, respLote2.TotalIdempotentes);
        Assert.Equal(1, respLote2.TotalRechazadas);

        // 8. CET-22: Verificar trazabilidad en tabla de auditoría y revocación inmediata al inactivar usuario (CET-21)
        var auditorias = await clienteAdmin.GetFromJsonAsync<List<RespuestaAuditoria>>("/api/auditorias?limite=50");
        Assert.NotNull(auditorias);
        Assert.NotEmpty(auditorias);

        var respInactivarMaestro = await clienteAdmin.PatchAsJsonAsync(
            $"/api/usuarios/{usuarioMaestro1.IdUsuario}/estado",
            new SolicitudCambiarEstadoUsuario(EstadosUsuario.Inactivo));
        Assert.Equal(HttpStatusCode.OK, respInactivarMaestro.StatusCode);

        // La cookie previamente emitida para maestro.obra1 es rechazada inmediatamente en la siguiente petición
        var respMaestroTrasInactivar = await clienteMaestroObra1.GetAsync("/api/sesion/actual");
        Assert.Equal(HttpStatusCode.Unauthorized, respMaestroTrasInactivar.StatusCode);
    }

    [Fact]
    public async Task DisparadoresMultiobraEnPostgreSqlRechazanAsociacionesCruzadasDirectasEIndirectas()
    {
        // CET-48 (CB-901): Verifica directamente contra PostgreSQL los disparadores de integridad multiobra,
        // incluyendo cambios indirectos (ej. cambiar avances.id_actividad hacia otra obra cuando ya está en actas_avance).
        await using var conexion = new NpgsqlConnection(cluster.CadenaConexion);
        await conexion.OpenAsync();

        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO public.cargos (nombre, activo) VALUES ('CARGO_{sufijo}', TRUE) RETURNING id_cargo;
            """;
        var idCargo = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.trabajadores (id_cargo, cedula, primer_nombre, primer_apellido, fecha_ingreso, estado)
            VALUES ({idCargo}, 'CED-{sufijo}-1', 'Juan', 'Perez', DATE '2026-03-01', 'ACTIVO')
            RETURNING id_trabajador;
            """;
        var idTrabajador = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.usuarios (id_trabajador, nombre_usuario, password_hash, estado)
            VALUES ({idTrabajador}, 'usr_{sufijo}', 'HASH', 'ACTIVO')
            RETURNING id_usuario;
            """;
        var idUsuario = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.obras (codigo, nombre, direccion, fecha_inicio_plan, fecha_fin_plan, estado)
            VALUES ('OB-A-{sufijo}', 'Obra A', 'Dir A', DATE '2026-03-01', DATE '2026-06-01', 'ACTIVA')
            RETURNING id_obra;
            """;
        var idObraA = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.obras (codigo, nombre, direccion, fecha_inicio_plan, fecha_fin_plan, estado)
            VALUES ('OB-B-{sufijo}', 'Obra B', 'Dir B', DATE '2026-03-01', DATE '2026-06-01', 'ACTIVA')
            RETURNING id_obra;
            """;
        var idObraB = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.asignaciones_trabajador_obra (id_trabajador, id_obra, tipo_contratacion, fecha_inicio, estado)
            VALUES ({idTrabajador}, {idObraB}, 'FIJO', DATE '2026-03-01', 'ACTIVA')
            RETURNING id_asignacion;
            """;
        var idAsignacionObraB = (int)(await cmd.ExecuteScalarAsync())!;

        // 1. Disparador tr_actividades_validar_responsable_obra: rechaza responsable de Obra B en actividad de Obra A
        cmd.CommandText = $"""
            INSERT INTO public.actividades (id_obra, id_asignacion_responsable, codigo, nombre, unidad_medida, cantidad_planificada, fecha_inicio_plan, fecha_fin_plan, estado)
            VALUES ({idObraA}, {idAsignacionObraB}, 'ACT-INV', 'Actividad Invalida', 'm2', 10, DATE '2026-03-01', DATE '2026-03-10', 'PLANIFICADA');
            """;
        var exResp = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", exResp.SqlState);

        // 2. Crear actividades válidas en Obra A y Obra B, avance en Obra A y acta en Obra A
        cmd.CommandText = $"""
            INSERT INTO public.actividades (id_obra, codigo, nombre, unidad_medida, cantidad_planificada, fecha_inicio_plan, fecha_fin_plan, estado)
            VALUES ({idObraA}, 'ACT-A', 'Actividad A', 'm2', 10, DATE '2026-03-01', DATE '2026-03-10', 'EN_PROCESO')
            RETURNING id_actividad;
            """;
        var idActividadA = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.actividades (id_obra, codigo, nombre, unidad_medida, cantidad_planificada, fecha_inicio_plan, fecha_fin_plan, estado)
            VALUES ({idObraB}, 'ACT-B', 'Actividad B', 'm2', 10, DATE '2026-03-01', DATE '2026-03-10', 'EN_PROCESO')
            RETURNING id_actividad;
            """;
        var idActividadB = (int)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.avances (id_actividad, fecha_avance, cantidad_ejecutada, estado, id_usuario_registro)
            VALUES ({idActividadA}, DATE '2026-03-02', 5, 'REGISTRADO', {idUsuario})
            RETURNING id_avance;
            """;
        var idAvanceA = (long)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.avances (id_actividad, fecha_avance, cantidad_ejecutada, estado, id_usuario_registro)
            VALUES ({idActividadB}, DATE '2026-03-02', 5, 'REGISTRADO', {idUsuario})
            RETURNING id_avance;
            """;
        var idAvanceB = (long)(await cmd.ExecuteScalarAsync())!;

        cmd.CommandText = $"""
            INSERT INTO public.actas (id_obra, numero_acta, periodo_inicio, periodo_fin, estado, id_usuario_creador)
            VALUES ({idObraA}, 'ACTA-01', DATE '2026-03-01', DATE '2026-03-07', 'BORRADOR', {idUsuario})
            RETURNING id_acta;
            """;
        var idActaA = (int)(await cmd.ExecuteScalarAsync())!;

        // 3. Disparador trg_validar_acta_avance_obra: rechaza asociar un avance de Obra B en un acta de Obra A
        cmd.CommandText = $"""
            INSERT INTO public.actas_avance (id_acta, id_avance)
            VALUES ({idActaA}, {idAvanceB});
            """;
        var exActaAvanceCruzado = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", exActaAvanceCruzado.SqlState);

        // Asociación válida dentro de la misma Obra A
        cmd.CommandText = $"""
            INSERT INTO public.actas_avance (id_acta, id_avance)
            VALUES ({idActaA}, {idAvanceA});
            """;
        await cmd.ExecuteNonQueryAsync();

        // 4. Disparadores indirectos trg_impedir_cambio_obra_*: rechazan cambiar id_obra de actividad, acta o asignación ya creadas
        cmd.CommandText = $"""
            UPDATE public.actividades SET id_obra = {idObraB} WHERE id_actividad = {idActividadA};
            """;
        var exCambioObraActividad = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", exCambioObraActividad.SqlState);

        cmd.CommandText = $"""
            UPDATE public.actas SET id_obra = {idObraB} WHERE id_acta = {idActaA};
            """;
        var exCambioObraActa = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", exCambioObraActa.SqlState);

        cmd.CommandText = $"""
            UPDATE public.asignaciones_trabajador_obra SET id_obra = {idObraA} WHERE id_asignacion = {idAsignacionObraB};
            """;
        var exCambioObraAsignacion = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", exCambioObraAsignacion.SqlState);
    }

    [Fact]
    public async Task RendimientoBajoCarga25UsuariosConcurrentesCumpleUmbralesNfr0001()
    {
        // CET-51 (CB-904 / NFR-0001):
        // - Operaciones estándar <= 3000 ms (p95) con 25 usuarios simultáneos.
        // - Consultas consolidadas y reportes <= 5000 ms (p95) con 25 usuarios simultáneos.
        await using var fabrica = cluster.CrearFabricaWeb();
        await SembrarAdministradorInicialAsync(fabrica.Services);

        using var clienteSesion = CrearClienteConCookies(fabrica);
        await IniciarSesionAsync(clienteSesion, "admin.inicial", "AdminSeguro2026!");

        const int concurrencia = 25;

        var tiemposEstandarMs = await Task.WhenAll(
            Enumerable.Range(0, concurrencia).Select(async _ =>
            {
                var cronometro = Stopwatch.StartNew();
                using var resp = await clienteSesion.GetAsync("/api/obras");
                cronometro.Stop();
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
                return cronometro.Elapsed.TotalMilliseconds;
            }));

        var tiemposConsolidadosMs = await Task.WhenAll(
            Enumerable.Range(0, concurrencia).Select(async _ =>
            {
                var cronometro = Stopwatch.StartNew();
                using var resp = await clienteSesion.GetAsync("/api/asistencias/consolidado");
                cronometro.Stop();
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
                return cronometro.Elapsed.TotalMilliseconds;
            }));

        var p95Estandar = CalcularPercentil95(tiemposEstandarMs);
        var p95Consolidado = CalcularPercentil95(tiemposConsolidadosMs);

        Assert.True(
            p95Estandar <= 3000.0,
            $"El p95 de operaciones estándar fue {p95Estandar:F2} ms y superó el umbral de 3000 ms (NFR-0001).");
        Assert.True(
            p95Consolidado <= 5000.0,
            $"El p95 de consultas consolidadas fue {p95Consolidado:F2} ms y superó el umbral de 5000 ms (NFR-0001).");
    }

    private static double CalcularPercentil95(double[] muestrasMs)
    {
        var ordenadas = muestrasMs.OrderBy(x => x).ToArray();
        var indice = (int)Math.Ceiling(ordenadas.Length * 0.95) - 1;
        return ordenadas[Math.Clamp(indice, 0, ordenadas.Length - 1)];
    }

    private static async Task SembrarAdministradorInicialAsync(IServiceProvider serviciosRaiz)
    {
        using var alcance = serviciosRaiz.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ContextoConstructora>();
        if (await contexto.Usuarios.AnyAsync(u => u.NombreUsuario == "admin.inicial")) return;
        var inicializador = alcance.ServiceProvider.GetRequiredService<IInicializadorSeguridad>();
        var resultado = await inicializador.CrearAdministradorInicialAsync("admin.inicial", "AdminSeguro2026!");
        Assert.True(resultado.EsExitoso, resultado.Error?.Mensaje);
    }

    private static HttpClient CrearClienteSinCookies(WebApplicationFactory<Program> fabrica) => ClienteApiPruebas.Crear(fabrica);
    private static HttpClient CrearClienteConCookies(WebApplicationFactory<Program> fabrica) => ClienteApiPruebas.Crear(fabrica);

    private static async Task IniciarSesionAsync(HttpClient cliente, string usuario, string contrasena)
    {
        using var resp = await cliente.PostAsJsonAsync(
            "/api/sesion/iniciar",
            new SolicitudIniciarSesion(usuario, contrasena));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private static async Task<TRespuesta> PostExitosoAsync<TSolicitud, TRespuesta>(
        HttpClient cliente,
        string ruta,
        TSolicitud solicitud)
    {
        using var respuesta = await cliente.PostAsJsonAsync(ruta, solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync();
            Assert.Fail($"POST {ruta} devolvió {(int)respuesta.StatusCode} ({respuesta.StatusCode}): {detalle}");
        }

        var valor = await respuesta.Content.ReadFromJsonAsync<TRespuesta>();
        Assert.NotNull(valor);
        return valor;
    }
}
