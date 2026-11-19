using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Personal;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.EPP;

public sealed class EntregaEppMapeo : IEntityTypeConfiguration<EntregaEpp>
{
    public void Configure(EntityTypeBuilder<EntregaEpp> entidad)
    {
        entidad.HasKey(registro => registro.IdEntregaEpp).HasName("entregas_epp_pkey");

        entidad.ToTable("entregas_epp", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_entrega_epp_estado", "((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'DEVUELTA'::character varying, 'PERDIDA'::character varying, 'REEMPLAZADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_entrega_epp_fecha", "((devuelto_en IS NULL) OR (devuelto_en >= entregado_en))");
        });

        entidad.HasIndex(registro => registro.IdEpp, "ux_entrega_epp_activa")
            .IsUnique()
            .HasFilter("((estado)::text = 'ACTIVA'::text)");

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_entrega_epp_uuid").IsUnique();

        entidad.Property(registro => registro.IdEntregaEpp).HasColumnName("id_entrega_epp");
        entidad.Property(registro => registro.CondicionDevolucion)
            .HasMaxLength(20)
            .HasColumnName("condicion_devolucion");
        entidad.Property(registro => registro.CondicionEntrega)
            .HasMaxLength(20)
            .HasColumnName("condicion_entrega");
        entidad.Property(registro => registro.CreadoEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("creado_en");
        entidad.Property(registro => registro.DevueltoEn)
            .HasPrecision(0)
            .HasColumnName("devuelto_en");
        entidad.Property(registro => registro.EntregadoEn)
            .HasPrecision(0)
            .HasColumnName("entregado_en");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdAsignacion).HasColumnName("id_asignacion");
        entidad.Property(registro => registro.IdEpp).HasColumnName("id_epp");
        entidad.Property(registro => registro.IdUsuarioEntrega).HasColumnName("id_usuario_entrega");
        entidad.Property(registro => registro.RutaConstancia)
            .HasMaxLength(500)
            .HasColumnName("ruta_constancia");
        entidad.Property(registro => registro.SincronizadoEn)
            .HasPrecision(0)
            .HasColumnName("sincronizado_en");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<AsignacionTrabajadorObra>().WithMany()
            .HasForeignKey(registro => registro.IdAsignacion)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_entrega_epp_asignacion");

        entidad.HasOne<Epp>().WithOne()
            .HasForeignKey<EntregaEpp>(registro => registro.IdEpp)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_entrega_epp_epp");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioEntrega)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_entrega_epp_usuario");
    }
}
