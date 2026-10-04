using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class CorreoEnColaConfiguration : IEntityTypeConfiguration<CorreoEnCola>
{
    public void Configure(EntityTypeBuilder<CorreoEnCola> builder)
    {
        builder.ToTable("CorreosEnCola");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Destinatario)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.Asunto)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Cuerpo)
            .IsRequired();

        builder.Property(c => c.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.FechaCreacion)
            .IsRequired();

        builder.Property(c => c.FechaEnvio)
            .IsRequired(false);

        builder.Property(c => c.Intentos)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.UltimoError)
            .IsRequired(false);
    }
}
