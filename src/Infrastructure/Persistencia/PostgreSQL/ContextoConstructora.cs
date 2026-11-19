using ConstructoraEderTorres.Dominio.Asistencia;
using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Herramientas;
using ConstructoraEderTorres.Dominio.Obras;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.SeguimientoObra;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.PostgreSQL;

public sealed class ContextoConstructora(
    DbContextOptions<ContextoConstructora> opciones) : DbContext(opciones)
{
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Especialidad> Especialidades => Set<Especialidad>();
    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();
    public DbSet<Obra> Obras => Set<Obra>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SesionUsuario> SesionesUsuario => Set<SesionUsuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<UsuarioRol> UsuariosRol => Set<UsuarioRol>();
    public DbSet<RolPermiso> RolesPermiso => Set<RolPermiso>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<Cuadrilla> Cuadrillas => Set<Cuadrilla>();
    public DbSet<TrabajadorEspecialidad> TrabajadoresEspecialidad => Set<TrabajadorEspecialidad>();
    public DbSet<AsignacionTrabajadorObra> AsignacionesTrabajadorObra => Set<AsignacionTrabajadorObra>();
    public DbSet<CuadrillaObra> CuadrillasObra => Set<CuadrillaObra>();
    public DbSet<TrabajadorCuadrilla> TrabajadoresCuadrilla => Set<TrabajadorCuadrilla>();
    public DbSet<RegistroAsistencia> Asistencias => Set<RegistroAsistencia>();
    public DbSet<CorreccionAsistencia> CorreccionesAsistencia => Set<CorreccionAsistencia>();
    public DbSet<Actividad> Actividades => Set<Actividad>();
    public DbSet<TrabajadorActividad> TrabajadoresActividad => Set<TrabajadorActividad>();
    public DbSet<Avance> Avances => Set<Avance>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<ActividadIncidencia> ActividadesIncidencia => Set<ActividadIncidencia>();
    public DbSet<Acta> Actas => Set<Acta>();
    public DbSet<ActaAvance> ActasAvance => Set<ActaAvance>();
    public DbSet<ActaIncidencia> ActasIncidencia => Set<ActaIncidencia>();
    public DbSet<TrabajadorActa> TrabajadoresActa => Set<TrabajadorActa>();
    public DbSet<AprobacionActa> AprobacionesActa => Set<AprobacionActa>();
    public DbSet<TipoEpp> TiposEpp => Set<TipoEpp>();
    public DbSet<Epp> EquiposProteccionPersonal => Set<Epp>();
    public DbSet<EntregaEpp> EntregasEpp => Set<EntregaEpp>();
    public DbSet<ReposicionEpp> ReposicionesEpp => Set<ReposicionEpp>();
    public DbSet<MovimientoExistenciasEpp> MovimientosExistenciasEpp => Set<MovimientoExistenciasEpp>();
    public DbSet<CategoriaHerramienta> CategoriasHerramienta => Set<CategoriaHerramienta>();
    public DbSet<TipoHerramienta> TiposHerramienta => Set<TipoHerramienta>();
    public DbSet<Herramienta> Herramientas => Set<Herramienta>();
    public DbSet<AsignacionHerramienta> AsignacionesHerramienta => Set<AsignacionHerramienta>();
    public DbSet<IncidenciaHerramienta> IncidenciasHerramienta => Set<IncidenciaHerramienta>();

    protected override void ConfigureConventions(ModelConfigurationBuilder constructorConvenciones)
    {
        base.ConfigureConventions(constructorConvenciones);
        constructorConvenciones.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        base.OnModelCreating(constructorModelo);
        constructorModelo.HasPostgresExtension("pgcrypto");
        constructorModelo.HasPostgresExtension("pg_trgm");
        constructorModelo.HasDbFunction(typeof(BusquedaPostgreSql).GetMethod(nameof(BusquedaPostgreSql.Normalizar))!)
            .HasParameter("valores").HasStoreType("text[]");
        // Evita que la inferencia de varchar(n) en arrays introduzca conversiones que impiden usar el índice.
        constructorModelo.HasDbFunction(typeof(BusquedaPostgreSql).GetMethod(nameof(BusquedaPostgreSql.ComoTexto))!)
            .HasTranslation(argumentos => new Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlUnaryExpression(
                System.Linq.Expressions.ExpressionType.Convert, argumentos[0], typeof(string),
                new Microsoft.EntityFrameworkCore.Storage.StringTypeMapping("text", System.Data.DbType.String)));
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoConstructora).Assembly);
    }
}
