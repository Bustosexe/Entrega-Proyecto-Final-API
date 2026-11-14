using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Models;

namespace TechHub.Api.Data;

/// <summary>
/// Datos de demostración para la presentación: proveedores, un catálogo de 17 componentes con foto,
/// sus marcas, las compras iniciales a proveedores (Compras + DetallesCompra) y un comprador de prueba
/// con pedidos en distintos estados (Pedidos + DetallesPedido + HistorialPedidos).
/// Es idempotente: solo agrega los productos que todavía no existen (por nombre o por imagen).
/// Se activa con "Seed:DatosDemo": true en appsettings.json.
/// Los precios son orientativos (ARS) y las fotos de producto están en wwwroot/demo.
/// </summary>
public static class DemoSeeder
{
    private sealed record Demo(string Nombre, string Marca, string Categoria, decimal Precio, decimal Costo,
        int Stock, int StockMinimo, string Imagen, string Descripcion);

    private static readonly Demo[] Catalogo =
    [
        new("AMD Ryzen 5 7600", "AMD", "Procesadores", 320000, 250000, 12, 3, "ryzen5-7600",
            "6 núcleos / 12 hilos, hasta 5,1 GHz, socket AM5. Incluye cooler Wraith Stealth. Ideal para gaming 1080p/1440p."),
        new("AMD Ryzen 7 7800X3D", "AMD", "Procesadores", 690000, 540000, 4, 2, "ryzen7-7800x3d",
            "8 núcleos / 16 hilos con 3D V-Cache (96 MB L3), socket AM5. Uno de los mejores procesadores para gaming."),
        new("Intel Core i5-14400F", "Intel", "Procesadores", 290000, 225000, 10, 3, "core-i5-14400f",
            "10 núcleos (6P + 4E) / 16 hilos, hasta 4,7 GHz, socket LGA1700. Requiere placa de video dedicada."),
        new("Intel Core i7-14700K", "Intel", "Procesadores", 620000, 485000, 2, 2, "core-i7-14700k",
            "20 núcleos (8P + 12E) / 28 hilos, hasta 5,6 GHz, desbloqueado para overclock, socket LGA1700."),

        new("GeForce RTX 4060 8GB Ventus 2X Black OC", "MSI", "Tarjetas Gráficas", 480000, 375000, 8, 2, "rtx4060",
            "8 GB GDDR6, DLSS 3 y Ray Tracing. Doble ventilador y bajo consumo (115 W)."),
        new("GeForce RTX 4070 SUPER 12GB Gaming X Slim", "MSI", "Tarjetas Gráficas", 980000, 770000, 3, 2, "rtx4070super",
            "12 GB GDDR6X, DLSS 3 y Ray Tracing. Triple ventilador en diseño delgado. Excelente para 1440p en ultra."),
        new("GeForce RTX 5070 Ti 16GB TUF Gaming OC", "ASUS", "Tarjetas Gráficas", 1450000, 1130000, 3, 1, "rtx5070ti",
            "16 GB GDDR7, PCIe 5.0, DLSS 4 y Ray Tracing. Triple ventilador y construcción reforzada TUF. Para 1440p y 4K."),
        new("Radeon RX 7800 XT 16GB Phantom Gaming OC", "ASRock", "Tarjetas Gráficas", 850000, 665000, 0, 2, "rx7800xt",
            "16 GB GDDR6, FSR 3. Gran rendimiento en 1440p. (Sin stock: muestra el estado \"Sin stock\" en el catálogo)."),

        new("B650M Gaming Plus WiFi", "Gigabyte", "Motherboards", 290000, 225000, 6, 2, "b650-gaming",
            "Socket AM5, chipset B650, DDR5, PCIe 4.0, 2 x M.2, WiFi 6E y Bluetooth. Formato Micro-ATX."),
        new("TUF Gaming B760-Plus WiFi", "ASUS", "Motherboards", 260000, 200000, 5, 2, "b760-tuf",
            "Socket LGA1700, chipset B760, DDR5, PCIe 4.0, 3 x M.2, WiFi 6. Formato ATX."),

        new("Fury Beast DDR5 16GB (2x8) 6000MHz", "Kingston", "Memorias RAM", 95000, 72000, 20, 5, "fury-ddr5-16",
            "Kit dual channel 2 x 8 GB DDR5 6000 MHz CL36, perfiles Intel XMP 3.0 y AMD EXPO."),
        new("Vengeance RGB DDR5 32GB (2x16) 6000MHz Blanca", "Corsair", "Memorias RAM", 165000, 128000, 14, 4, "vengeance-ddr5-32",
            "Kit dual channel 2 x 16 GB DDR5 6000 MHz CL30, disipador blanco con iluminación RGB, XMP 3.0."),

        new("SSD 990 PRO 1TB NVMe M.2", "Samsung", "Almacenamiento M.2", 165000, 128000, 9, 3, "990pro-1tb",
            "NVMe PCIe 4.0, hasta 7.450 MB/s de lectura. Formato M.2 2280."),
        new("SSD NV2 1TB NVMe M.2", "Kingston", "Almacenamiento M.2", 85000, 64000, 25, 5, "nv2-1tb",
            "NVMe PCIe 4.0, hasta 3.500 MB/s de lectura. La opción económica para tu sistema."),
        new("SSD Black SN850X 2TB NVMe M.2", "WD", "Almacenamiento M.2", 260000, 205000, 1, 2, "sn850x-2tb",
            "NVMe PCIe 4.0, hasta 7.300 MB/s de lectura. Ideal para juegos y edición de video."),

        new("Fuente RM750e 750W 80+ Gold Modular", "Corsair", "Fuentes de Poder", 155000, 120000, 7, 2, "rm750e",
            "750 W certificada 80 PLUS Gold, totalmente modular, ventilador de 120 mm de bajo ruido, ATX 3.0."),
        new("Fuente 600 W1 600W 80+", "EVGA", "Fuentes de Poder", 75000, 56000, 11, 3, "w1-600",
            "600 W certificada 80 PLUS White. Para equipos de oficina y gaming de entrada.")
    ];

