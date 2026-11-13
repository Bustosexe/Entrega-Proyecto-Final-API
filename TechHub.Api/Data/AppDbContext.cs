using Microsoft.EntityFrameworkCore;
using TechHub.Api.Models;

namespace TechHub.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Ingreso> Ingresos => Set<Ingreso>();
    public DbSet<Salida> Salidas => Set<Salida>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

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
            e.Property(x => x.Marca).HasMaxLength(80).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.Precio).HasPrecision(18, 2);
            e.Property(x => x.ImagenUrl).HasMaxLength(300);
            e.HasIndex(x => x.Nombre);

            e.HasOne(x => x.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(x => x.CategoriaId)
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

        // ---------- Usuarios ----------
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.Property(x => x.NombreUsuario).HasMaxLength(150).IsRequired();
            e.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Rol).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.NombreUsuario).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();

            // 1 a 1: un comprador tiene una ficha de cliente (índice único filtrado por NOT NULL).
            e.HasOne(x => x.Cliente)
             .WithOne(c => c.Usuario)
             .HasForeignKey<Usuario>(x => x.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Ingresos (compras) ----------
        modelBuilder.Entity<Ingreso>(e =>
        {
            e.ToTable("Ingresos", t => t.HasCheckConstraint("CK_Ingresos_Cantidad", "[Cantidad] > 0"));
            e.Property(x => x.CostoUnitario).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.NumeroComprobante).HasMaxLength(50);
            e.Property(x => x.Observaciones).HasMaxLength(500);
            e.HasIndex(x => x.Fecha);

            e.HasOne(x => x.Producto).WithMany(p => p.Ingresos)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Proveedor).WithMany(p => p.Ingresos)
             .HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Usuario).WithMany(u => u.Ingresos)
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Salidas (ventas) ----------
        modelBuilder.Entity<Salida>(e =>
        {
            e.ToTable("Salidas", t => t.HasCheckConstraint("CK_Salidas_Cantidad", "[Cantidad] > 0"));
            e.Property(x => x.CodigoPedido).HasMaxLength(20).IsRequired();
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Observaciones).HasMaxLength(500);
            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => x.CodigoPedido);
            e.HasIndex(x => x.Estado);

            e.HasOne(x => x.Producto).WithMany(p => p.Salidas)
             .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cliente).WithMany(c => c.Salidas)
             .HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Usuario).WithMany(u => u.Salidas)
             .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
