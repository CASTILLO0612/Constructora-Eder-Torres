using ConstructoraEderTorres.Dominio.EPP;
using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.EPP;

public sealed class ReposicionEppMapeo : IEntityTypeConfiguration<ReposicionEpp>
{
    public void Configure(EntityTypeBuilder<ReposicionEpp> entidad)
    {
        entidad.HasKey(registro => registro.IdReposicionEpp).HasName("reposiciones_epp_pkey");

        entidad.ToTable("reposiciones_epp", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_reposicion_estado", "((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'APROBADA'::character varying, 'RECHAZADA'::character varying, 'EJECUTADA'::character varying, 'ANULADA'::character varying])::text[]))");
            configuracionTabla.HasCheckConstraint("ck_reposicion_fechas", "((resuelta_en IS NULL) OR (resuelta_en >= solicitada_en))");
            configuracionTabla.HasCheckConstraint("ck_reposicion_motivo", "((motivo)::text = ANY ((ARRAY['DANO'::character varying, 'PERDIDA'::character varying, 'DESGASTE'::character varying, 'VENCIMIENTO'::character varying, 'OTRO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.IdEntregaNueva, "ux_reposicion_entrega_nueva")
            .IsUnique()
            .HasFilter("(id_entrega_nueva IS NOT NULL)");

        entidad.HasIndex(registro => registro.UuidRegistro, "ux_reposicion_uuid").IsUnique();

        entidad.Property(registro => registro.IdReposicionEpp).HasColumnName("id_reposicion_epp");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(300)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.EquipoDevuelto)
            .HasDefaultValue(false)
            .HasColumnName("equipo_devuelto");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(20)
            .HasColumnName("estado");
        entidad.Property(registro => registro.IdEntregaNueva).HasColumnName("id_entrega_nueva");
        entidad.Property(registro => registro.IdEntregaOrigen).HasColumnName("id_entrega_origen");
        entidad.Property(registro => registro.IdUsuarioAprueba).HasColumnName("id_usuario_aprueba");
        entidad.Property(registro => registro.IdUsuarioSolicita).HasColumnName("id_usuario_solicita");
        entidad.Property(registro => registro.Motivo)
            .HasMaxLength(30)
            .HasColumnName("motivo");
        entidad.Property(registro => registro.ResueltaEn)
            .HasPrecision(0)
            .HasColumnName("resuelta_en");
        entidad.Property(registro => registro.SolicitadaEn)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("solicitada_en");
        entidad.Property(registro => registro.UuidRegistro)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_registro");

        entidad.HasOne<EntregaEpp>().WithOne()
            .HasForeignKey<ReposicionEpp>(registro => registro.IdEntregaNueva)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_reposicion_nueva");

        entidad.HasOne<EntregaEpp>().WithMany()
            .HasForeignKey(registro => registro.IdEntregaOrigen)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_reposicion_origen");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioAprueba)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_reposicion_aprueba");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuarioSolicita)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_reposicion_solicita");
    }
}
