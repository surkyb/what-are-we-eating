using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatAreWeEating.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecetaMotivoRechazo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "Recetas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "Recetas");
        }
    }
}