    public static async Task CargarAsync(AppDbContext db, IPasswordHasher<Usuario> hasher, ILogger logger)
    {
        var admin = await db.Usuarios.Where(u => u.RolId == Roles.AdminId).OrderBy(u => u.Id).FirstOrDefaultAsync();
        if (admin is null) return;

        // Se considera que un producto de demo ya existe si coincide el nombre o la imagen
        // (así, si se renombra un producto desde la app, no se vuelve a crear duplicado).
        var actuales = await db.Productos.Select(p => new { p.Nombre, p.ImagenUrl }).ToListAsync();
        var nombres = actuales.Select(p => p.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imagenes = actuales.Where(p => p.ImagenUrl != null).Select(p => p.ImagenUrl!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendientes = Catalogo
            .Where(d => !nombres.Contains(d.Nombre) && !imagenes.Contains($"/demo/{d.Imagen}.png"))
            .ToList();
        if (pendientes.Count == 0) return;

        // ---------- Proveedores ----------
        var proveedores = new List<Proveedor>
        {
            await ObtenerProveedorAsync(db, "30-71234567-8", "Distribuidora Mayorista Demo SA",
                "ventas@mayoristademo.com.ar", "011 4000-1234", "Av. Corrientes 1234, CABA"),
            await ObtenerProveedorAsync(db, "30-70987654-3", "Importadora Hardware Demo SRL",
                "pedidos@importadorademo.com.ar", "011 4555-9876", "Av. Rivadavia 5678, CABA")
        };

        // ---------- Marcas (se crean las que falten) ----------
        var marcas = await db.Marcas.ToDictionaryAsync(m => m.Nombre, StringComparer.OrdinalIgnoreCase);
        foreach (var nombreMarca in pendientes.Select(d => d.Marca).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (marcas.ContainsKey(nombreMarca)) continue;
            var marca = new Marca { Nombre = nombreMarca };
            db.Marcas.Add(marca);
            marcas[nombreMarca] = marca;
        }

        // ---------- Productos + compras iniciales (el stock entra por una Compra: trazabilidad) ----------
        var categorias = await db.Categorias.ToDictionaryAsync(c => c.Nombre, StringComparer.OrdinalIgnoreCase);
        var creados = new List<Producto>();
        var fechaCompra = DateTime.UtcNow.AddDays(-10);

        // Una factura por proveedor: procesadores, motherboards y fuentes a uno; el resto al otro.
        var compras = proveedores.Select((prov, k) => new Compra
        {
            Proveedor = prov,
            Usuario = admin,
            Fecha = fechaCompra.AddMinutes(k * 30),
            NumeroComprobante = $"A-0001-{Random.Shared.Next(10_000_000, 99_999_999)}",
            Observaciones = "Carga inicial (datos de demostración)"
        }).ToList();

        foreach (var d in pendientes)
        {
            if (!categorias.TryGetValue(d.Categoria, out var categoria)) continue;

            var producto = new Producto
            {
                Nombre = d.Nombre,
                Marca = marcas[d.Marca],
                Descripcion = d.Descripcion,
                Precio = d.Precio,
                Stock = d.Stock,
                StockMinimo = d.StockMinimo,
                ImagenUrl = $"/demo/{d.Imagen}.png",
                Categoria = categoria,
                FechaCreacion = fechaCompra
            };
            db.Productos.Add(producto);
            creados.Add(producto);

            if (d.Stock > 0)
            {
                var compra = d.Categoria is "Procesadores" or "Motherboards" or "Fuentes de Poder" ? compras[0] : compras[1];
                compra.Detalles.Add(new DetalleCompra { Producto = producto, Cantidad = d.Stock, CostoUnitario = d.Costo });
            }
        }

        foreach (var compra in compras.Where(c => c.Detalles.Count > 0))
            db.Compras.Add(compra);

        // ---------- Comprador de prueba con pedidos (para "Mis pedidos" y el panel) ----------
        const string emailDemo = "comprador@techhub.local";
        const string documentoDemo = "35123456";
        if (creados.Count > 0
            && !await db.Usuarios.AnyAsync(u => u.Email == emailDemo || u.NombreUsuario == emailDemo)
            && !await db.Clientes.AnyAsync(c => c.Documento == documentoDemo))
        {
            var cliente = new Cliente
            {
                Nombre = "Lucía Fernández",
                Documento = documentoDemo,
                Email = emailDemo,
                Telefono = "11 5555-1234",
                Direccion = "Av. Santa Fe 3200, CABA"
            };
            var comprador = new Usuario
            {
                NombreUsuario = emailDemo,
                NombreCompleto = cliente.Nombre,
                Email = emailDemo,
                RolId = Roles.CompradorId,
                Cliente = cliente
            };
            comprador.PasswordHash = hasher.HashPassword(comprador, "Comprador123!");
            db.Usuarios.Add(comprador);

            Producto? P(string nombre) => creados.FirstOrDefault(p => p.Nombre == nombre);

            AgregarPedido(db, "P-DEMO-0001", EstadosPedido.Entregado, DateTime.UtcNow.AddDays(-6), cliente, comprador, admin,
                "Retiro por el local", (P("AMD Ryzen 5 7600"), 1), (P("B650M Gaming Plus WiFi"), 1), (P("Fury Beast DDR5 16GB (2x8) 6000MHz"), 1));
            AgregarPedido(db, "P-DEMO-0002", EstadosPedido.Confirmado, DateTime.UtcNow.AddDays(-1), cliente, comprador, admin,
                "Envío a domicilio", (P("GeForce RTX 4060 8GB Ventus 2X Black OC"), 1));
            AgregarPedido(db, "P-DEMO-0003", EstadosPedido.Pendiente, DateTime.UtcNow.AddHours(-2), cliente, comprador, admin,
                null, (P("SSD NV2 1TB NVMe M.2"), 2), (P("Fuente 600 W1 600W 80+"), 1));
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Datos de demostración cargados: {Cantidad} productos.", creados.Count);
    }

    private static async Task<Proveedor> ObtenerProveedorAsync(AppDbContext db, string cuit, string razonSocial,
        string email, string telefono, string direccion)
    {
        var p = await db.Proveedores.FirstOrDefaultAsync(x => x.Cuit == cuit);
        if (p is not null) return p;
        p = new Proveedor { Cuit = cuit, RazonSocial = razonSocial, Email = email, Telefono = telefono, Direccion = direccion };
        db.Proveedores.Add(p);
        return p;
    }

    /// <summary>
    /// Crea un pedido de demo (cabecera + renglones) con su historial de estados y descuenta el stock
    /// (solo con productos recién creados).
    /// </summary>
    private static void AgregarPedido(AppDbContext db, string codigo, string estadoFinal, DateTime fecha, Cliente cliente,
        Usuario comprador, Usuario admin, string? observaciones, params (Producto? Producto, int Cantidad)[] lineas)
    {
        var pedido = new Pedido
        {
            Codigo = codigo,
            Fecha = fecha,
            Cliente = cliente,
            Usuario = comprador,
            EstadoId = EstadosPedido.Id(estadoFinal),
            Observaciones = observaciones
        };

        foreach (var (producto, cantidad) in lineas)
        {
            if (producto is null || producto.Stock < cantidad) continue;
            producto.Stock -= cantidad;
            pedido.Detalles.Add(new DetallePedido { Producto = producto, Cantidad = cantidad, PrecioUnitario = producto.Precio });
        }
        if (pedido.Detalles.Count == 0) return;

        // Historial coherente con el estado final: así la línea de tiempo muestra fechas reales.
        void Paso(int estadoId, DateTime cuando, Usuario quien, string? comentario = null) =>
            pedido.Historial.Add(new HistorialPedido { EstadoId = estadoId, Fecha = cuando, Usuario = quien, Comentario = comentario });

        Paso(EstadosPedido.PendienteId, fecha, comprador);
        if (estadoFinal is EstadosPedido.Confirmado or EstadosPedido.Entregado)
        {
            Paso(EstadosPedido.EnRevisionId, fecha.AddHours(1), comprador);
            Paso(EstadosPedido.ConfirmadoId, fecha.AddHours(3), admin, "Transferencia acreditada");
        }
        if (estadoFinal == EstadosPedido.Entregado)
            Paso(EstadosPedido.EntregadoId, fecha.AddDays(1), admin, "Retirado por el local");

        db.Pedidos.Add(pedido);
    }
}
