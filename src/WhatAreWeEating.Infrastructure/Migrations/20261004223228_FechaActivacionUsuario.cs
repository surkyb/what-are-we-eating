using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatAreWeEating.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FechaActivacionUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActivacion",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Usuarios SET FechaActivacion = FechaCreacion WHERE Activo = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaActivacion",
                table: "Usuarios");
        }
    }
}
