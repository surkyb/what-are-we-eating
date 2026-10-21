using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatAreWeEating.Core.Entities;

namespace WhatAreWeEating.Infrastructure.Configurations;

public class SolicitudPermisoConfiguration : IEntityTypeConfiguration<SolicitudPermiso>
{
    public void Configure(EntityTypeBuilder<SolicitudPermiso> builder)
    {
        builder.ToTable("SolicitudesPermiso");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permiso).IsRequired().HasMaxLength(100);

        builder.Property(s => s.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.FechaCreacion).IsRequired();
        builder.Property(s => s.MotivoRechazo).HasMaxLength(500);
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => s.SolicitanteId);
        builder.HasIndex(s => s.Estado);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.SolicitanteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.ResueltaPorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
