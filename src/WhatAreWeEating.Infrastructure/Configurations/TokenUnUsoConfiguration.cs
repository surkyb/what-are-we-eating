using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class TokenUnUsoConfiguration : IEntityTypeConfiguration<TokenUnUso>
{
    public void Configure(EntityTypeBuilder<TokenUnUso> builder)
    {
        builder.ToTable("TokensUnUso");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UsuarioId)
            .IsRequired();

        builder.Property(t => t.Tipo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(t => t.TokenHash);

        builder.Property(t => t.FechaVencimiento)
            .IsRequired();

        builder.Property(t => t.Usado)
            .IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
