using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class SesionConfiguration : IEntityTypeConfiguration<Sesion>
{
    public void Configure(EntityTypeBuilder<Sesion> builder)
    {
        builder.ToTable("Sesiones");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UsuarioId)
            .IsRequired();

        builder.HasIndex(s => s.UsuarioId);

        builder.Property(s => s.FechaEmision)
            .IsRequired();

        builder.Property(s => s.FechaExpiracion)
            .IsRequired();

        builder.Property(s => s.Revocada)
            .IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
