using ConstructoraEderTorres.Dominio.Personal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructoraEderTorres.Infraestructura.Persistencia.Mapeos.Personal;

public sealed class EspecialidadMapeo : IEntityTypeConfiguration<Especialidad>
{
    public void Configure(EntityTypeBuilder<Especialidad> entidad)
    {
        entidad.HasKey(registro => registro.IdEspecialidad).HasName("especialidades_pkey");

        entidad.ToTable("especialidades", "public");

        entidad.HasIndex(registro => registro.Nombre, "especialidades_nombre_key").IsUnique();

        entidad.Property(registro => registro.IdEspecialidad).HasColumnName("id_especialidad");
        entidad.Property(registro => registro.Activo)
            .HasDefaultValue(true)
            .HasColumnName("activo");
        entidad.Property(registro => registro.Descripcion)
            .HasMaxLength(200)
            .HasColumnName("descripcion");
        entidad.Property(registro => registro.Nombre)
            .HasMaxLength(60)
            .HasColumnName("nombre");
    }
}
