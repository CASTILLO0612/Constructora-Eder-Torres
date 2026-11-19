using ConstructoraEderTorres.Dominio.Asistencia;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Asistencia;

public sealed class RegistroAsistenciaMapeo : IEntityTypeConfiguration<RegistroAsistencia>
{
    public void Configure(EntityTypeBuilder<RegistroAsistencia> entidad)
    {
        entidad.HasKey(registro => registro.IdAsistencia).HasName("asistencias_pkey");

        entidad.ToTable("asistencias", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_asistencia_estado", "((estado)::text = ANY ((ARRAY['ABIERTA'::character varying, 'COMPLETA'::character varying, 'CORREGIDA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_horas", "((salida_at IS NULL) OR (salida_at >= entrada_at))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_precision_gps", "(((precision_gps_entrada_metros IS NULL) OR ((precision_gps_entrada_metros >= (0)::numeric) AND (precision_gps_entrada_metros <= 99999999.99) AND (latitud_entrada IS NOT NULL) AND (longitud_entrada IS NOT NULL))) AND ((precision_gps_salida_metros IS NULL) OR ((precision_gps_salida_metros >= (0)::numeric) AND (precision_gps_salida_metros <= 99999999.99) AND (latitud_salida IS NOT NULL) AND (longitud_salida IS NOT NULL) AND (salida_at IS NOT NULL))))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_lat_entrada", "((latitud_entrada IS NULL) OR ((latitud_entrada >= ('-90'::integer)::numeric) AND (latitud_entrada <= (90)::numeric)))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_lat_salida", "((latitud_salida IS NULL) OR ((latitud_salida >= ('-90'::integer)::numeric) AND (latitud_salida <= (90)::numeric)))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_lon_entrada", "((longitud_entrada IS NULL) OR ((longitud_entrada >= ('-180'::integer)::numeric) AND (longitud_entrada <= (180)::numeric)))");
            configuracionTabla.HasCheckConstraint("ck_asistencia_lon_salida", "((longitud_salida IS NULL) OR ((longitud_salida >= ('-180'::integer)::numeric) AND (longitud_salida <= (180)::numeric)))");
        });

        entidad.HasIndex(registro => new { registro.IdAsignacion, registro.EntradaEn }, "ux_asistencia_marcacion").IsUnique();

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_asistencia_uuid").IsUnique();
        entidad.HasIndex(registro => registro.IdAsignacion, "ux_asistencia_abierta_asignacion")
            .IsUnique().HasFilter("salida_at IS NULL AND estado <> 'ANULADA'");

        entidad.Property(registro => registro.IdAsistencia).HasColumnName("id_asistencia");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.EntradaEn)
            .HasPrecision(0)
            .HasColumnName("entrada_at");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaJornada).HasColumnName("fecha_jornada");
        entidad.Property(registro => registro.PrecisionGpsEntradaMetros).HasPrecision(10, 2).HasColumnName("precision_gps_entrada_metros");
        entidad.Property(registro => registro.PrecisionGpsSalidaMetros).HasPrecision(10, 2).HasColumnName("precision_gps_salida_metros");
        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.IdUsuarioRegistro).HasColumnName("id_usuario_registro");
        entidad.Property(registro => registro.LatitudEntrada)
            .HasPrecision(9, 6)
            .HasColumnName("latitud_entrada");
        entidad.Property(registro => registro.LatitudSalida)
            .HasPrecision(9, 6)
            .HasColumnName("latitud_salida");
        entidad.Property(registro => registro.LongitudEntrada)
            .HasPrecision(9, 6)
            .HasColumnName("longitud_entrada");
        entidad.Property(registro => registro.LongitudSalida)
            .HasPrecision(9, 6)
            .HasColumnName("longitud_salida");
        entidad.Property(registro => registro.SalidaEn)
            .HasPrecision(0)
            .HasColumnName("salida_at");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacion)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asistencia_asignacion");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioRegistro)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asistencia_usuario");
    }
}
