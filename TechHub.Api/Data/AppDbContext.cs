using Microsoft.EntityFrameworkCore;
using TechHub.Api.Models;
// Alias: dentro del contexto, "Roles" y "EstadosPedido" son los DbSet; estos nombres apuntan a los valores fijos.
using RolesFijos = TechHub.Api.Models.Roles;
using Estados = TechHub.Api.Models.EstadosPedido;

namespace TechHub.Api.Data;

/// <summary>
/// Modelo de datos (Code-First) normalizado hasta la 3ª forma normal: 18 tablas.
///   Catálogo:       Categorias, Marcas, Productos
///   Terceros:       Proveedores, Clientes
///   Seguridad:      Usuarios, Roles
///   Compras:        Compras (cabecera) + DetallesCompra (renglones)
///   Ventas:         Pedidos (cabecera) + DetallesPedido (renglones) + HistorialPedidos (cambios de estado)
///   Referencia:     EstadosPedido
///   Promociones:    Ofertas (descuento por producto), Combos + ComboItems, CategoriasCompatibles (qué categorías pueden ir juntas)
///   Auditoría:      Auditoria (bitácora de operaciones)
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<DetalleCompra> DetallesCompra => Set<DetalleCompra>();
    public DbSet<EstadoPedido> EstadosPedido => Set<EstadoPedido>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<HistorialPedido> HistorialPedidos => Set<HistorialPedido>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();
    public DbSet<CategoriaCompatible> CategoriasCompatibles => Set<CategoriaCompatible>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Categorías ----------
        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("Categorias");
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(300);
            e.HasIndex(x => x.Nombre).IsUnique();

            e.HasData(
                new Categoria { Id = 1, Nombre = "Procesadores", Descripcion = "CPUs Intel y AMD" },
                new Categoria { Id = 2, Nombre = "Tarjetas Gráficas", Descripcion = "GPUs Nvidia, AMD e Intel" },
                new Categoria { Id = 3, Nombre = "Motherboards", Descripcion = "Placas madre" },
                new Categoria { Id = 4, Nombre = "Memorias RAM", Descripcion = "Módulos DDR4 / DDR5" },
                new Categoria { Id = 5, Nombre = "Almacenamiento M.2", Descripcion = "SSD NVMe y SATA" },
                new Categoria { Id = 6, Nombre = "Fuentes de Poder", Descripcion = "PSU certificadas" });
        });

        // ---------- Marcas ----------
        modelBuilder.Entity<Marca>(e =>
        {
            e.ToTable("Marcas");
            e.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        // ---------- Productos ----------
        modelBuilder.Entity<Producto>(e =>
        {
            e.ToTable("Productos", t =>
            {
                // La base de datos nunca acepta stock negativo, aunque falle la lógica de la app.
                t.HasCheckConstraint("CK_Productos_Stock", "[Stock] >= 0");
                t.HasCheckConstraint("CK_Productos_Precio", "[Precio] >= 0");
            });
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.Precio).HasPrecision(18, 2);
            e.Property(x => x.ImagenUrl).HasMaxLength(300);
            e.HasIndex(x => x.Nombre);

            e.HasOne(x => x.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(x => x.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Marca)
             .WithMany(m => m.Productos)
             .HasForeignKey(x => x.MarcaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Proveedores ----------
        modelBuilder.Entity<Proveedor>(e =>
        {
            e.ToTable("Proveedores");
            e.Property(x => x.RazonSocial).HasMaxLength(150).IsRequired();
            e.Property(x => x.Cuit).HasMaxLength(13).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.HasIndex(x => x.Cuit).IsUnique();
        });

        // ---------- Clientes ----------
        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes");
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Documento).HasMaxLength(13).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Telefono).HasMaxLength(30);
            e.Property(x => x.Direccion).HasMaxLength(200);
            e.HasIndex(x => x.Documento).IsUnique();
        });

        // ---------- Roles (tabla de referencia con valores fijos) ----------
        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("Roles");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(20).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(150);
            e.HasIndex(x => x.Nombre).IsUnique();

            e.HasData(
                new Rol { Id = RolesFijos.AdminId, Nombre = RolesFijos.Admin, Descripcion = "Personal de la tienda: gestiona catálogo, stock, pedidos y reportes" },
                new Rol { Id = RolesFijos.CompradorId, Nombre = RolesFijos.Comprador, Descripcion = "Cliente registrado desde la app: compra y sigue sus pedidos" });
        });

        // ---------- Usuarios ----------
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.Property(x => x.NombreUsuario).HasMaxLength(150).IsRequired();
            e.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.NombreUsuario).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Rol)
             .WithMany(r => r.Usuarios)
             .HasForeignKey(x => x.RolId)
             .OnDelete(DeleteBehavior.Restrict);

            // 1 a 1: un comprador tiene una ficha de cliente (índice único filtrado por NOT NULL).
            e.HasOne(x => x.Cliente)
             .WithOne(c => c.Usuario)
             .HasForeignKey<Usuario>(x => x.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Compras (cabecera) ----------
        modelBuilder.Entity<Compra>(e =>
        {
            e.ToTable("Compras");
            e.Property(x => x.NumeroComprobante).HasMaxLength(50);
            e.Property(x => x.Observaciones).HasMaxLength(500);
            e.HasIndex(x => x.Fecha);
            // Una misma factura de un proveedor no se puede cargar dos veces.
            e.HasIndex(x => new { x.ProveedorId, x.NumeroComprobante })
             .IsUnique()
             .HasFilter("[NumeroComprobante] IS NOT NULL");

            e.HasOne(x => x.Proveedor).WithMany(p => p.Compras)
             .HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Usuario).WithMany(u => u.Compras)
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- DetallesCompra (renglones) ----------
        modelBuilder.Entity<DetalleCompra>(e =>
        {
            e.ToTable("DetallesCompra", t =>
            {
                t.HasCheckConstraint("CK_DetallesCompra_Cantidad", "[Cantidad] > 0");
                t.HasCheckConstraint("CK_DetallesCompra_Costo", "[CostoUnitario] >= 0");
            });
            e.Property(x => x.CostoUnitario).HasPrecision(18, 2);
            // Un producto aparece una sola vez por compra (si se repite, se suman las cantidades).
            e.HasIndex(x => new { x.CompraId, x.ProductoId }).IsUnique();

            e.HasOne(x => x.Compra).WithMany(c => c.Detalles)
             .HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany(p => p.DetallesCompra)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- EstadosPedido (tabla de referencia con valores fijos) ----------
        modelBuilder.Entity<EstadoPedido>(e =>
        {
            e.ToTable("EstadosPedido");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();

            e.HasData(
                new EstadoPedido { Id = Estados.PendienteId, Codigo = Estados.Pendiente, Nombre = "Esperando pago" },
                new EstadoPedido { Id = Estados.EnRevisionId, Codigo = Estados.EnRevision, Nombre = "Pago en revisión" },
                new EstadoPedido { Id = Estados.ConfirmadoId, Codigo = Estados.Confirmado, Nombre = "Pago confirmado" },
                new EstadoPedido { Id = Estados.EntregadoId, Codigo = Estados.Entregado, Nombre = "Entregado" },
                new EstadoPedido { Id = Estados.CanceladoId, Codigo = Estados.Cancelado, Nombre = "Cancelado" });
        });

        // ---------- Pedidos (cabecera) ----------
        modelBuilder.Entity<Pedido>(e =>
        {
            e.ToTable("Pedidos");
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Observaciones).HasMaxLength(500);
            e.Property(x => x.ComprobanteArchivo).HasMaxLength(200);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => x.Fecha);

            e.HasOne(x => x.Cliente).WithMany(c => c.Pedidos)
             .HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Usuario).WithMany(u => u.Pedidos)
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Estado).WithMany(s => s.Pedidos)
             .HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- DetallesPedido (renglones) ----------
        modelBuilder.Entity<DetallePedido>(e =>
        {
            e.ToTable("DetallesPedido", t =>
            {
                t.HasCheckConstraint("CK_DetallesPedido_Cantidad", "[Cantidad] > 0");
                t.HasCheckConstraint("CK_DetallesPedido_Precio", "[PrecioUnitario] >= 0");
            });
            e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
            e.HasIndex(x => new { x.PedidoId, x.ProductoId }).IsUnique();

            e.HasOne(x => x.Pedido).WithMany(p => p.Detalles)
             .HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany(p => p.DetallesPedido)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- HistorialPedidos (auditoría de cambios de estado) ----------
        modelBuilder.Entity<HistorialPedido>(e =>
        {
            e.ToTable("HistorialPedidos");
            e.Property(x => x.Comentario).HasMaxLength(300);
            e.HasIndex(x => new { x.PedidoId, x.Fecha });

            e.HasOne(x => x.Pedido).WithMany(p => p.Historial)
             .HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Estado).WithMany(s => s.Historial)
             .HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Usuario).WithMany(u => u.CambiosDeEstado)
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
        // ---------- Ofertas (descuento temporal por producto) ----------
        modelBuilder.Entity<Oferta>(e =>
        {
            e.ToTable("Ofertas", t =>
            {
                t.HasCheckConstraint("CK_Ofertas_Porcentaje", "[Porcentaje] BETWEEN 1 AND 90");
                t.HasCheckConstraint("CK_Ofertas_Vigencia", "[Hasta] >= [Desde]");
            });
            e.Property(x => x.Titulo).HasMaxLength(80);
            e.HasIndex(x => new { x.ProductoId, x.Hasta });

            e.HasOne(x => x.Producto).WithMany(p => p.Ofertas)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Combos + ComboItems ----------
        modelBuilder.Entity<Combo>(e =>
        {
            e.ToTable("Combos", t => t.HasCheckConstraint("CK_Combos_Porcentaje", "[Porcentaje] BETWEEN 1 AND 50"));
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        modelBuilder.Entity<ComboItem>(e =>
        {
            e.ToTable("ComboItems", t => t.HasCheckConstraint("CK_ComboItems_Cantidad", "[Cantidad] BETWEEN 1 AND 20"));
            e.HasIndex(x => new { x.ComboId, x.ProductoId }).IsUnique();

            e.HasOne(x => x.Combo).WithMany(c => c.Items)
             .HasForeignKey(x => x.ComboId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Producto).WithMany(p => p.EnCombos)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- CategoriasCompatibles (qué categorías pueden formar un combo) ----------
        modelBuilder.Entity<CategoriaCompatible>(e =>
        {
            e.ToTable("CategoriasCompatibles", t => t.HasCheckConstraint("CK_CategoriasCompatibles_Orden", "[CategoriaAId] < [CategoriaBId]"));
            e.HasKey(x => new { x.CategoriaAId, x.CategoriaBId });

            e.HasOne(x => x.CategoriaA).WithMany()
             .HasForeignKey(x => x.CategoriaAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CategoriaB).WithMany()
             .HasForeignKey(x => x.CategoriaBId).OnDelete(DeleteBehavior.Restrict);

            // Categorías: 1 Procesadores, 2 Tarjetas Gráficas, 3 Motherboards, 4 Memorias RAM, 5 Almacenamiento M.2, 6 Fuentes de Poder.
            // Una fuente de poder solo combina con placa de video y motherboard; la RAM nunca con la fuente.
            e.HasData(
                new CategoriaCompatible { CategoriaAId = 1, CategoriaBId = 2 },
                new CategoriaCompatible { CategoriaAId = 1, CategoriaBId = 3 },
                new CategoriaCompatible { CategoriaAId = 1, CategoriaBId = 4 },
                new CategoriaCompatible { CategoriaAId = 1, CategoriaBId = 5 },
                new CategoriaCompatible { CategoriaAId = 2, CategoriaBId = 3 },
                new CategoriaCompatible { CategoriaAId = 2, CategoriaBId = 6 },
                new CategoriaCompatible { CategoriaAId = 3, CategoriaBId = 4 },
                new CategoriaCompatible { CategoriaAId = 3, CategoriaBId = 5 },
                new CategoriaCompatible { CategoriaAId = 3, CategoriaBId = 6 },
                new CategoriaCompatible { CategoriaAId = 4, CategoriaBId = 5 });
        });

        // ---------- Auditoria (bitácora) ----------
        modelBuilder.Entity<RegistroAuditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.Property(x => x.Accion).HasMaxLength(60).IsRequired();
            e.Property(x => x.Entidad).HasMaxLength(40).IsRequired();
            e.Property(x => x.EntidadId).HasMaxLength(50);
            e.Property(x => x.Detalle).HasMaxLength(1000);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => new { x.Entidad, x.EntidadId });

            e.HasOne(x => x.Usuario).WithMany()
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
