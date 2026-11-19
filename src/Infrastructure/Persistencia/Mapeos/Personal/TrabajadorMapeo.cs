using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class TrabajadorMapeo : IEntityTypeConfiguration<Trabajador>
{
    public void Configure(EntityTypeBuilder<Trabajador> entidad)
    {
        entidad.HasKey(registro => registro.IdTrabajador).HasName("trabajadores_pkey");

        entidad.ToTable("trabajadores", "public", configuracionTabla =>
        {
            configuracionTabla.HasCheckConstraint("ck_trabajador_estado", "((estado)::text = ANY ((ARRAY['ACTIVO'::character varying, 'INACTIVO'::character varying])::text[]))");
        });

        entidad.HasIndex(registro => registro.Cedula, "trabajadores_cedula_key").IsUnique();

        entidad.HasIndex(registro => registro.Correo, "ux_trabajador_correo")
            .IsUnique()
            .HasFilter("(correo IS NOT NULL)");

        entidad.Property(registro => registro.IdTrabajador).HasColumnName("id_trabajador");
        entidad.Property(registro => registro.Cedula)
            .HasMaxLength(20)
            .HasColumnName("cedula");
        entidad.Property(registro => registro.Correo)
            .HasMaxLength(100)
            .HasColumnName("correo");
        entidad.Property(registro => registro.Direccion)
            .HasMaxLength(200)
            .HasColumnName("direccion");
        entidad.Property(registro => registro.Estado)
            .HasMaxLength(15)
            .HasColumnName("estado");
        entidad.Property(registro => registro.FechaIngreso).HasColumnName("fecha_ingreso");
        entidad.Property(registro => registro.IdCargo).HasColumnName("id_cargo");
        entidad.Property(registro => registro.PrimerApellido)
            .HasMaxLength(50)
            .HasColumnName("primer_apellido");
        entidad.Property(registro => registro.PrimerNombre)
            .HasMaxLength(50)
            .HasColumnName("primer_nombre");
        entidad.Property(registro => registro.SegundoApellido)
            .HasMaxLength(50)
            .HasColumnName("segundo_apellido");
        entidad.Property(registro => registro.SegundoNombre)
            .HasMaxLength(50)
            .HasColumnName("segundo_nombre");
        entidad.Property(registro => registro.Telefono)
            .HasMaxLength(20)
            .HasColumnName("telefono");

        entidad.HasOne<Cargo>().WithMany()
            .HasForeignKey(registro => registro.IdCargo)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_trabajador_cargo");
    }
}
