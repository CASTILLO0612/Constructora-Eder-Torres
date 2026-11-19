using ConstructoraEderTorres.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Seguridad;

public sealed class AuditoriaMapeo : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> entidad)
    {
        entidad.HasKey(registro => registro.IdAuditoria).HasName("auditorias_pkey");

        entidad.ToTable("auditorias", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_auditoria_accion", "((accion)::text = ANY ((ARRAY['CREAR'::character varying, 'MODIFICAR'::character varying, 'ANULAR'::character varying, 'APROBAR'::character varying, 'RECHAZAR'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.UuidOperacion, "ux_auditoria_uuid").IsUnique();

        entidad.Property(registro => registro.IdAuditoria).HasColumnName("id_auditoria");
        entidad.Property(registro => registro.Accion)
            .HasMaxLength(20)
            .HasColumnName("accion");
        entidad.Property(registro => registro.Campo)
            .HasMaxLength(60)
            .HasColumnName("campo");
        entidad.Property(registro => registro.Entidad)
            .HasMaxLength(60)
            .HasColumnName("entidad");
        entidad.Property(registro => registro.FechaHora)
            .HasPrecision(0)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasColumnName("fecha_hora");
        entidad.Property(registro => registro.IdRegistro)
            .HasMaxLength(50)
            .HasColumnName("id_registro");
        entidad.Property(registro => registro.IdUsuario).HasColumnName("id_usuario");
        entidad.Property(registro => registro.IpOrigen).HasColumnName("ip_origen");
        entidad.Property(registro => registro.UuidOperacion)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("uuid_operacion");
        entidad.Property(registro => registro.ValorAnterior)
            .HasMaxLength(1000)
            .HasColumnName("valor_anterior");
        entidad.Property(registro => registro.ValorNuevo)
            .HasMaxLength(1000)
            .HasColumnName("valor_nuevo");

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(registro => registro.IdUsuario)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_auditoria_usuario");
    }
}
