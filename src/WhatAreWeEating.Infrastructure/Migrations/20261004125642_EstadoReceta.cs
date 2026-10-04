using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatAreWeEating.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstadoReceta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoPublicacion",
                table: "Recetas");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Recetas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Borrador");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Recetas");

            migrationBuilder.AddColumn<string>(
                name: "EstadoPublicacion",
                table: "Recetas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }
    }
}
