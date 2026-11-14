using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TechHub.Api.Migrations
{
    /// <summary>
    /// Normalización a 3FN: de 7 a 13 tablas, SIN perder datos.
    ///   Usuarios.Rol (texto)        → tabla Roles + Usuarios.RolId
    ///   Productos.Marca (texto)     → tabla Marcas + Productos.MarcaId
    ///   Ingresos                    → Compras (cabecera por factura) + DetallesCompra (renglones)
    ///   Salidas                     → Pedidos (cabecera) + DetallesPedido (renglones)
    ///   Salidas.Estado (texto)      → tabla EstadosPedido + Pedidos.EstadoId
    ///   Estado/comprobante/rechazo  → HistorialPedidos (cada cambio de estado con fecha, usuario y comentario)
    /// Las columnas calculables (Total, Subtotal) ya no se guardan: se calculan Cantidad × Precio.
    /// </summary>
    public partial class Normalizacion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================
            // 1) Roles
            // =========================================================
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, "Personal de la tienda: gestiona catálogo, stock, pedidos y reportes", "Admin" },
                    { 2, "Cliente registrado desde la app: compra y sigue sus pedidos", "Comprador" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nombre",
                table: "Roles",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "RolId",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("UPDATE Usuarios SET RolId = CASE WHEN Rol = 'Admin' THEN 1 ELSE 2 END;");

            migrationBuilder.DropColumn(
                name: "Rol",
                table: "Usuarios");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Roles_RolId",
                table: "Usuarios",
                column: "RolId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =========================================================
            // 2) Marcas
            // =========================================================
            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.Sql(@"
INSERT INTO Marcas (Nombre, Activo)
SELECT DISTINCT CASE WHEN LTRIM(RTRIM(Marca)) = '' THEN N'Sin marca' ELSE LTRIM(RTRIM(Marca)) END, CAST(1 AS bit)
FROM Productos;");

            migrationBuilder.AddColumn<int>(
                name: "MarcaId",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
UPDATE p SET p.MarcaId = m.Id
FROM Productos p
JOIN Marcas m ON m.Nombre = CASE WHEN LTRIM(RTRIM(p.Marca)) = '' THEN N'Sin marca' ELSE LTRIM(RTRIM(p.Marca)) END;");

            migrationBuilder.DropColumn(
                name: "Marca",
                table: "Productos");

            migrationBuilder.CreateIndex(
                name: "IX_Marcas_Nombre",
                table: "Marcas",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_MarcaId",
                table: "Productos",
                column: "MarcaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Marcas_MarcaId",
                table: "Productos",
                column: "MarcaId",
                principalTable: "Marcas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =========================================================
            // 3) EstadosPedido
            // =========================================================
            migrationBuilder.CreateTable(
                name: "EstadosPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosPedido", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EstadosPedido",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: new object[,]
                {
                    { 1, "Pendiente", "Esperando pago" },
                    { 2, "EnRevision", "Pago en revisión" },
                    { 3, "Confirmado", "Pago confirmado" },
                    { 4, "Entregado", "Entregado" },
                    { 5, "Cancelado", "Cancelado" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstadosPedido_Codigo",
                table: "EstadosPedido",
                column: "Codigo",
                unique: true);

            // =========================================================
            // 4) Pedidos (cabecera) ← Salidas agrupadas por CodigoPedido
            // =========================================================
            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ComprobanteArchivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    EstadoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_EstadosPedido_EstadoId",
                        column: x => x.EstadoId,
                        principalTable: "EstadosPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
INSERT INTO Pedidos (Codigo, Fecha, Observaciones, ComprobanteArchivo, ClienteId, UsuarioId, EstadoId)
SELECT s.CodigoPedido, MIN(s.Fecha), MAX(s.Observaciones), MAX(s.ComprobanteArchivo), MIN(s.ClienteId), MIN(s.UsuarioId),
       CASE MAX(s.Estado)
            WHEN 'Pendiente'  THEN 1
            WHEN 'EnRevision' THEN 2
            WHEN 'Confirmado' THEN 3
            WHEN 'Entregado'  THEN 4
            WHEN 'Cancelado'  THEN 5
            ELSE 1 END
FROM Salidas s
GROUP BY s.CodigoPedido;");

            // =========================================================
            // 5) DetallesPedido (renglones) ← Salidas
            // =========================================================
            migrationBuilder.CreateTable(
                name: "DetallesPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedido", x => x.Id);
                    table.CheckConstraint("CK_DetallesPedido_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallesPedido_Precio", "[PrecioUnitario] >= 0");
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
INSERT INTO DetallesPedido (PedidoId, ProductoId, Cantidad, PrecioUnitario)
SELECT p.Id, s.ProductoId, SUM(s.Cantidad), MAX(s.PrecioUnitario)
FROM Salidas s
JOIN Pedidos p ON p.Codigo = s.CodigoPedido
GROUP BY p.Id, s.ProductoId;");

            // =========================================================
            // 6) HistorialPedidos ← se reconstruye a partir del estado, el comprobante y el rechazo
            //    (en la versión anterior solo se guardaba el estado actual).
            // =========================================================
            migrationBuilder.CreateTable(
                name: "HistorialPedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    EstadoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialPedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialPedidos_EstadosPedido_EstadoId",
                        column: x => x.EstadoId,
                        principalTable: "EstadosPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialPedidos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialPedidos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // a) Alta del pedido (una venta en el local hecha por un admin nace "Entregado").
            migrationBuilder.Sql(@"
INSERT INTO HistorialPedidos (PedidoId, EstadoId, Fecha, UsuarioId, Comentario)
SELECT p.Id,
       CASE WHEN p.EstadoId = 4 AND u.RolId = 1 THEN 4 ELSE 1 END,
       p.Fecha, p.UsuarioId, NULL
FROM Pedidos p
JOIN Usuarios u ON u.Id = p.UsuarioId;");

            // b) Comprobante enviado → "Pago en revisión".
            migrationBuilder.Sql(@"
INSERT INTO HistorialPedidos (PedidoId, EstadoId, Fecha, UsuarioId, Comentario)
SELECT p.Id, 2, x.ComprobanteFecha, p.UsuarioId, NULL
FROM Pedidos p
JOIN (SELECT CodigoPedido, MAX(ComprobanteFecha) AS ComprobanteFecha
      FROM Salidas WHERE ComprobanteFecha IS NOT NULL GROUP BY CodigoPedido) x ON x.CodigoPedido = p.Codigo;");

            // c) Pago rechazado (volvió a "Esperando pago" con motivo).
            migrationBuilder.Sql(@"
INSERT INTO HistorialPedidos (PedidoId, EstadoId, Fecha, UsuarioId, Comentario)
SELECT p.Id, 1, DATEADD(MINUTE, 1, COALESCE(x.ComprobanteFecha, p.Fecha)), NULL, x.MotivoRechazo
FROM Pedidos p
JOIN (SELECT CodigoPedido, MAX(ComprobanteFecha) AS ComprobanteFecha, MAX(MotivoRechazo) AS MotivoRechazo
      FROM Salidas GROUP BY CodigoPedido) x ON x.CodigoPedido = p.Codigo
WHERE p.EstadoId = 1 AND x.MotivoRechazo IS NOT NULL;");

            // d) Pago confirmado / entregado / cancelado (estado actual).
            migrationBuilder.Sql(@"
INSERT INTO HistorialPedidos (PedidoId, EstadoId, Fecha, UsuarioId, Comentario)
SELECT p.Id, 3, DATEADD(MINUTE, 2, COALESCE(x.ComprobanteFecha, p.Fecha)), NULL, NULL
FROM Pedidos p
JOIN Usuarios u ON u.Id = p.UsuarioId
JOIN (SELECT CodigoPedido, MAX(ComprobanteFecha) AS ComprobanteFecha FROM Salidas GROUP BY CodigoPedido) x ON x.CodigoPedido = p.Codigo
WHERE p.EstadoId IN (3, 4) AND NOT (p.EstadoId = 4 AND u.RolId = 1);");

            migrationBuilder.Sql(@"
INSERT INTO HistorialPedidos (PedidoId, EstadoId, Fecha, UsuarioId, Comentario)
SELECT p.Id, p.EstadoId, DATEADD(MINUTE, 3, COALESCE(x.ComprobanteFecha, p.Fecha)), NULL, NULL
FROM Pedidos p
JOIN Usuarios u ON u.Id = p.UsuarioId
JOIN (SELECT CodigoPedido, MAX(ComprobanteFecha) AS ComprobanteFecha FROM Salidas GROUP BY CodigoPedido) x ON x.CodigoPedido = p.Codigo
WHERE (p.EstadoId = 4 AND u.RolId <> 1) OR p.EstadoId = 5;");

            // =========================================================
            // 7) Compras (cabecera por factura) + DetallesCompra ← Ingresos
            //    Los ingresos con el mismo proveedor y número de comprobante forman una compra;
            //    los que no tienen número quedan como una compra cada uno.
            // =========================================================
            migrationBuilder.CreateTable(
                name: "Compras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    ClaveMigracion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compras_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compras_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompraId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesCompra", x => x.Id);
                    table.CheckConstraint("CK_DetallesCompra_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallesCompra_Costo", "[CostoUnitario] >= 0");
                    table.ForeignKey(
                        name: "FK_DetallesCompra_Compras_CompraId",
                        column: x => x.CompraId,
                        principalTable: "Compras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesCompra_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
INSERT INTO Compras (Fecha, NumeroComprobante, Observaciones, ProveedorId, UsuarioId, ClaveMigracion)
SELECT MIN(i.Fecha),
       NULLIF(LTRIM(RTRIM(MAX(i.NumeroComprobante))), ''),
       MAX(i.Observaciones),
       i.ProveedorId,
       MIN(i.UsuarioId),
       k.Clave
FROM Ingresos i
CROSS APPLY (SELECT CASE WHEN LTRIM(RTRIM(ISNULL(i.NumeroComprobante, ''))) = ''
                         THEN CONCAT('#', i.Id)
                         ELSE CONCAT(i.ProveedorId, '|', LTRIM(RTRIM(i.NumeroComprobante))) END AS Clave) k
GROUP BY i.ProveedorId, k.Clave;");

            migrationBuilder.Sql(@"
INSERT INTO DetallesCompra (CompraId, ProductoId, Cantidad, CostoUnitario)
SELECT c.Id, i.ProductoId, SUM(i.Cantidad), MAX(i.CostoUnitario)
FROM Ingresos i
CROSS APPLY (SELECT CASE WHEN LTRIM(RTRIM(ISNULL(i.NumeroComprobante, ''))) = ''
                         THEN CONCAT('#', i.Id)
                         ELSE CONCAT(i.ProveedorId, '|', LTRIM(RTRIM(i.NumeroComprobante))) END AS Clave) k
JOIN Compras c ON c.ClaveMigracion = k.Clave
GROUP BY c.Id, i.ProductoId;");

            migrationBuilder.DropColumn(
                name: "ClaveMigracion",
                table: "Compras");

            // =========================================================
            // 8) Se eliminan las tablas viejas (los datos ya están en las nuevas)
            // =========================================================
            migrationBuilder.DropTable(name: "Salidas");
            migrationBuilder.DropTable(name: "Ingresos");

            // =========================================================
            // 9) Índices de las tablas nuevas
            // =========================================================
            migrationBuilder.CreateIndex(
                name: "IX_Compras_Fecha",
                table: "Compras",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_ProveedorId_NumeroComprobante",
                table: "Compras",
                columns: new[] { "ProveedorId", "NumeroComprobante" },
                unique: true,
                filter: "[NumeroComprobante] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_UsuarioId",
                table: "Compras",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCompra_CompraId_ProductoId",
                table: "DetallesCompra",
                columns: new[] { "CompraId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCompra_ProductoId",
                table: "DetallesCompra",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Codigo",
                table: "Pedidos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_EstadoId",
                table: "Pedidos",
                column: "EstadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Fecha",
                table: "Pedidos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_UsuarioId",
                table: "Pedidos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_PedidoId_ProductoId",
                table: "DetallesPedido",
                columns: new[] { "PedidoId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_ProductoId",
                table: "DetallesPedido",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPedidos_EstadoId",
                table: "HistorialPedidos",
                column: "EstadoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPedidos_PedidoId_Fecha",
                table: "HistorialPedidos",
                columns: new[] { "PedidoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPedidos_UsuarioId",
                table: "HistorialPedidos",
                column: "UsuarioId");
        }

        /// <summary>Vuelve al esquema de 7 tablas, también conservando los datos.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---- Ingresos ← Compras + DetallesCompra ----
            migrationBuilder.CreateTable(
                name: "Ingresos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingresos", x => x.Id);
                    table.CheckConstraint("CK_Ingresos_Cantidad", "[Cantidad] > 0");
                    table.ForeignKey("FK_Ingresos_Productos_ProductoId", x => x.ProductoId, "Productos", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Ingresos_Proveedores_ProveedorId", x => x.ProveedorId, "Proveedores", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Ingresos_Usuarios_UsuarioId", x => x.UsuarioId, "Usuarios", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
INSERT INTO Ingresos (Fecha, Cantidad, CostoUnitario, Total, NumeroComprobante, Observaciones, ProductoId, ProveedorId, UsuarioId)
SELECT c.Fecha, d.Cantidad, d.CostoUnitario, d.Cantidad * d.CostoUnitario, c.NumeroComprobante, c.Observaciones, d.ProductoId, c.ProveedorId, c.UsuarioId
FROM DetallesCompra d JOIN Compras c ON c.Id = d.CompraId;");

            // ---- Salidas ← Pedidos + DetallesPedido + HistorialPedidos ----
            migrationBuilder.CreateTable(
                name: "Salidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoPedido = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    ComprobanteArchivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ComprobanteFecha = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salidas", x => x.Id);
                    table.CheckConstraint("CK_Salidas_Cantidad", "[Cantidad] > 0");
                    table.ForeignKey("FK_Salidas_Clientes_ClienteId", x => x.ClienteId, "Clientes", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Salidas_Productos_ProductoId", x => x.ProductoId, "Productos", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Salidas_Usuarios_UsuarioId", x => x.UsuarioId, "Usuarios", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
INSERT INTO Salidas (CodigoPedido, Estado, Fecha, Cantidad, PrecioUnitario, Total, Observaciones, ProductoId, ClienteId, UsuarioId,
                     ComprobanteArchivo, ComprobanteFecha, MotivoRechazo)
SELECT p.Codigo, e.Codigo, p.Fecha, d.Cantidad, d.PrecioUnitario, d.Cantidad * d.PrecioUnitario, p.Observaciones, d.ProductoId,
       p.ClienteId, p.UsuarioId, p.ComprobanteArchivo,
       (SELECT MAX(h.Fecha) FROM HistorialPedidos h WHERE h.PedidoId = p.Id AND h.EstadoId = 2),
       CASE WHEN p.EstadoId = 1 THEN (SELECT TOP 1 h.Comentario FROM HistorialPedidos h WHERE h.PedidoId = p.Id ORDER BY h.Fecha DESC, h.Id DESC) END
FROM DetallesPedido d
JOIN Pedidos p ON p.Id = d.PedidoId
JOIN EstadosPedido e ON e.Id = p.EstadoId;");

            migrationBuilder.DropTable(name: "DetallesCompra");
            migrationBuilder.DropTable(name: "Compras");
            migrationBuilder.DropTable(name: "DetallesPedido");
            migrationBuilder.DropTable(name: "HistorialPedidos");
            migrationBuilder.DropTable(name: "Pedidos");
            migrationBuilder.DropTable(name: "EstadosPedido");

            migrationBuilder.CreateIndex(name: "IX_Ingresos_Fecha", table: "Ingresos", column: "Fecha");
            migrationBuilder.CreateIndex(name: "IX_Ingresos_ProductoId", table: "Ingresos", column: "ProductoId");
            migrationBuilder.CreateIndex(name: "IX_Ingresos_ProveedorId", table: "Ingresos", column: "ProveedorId");
            migrationBuilder.CreateIndex(name: "IX_Ingresos_UsuarioId", table: "Ingresos", column: "UsuarioId");
            migrationBuilder.CreateIndex(name: "IX_Salidas_ClienteId", table: "Salidas", column: "ClienteId");
            migrationBuilder.CreateIndex(name: "IX_Salidas_CodigoPedido", table: "Salidas", column: "CodigoPedido");
            migrationBuilder.CreateIndex(name: "IX_Salidas_Estado", table: "Salidas", column: "Estado");
            migrationBuilder.CreateIndex(name: "IX_Salidas_Fecha", table: "Salidas", column: "Fecha");
            migrationBuilder.CreateIndex(name: "IX_Salidas_ProductoId", table: "Salidas", column: "ProductoId");
            migrationBuilder.CreateIndex(name: "IX_Salidas_UsuarioId", table: "Salidas", column: "UsuarioId");

            // ---- Productos.Marca ← Marcas ----
            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "Productos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE p SET p.Marca = m.Nombre FROM Productos p JOIN Marcas m ON m.Id = p.MarcaId;");

            migrationBuilder.DropForeignKey(name: "FK_Productos_Marcas_MarcaId", table: "Productos");
            migrationBuilder.DropIndex(name: "IX_Productos_MarcaId", table: "Productos");
            migrationBuilder.DropColumn(name: "MarcaId", table: "Productos");
            migrationBuilder.DropTable(name: "Marcas");

            // ---- Usuarios.Rol ← Roles ----
            migrationBuilder.AddColumn<string>(
                name: "Rol",
                table: "Usuarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Comprador");

            migrationBuilder.Sql("UPDATE u SET u.Rol = r.Nombre FROM Usuarios u JOIN Roles r ON r.Id = u.RolId;");

            migrationBuilder.DropForeignKey(name: "FK_Usuarios_Roles_RolId", table: "Usuarios");
            migrationBuilder.DropIndex(name: "IX_Usuarios_RolId", table: "Usuarios");
            migrationBuilder.DropColumn(name: "RolId", table: "Usuarios");
            migrationBuilder.DropTable(name: "Roles");
        }
    }
}
