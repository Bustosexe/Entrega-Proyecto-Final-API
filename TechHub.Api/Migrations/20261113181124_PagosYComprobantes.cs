using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class PagosYComprobantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComprobanteArchivo",
                table: "Salidas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComprobanteFecha",
                table: "Salidas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRechazo",
                table: "Salidas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComprobanteArchivo",
                table: "Salidas");

            migrationBuilder.DropColumn(
                name: "ComprobanteFecha",
                table: "Salidas");

            migrationBuilder.DropColumn(
                name: "MotivoRechazo",
                table: "Salidas");
        }
    }
}
