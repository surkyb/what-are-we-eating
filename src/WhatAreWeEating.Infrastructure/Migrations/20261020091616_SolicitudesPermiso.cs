using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatAreWeEating.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SolicitudesPermiso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesPermiso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAplicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPermiso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesPermiso_Usuarios_ResueltaPorId",
                        column: x => x.ResueltaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SolicitudesPermiso_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermisosConcedidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaConcesion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosConcedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosConcedidos_SolicitudesPermiso_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "SolicitudesPermiso",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PermisosConcedidos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermisosConcedidos_SolicitudId",
                table: "PermisosConcedidos",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_PermisosConcedidos_UsuarioId_Permiso",
                table: "PermisosConcedidos",
                columns: new[] { "UsuarioId", "Permiso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_Estado",
                table: "SolicitudesPermiso",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_ResueltaPorId",
                table: "SolicitudesPermiso",
                column: "ResueltaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_SolicitanteId",
                table: "SolicitudesPermiso",
                column: "SolicitanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermisosConcedidos");

            migrationBuilder.DropTable(
                name: "SolicitudesPermiso");
        }
    }
}
