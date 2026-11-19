using ConstructoraEderTorres.Dominio.Obras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Obras;

public sealed class ObraMapeo : IEntityTypeConfiguration<Obra>
{
    public void Configure(EntityTypeBuilder<Obra> entidad)
    {
        entidad.HasKey(registro => registro.IdObra).HasName("obras_pkey");

        entidad.ToTable("obras", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_obra_estado", "((estado)::text = ANY ((ARRAY['PLANIFICADA'::character varying, 'ACTIVA'::character varying, 'SUSPENDIDA'::character varying, 'FINALIZADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_obra_fechas_plan", "(fecha_fin_plan >= fecha_inicio_plan)");
            configuracionTabla.HasCheckConstraint("ck_obra_fechas_real", "((fecha_fin_real IS NULL) OR (fecha_inicio_real IS NULL) OR (fecha_fin_real >= fecha_inicio_real))");
            configuracionTabla.HasCheckConstraint("ck_obra_geocerca", "(((latitud IS NULL) AND (longitud IS NULL) AND (radio_asistencia_metros IS NULL)) OR ((latitud IS NOT NULL) AND (longitud IS NOT NULL) AND (radio_asistencia_metros IS NOT NULL) AND (latitud >= '-90'::numeric) AND (latitud <= (90)::numeric) AND (longitud >= '-180'::numeric) AND (longitud <= (180)::numeric) AND (radio_asistencia_metros > (0)::numeric)))");
        });

        entidad.HasIndex(registro => registro.Codigo, "obras_codigo_key").IsUnique();

        entidad.Property(registro => registro.IdObra).HasColumnName("id_obra");
        entidad.Property(registro => registro.Codigo)
            .HasMaxLength(20)
            .HasColumnName("codigo");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Direccion)
            .HasMaxLength(200)
            .HasColumnName("direccion");
        entidad.Property(registro => registro.Latitud).HasPrecision(9, 6).HasColumnName("latitud");
        entidad.Property(registro => registro.Longitud).HasPrecision(9, 6).HasColumnName("longitud");
        entidad.Property(registro => registro.RadioAsistenciaMetros).HasPrecision(10, 2).HasColumnName("radio_asistencia_metros");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaFinPlan).HasColumnName("fecha_fin_plan");
        entidad.Property(registro => registro.FechaFinReal).HasColumnName("fecha_fin_real");
        entidad.Property(registro => registro.FechaInicioPlan).HasColumnName("fecha_inicio_plan");
        entidad.Property(registro => registro.FechaInicioReal).HasColumnName("fecha_inicio_real");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(120)
            .HasColumnName("nombre");
    }
}
