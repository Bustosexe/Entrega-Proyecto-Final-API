using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechHub.Api.Data;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TechHub.Api.Migrations
{
    /// <summary>
    /// De 13 a 18 tablas (solo agrega tablas nuevas, no toca datos existentes):
    ///   Ofertas                 descuento temporal por producto
    ///   Combos + ComboItems     paquetes de productos con descuento
    ///   CategoriasCompatibles   qué categorías pueden ir juntas en un combo (motherboard + RAM sí; RAM + fuente no)
    ///   Auditoria               bitácora de operaciones (quién, qué y cuándo)
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261115060000_OfertasCombosAuditoria")]
    public partial class OfertasCombosAuditoria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auditoria_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Combos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Porcentaje = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combos", x => x.Id);
                    table.CheckConstraint("CK_Combos_Porcentaje", "[Porcentaje] BETWEEN 1 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "Ofertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Porcentaje = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hasta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ofertas", x => x.Id);
                    table.CheckConstraint("CK_Ofertas_Porcentaje", "[Porcentaje] BETWEEN 1 AND 90");
                    table.CheckConstraint("CK_Ofertas_Vigencia", "[Hasta] >= [Desde]");
                    table.ForeignKey(
                        name: "FK_Ofertas_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasCompatibles",
                columns: table => new
                {
                    CategoriaAId = table.Column<int>(type: "int", nullable: false),
                    CategoriaBId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasCompatibles", x => new { x.CategoriaAId, x.CategoriaBId });
                    table.CheckConstraint("CK_CategoriasCompatibles_Orden", "[CategoriaAId] < [CategoriaBId]");
                    table.ForeignKey(
                        name: "FK_CategoriasCompatibles_Categorias_CategoriaAId",
                        column: x => x.CategoriaAId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoriasCompatibles_Categorias_CategoriaBId",
                        column: x => x.CategoriaBId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComboItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComboId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboItems", x => x.Id);
                    table.CheckConstraint("CK_ComboItems_Cantidad", "[Cantidad] BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_ComboItems_Combos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Combos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComboItems_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Reglas de compatibilidad (1 Procesadores, 2 Tarjetas Gráficas, 3 Motherboards, 4 Memorias RAM,
            // 5 Almacenamiento M.2, 6 Fuentes de Poder). La RAM nunca se combina con la fuente.
            migrationBuilder.InsertData(
                table: "CategoriasCompatibles",
                columns: new[] { "CategoriaAId", "CategoriaBId" },
                values: new object[,]
                {
                    { 1, 2 }, { 1, 3 }, { 1, 4 }, { 1, 5 },
                    { 2, 3 }, { 2, 6 },
                    { 3, 4 }, { 3, 5 }, { 3, 6 },
                    { 4, 5 }
                });

            migrationBuilder.CreateIndex(name: "IX_Auditoria_Entidad_EntidadId", table: "Auditoria", columns: new[] { "Entidad", "EntidadId" });
            migrationBuilder.CreateIndex(name: "IX_Auditoria_Fecha", table: "Auditoria", column: "Fecha");
            migrationBuilder.CreateIndex(name: "IX_Auditoria_UsuarioId", table: "Auditoria", column: "UsuarioId");
            migrationBuilder.CreateIndex(name: "IX_CategoriasCompatibles_CategoriaBId", table: "CategoriasCompatibles", column: "CategoriaBId");
            migrationBuilder.CreateIndex(name: "IX_ComboItems_ComboId_ProductoId", table: "ComboItems", columns: new[] { "ComboId", "ProductoId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ComboItems_ProductoId", table: "ComboItems", column: "ProductoId");
            migrationBuilder.CreateIndex(name: "IX_Combos_Nombre", table: "Combos", column: "Nombre", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Ofertas_ProductoId_Hasta", table: "Ofertas", columns: new[] { "ProductoId", "Hasta" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Auditoria");
            migrationBuilder.DropTable(name: "CategoriasCompatibles");
            migrationBuilder.DropTable(name: "ComboItems");
            migrationBuilder.DropTable(name: "Ofertas");
            migrationBuilder.DropTable(name: "Combos");
        }
    }
}
