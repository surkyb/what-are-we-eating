using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class PermisoConcedidoConfiguration : IEntityTypeConfiguration<PermisoConcedido>
{
    public void Configure(EntityTypeBuilder<PermisoConcedido> builder)
    {
        builder.ToTable("PermisosConcedidos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Permiso).IsRequired().HasMaxLength(100);
        builder.Property(p => p.FechaConcesion).IsRequired();

        // Un usuario recibe cada permiso una sola vez
        builder.HasIndex(p => new { p.UsuarioId, p.Permiso }).IsUnique();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SolicitudPermiso>()
            .WithMany()
            .HasForeignKey(p => p.SolicitudId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
