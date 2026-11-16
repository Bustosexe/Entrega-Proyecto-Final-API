# TechHub · Modelo de datos (DER)

Base de datos **SQL Server** creada con **Entity Framework Core 10 (Code-First)**. Son **18 tablas en tercera forma normal (3FN)**.

![Diagrama Entidad-Relación](DER_TechHub.png)

> El mismo diagrama está en texto en [DER_TechHub.mmd](DER_TechHub.mmd) (Mermaid) y se dibuja solo en GitHub:

```mermaid
erDiagram
    Categorias ||--o{ Productos : clasifica
    Marcas ||--o{ Productos : fabrica
    Productos ||--o{ DetallesCompra : "ingresa en"
    Productos ||--o{ DetallesPedido : "se vende en"
    Productos ||--o{ Ofertas : "tiene"
    Productos ||--o{ ComboItems : "forma parte de"
    Combos ||--|{ ComboItems : contiene
    Categorias ||--o{ CategoriasCompatibles : "regla A"
    Categorias ||--o{ CategoriasCompatibles : "regla B"
    Proveedores ||--o{ Compras : factura
    Compras ||--|{ DetallesCompra : contiene
    Clientes ||--o{ Pedidos : realiza
    Clientes |o--o| Usuarios : "tiene cuenta"
    Roles ||--o{ Usuarios : asigna
    Usuarios ||--o{ Compras : registra
    Usuarios ||--o{ Pedidos : registra
    Usuarios |o--o{ HistorialPedidos : "cambia estado"
    Usuarios |o--o{ Auditoria : "realiza"
    EstadosPedido ||--o{ Pedidos : "estado actual"
    EstadosPedido ||--o{ HistorialPedidos : "estado nuevo"
    Pedidos ||--|{ DetallesPedido : contiene
    Pedidos ||--o{ HistorialPedidos : historial

    Categorias {
        int Id PK
        nvarchar100 Nombre UK
        nvarchar300 Descripcion
        bit Activo
    }
    Marcas {
        int Id PK
        nvarchar80 Nombre UK
        bit Activo
    }
    Productos {
        int Id PK
        nvarchar150 Nombre
        nvarchar1000 Descripcion
        decimal Precio "CHECK >= 0"
        int Stock "CHECK >= 0"
        int StockMinimo
        nvarchar300 ImagenUrl
        bit Activo
        datetime2 FechaCreacion
        int CategoriaId FK
        int MarcaId FK
    }
    Proveedores {
        int Id PK
        nvarchar150 RazonSocial
        nvarchar13 Cuit UK
        nvarchar150 Email
        nvarchar30 Telefono
        nvarchar200 Direccion
        bit Activo
    }
    Clientes {
        int Id PK
        nvarchar150 Nombre
        nvarchar13 Documento UK
        nvarchar150 Email
        nvarchar30 Telefono
        nvarchar200 Direccion
        bit Activo
    }
    Roles {
        int Id PK
        nvarchar20 Nombre UK
        nvarchar150 Descripcion
    }
    Usuarios {
        int Id PK
        nvarchar150 NombreUsuario UK
        nvarchar150 NombreCompleto
        nvarchar150 Email UK
        nvarchar500 PasswordHash
        bit Activo
        datetime2 FechaCreacion
        int RolId FK
        int ClienteId FK
    }
    EstadosPedido {
        int Id PK
        nvarchar20 Codigo UK
        nvarchar50 Nombre
    }
    Compras {
        int Id PK
        datetime2 Fecha
        nvarchar50 NumeroComprobante
        nvarchar500 Observaciones
        int ProveedorId FK
        int UsuarioId FK
    }
    DetallesCompra {
        int Id PK
        int CompraId FK
        int ProductoId FK
        int Cantidad "CHECK > 0"
        decimal CostoUnitario
    }
    Pedidos {
        int Id PK
        nvarchar20 Codigo UK
        datetime2 Fecha
        nvarchar500 Observaciones
        nvarchar200 ComprobanteArchivo
        int ClienteId FK
        int UsuarioId FK
        int EstadoId FK
    }
    DetallesPedido {
        int Id PK
        int PedidoId FK
        int ProductoId FK
        int Cantidad "CHECK > 0"
        decimal PrecioUnitario
    }
    HistorialPedidos {
        int Id PK
        int PedidoId FK
        int EstadoId FK
        datetime2 Fecha
        int UsuarioId FK
        nvarchar300 Comentario
    }
    Ofertas {
        int Id PK
        int ProductoId FK
        nvarchar80 Titulo
        int Porcentaje "CHECK 1-90"
        datetime2 Desde
        datetime2 Hasta "CHECK >= Desde"
        bit Activa
    }
    Combos {
        int Id PK
        nvarchar100 Nombre UK
        nvarchar500 Descripcion
        int Porcentaje "CHECK 1-50"
        bit Activo
        datetime2 FechaCreacion
    }
    ComboItems {
        int Id PK
        int ComboId FK
        int ProductoId FK
        int Cantidad "CHECK 1-20"
    }
    CategoriasCompatibles {
        int CategoriaAId PK
        int CategoriaBId PK
    }
    Auditoria {
        bigint Id PK
        datetime2 Fecha
        int UsuarioId FK
        nvarchar60 Accion
        nvarchar40 Entidad
        nvarchar50 EntidadId
        nvarchar1000 Detalle
        nvarchar45 Ip
    }
```

Convenciones: **PK** clave primaria · **FK** clave foránea · **UK** valor único. Los `CHECK` se indican junto a la columna. Notación de pata de gallo: `||` uno y solo uno, `|o` cero o uno, `o{` cero o muchos, `|{` uno o muchos.

## Tablas

| Grupo | Tabla | Qué guarda |
|---|---|---|
| Catálogo | **Categorias** | Rubros (Procesadores, Memorias RAM…). Nombre único. |
| | **Marcas** | Fabricantes (AMD, Intel, ASUS…). Nombre único. |
| | **Productos** | Componente a la venta: precio, stock, stock mínimo, imagen. |
| Personas | **Roles** | Referencia: Admin y Comprador. |
| | **Usuarios** | Cuentas de acceso (contraseña hasheada). |
| | **Clientes** | Datos del comprador (DNI/CUIT único). Un usuario comprador tiene una ficha de cliente. |
| | **Proveedores** | Mayoristas a quienes se les compra (CUIT único). |
| Compras | **Compras** | Cabecera de la factura de un proveedor. |
| | **DetallesCompra** | Renglones de la factura (suman stock). |
| Ventas | **EstadosPedido** | Referencia: Pendiente, EnRevision, Confirmado, Entregado, Cancelado. |
| | **Pedidos** | Cabecera del pedido: código, cliente, estado, comprobante de pago. |
| | **DetallesPedido** | Renglones del pedido con el precio congelado (restan stock). |
| | **HistorialPedidos** | Cada cambio de estado: quién, cuándo y por qué. |
| Promociones | **Ofertas** | Descuento temporal (porcentaje y fechas) sobre un producto. |
| | **Combos** | Paquete de productos con un descuento. |
| | **ComboItems** | Productos de un combo y su cantidad. |
| | **CategoriasCompatibles** | Reglas opcionales de compatibilidad entre categorías (hoy no se exigen al armar un combo). |
| Seguridad | **Auditoria** | Bitácora: qué operación, sobre qué entidad, quién, cuándo y desde qué IP. |

## Relaciones principales

- Una **Categoria** y una **Marca** tienen muchos **Productos** (1:N).
- Una **Compra** tiene muchos **DetallesCompra**, y cada renglón apunta a un **Producto** (N:M entre Compras y Productos resuelta con DetallesCompra).
- Un **Pedido** tiene muchos **DetallesPedido** y muchos **HistorialPedidos** (N:M entre Pedidos y Productos resuelta con DetallesPedido).
- Un **Cliente** realiza muchos **Pedidos**; un **Usuario** puede estar vinculado a un único **Cliente** (1:0..1).
- Un **Rol** tiene muchos **Usuarios**; un **EstadoPedido** se aplica a muchos **Pedidos**.
- Un **Producto** tiene muchas **Ofertas** y puede estar en muchos **Combos** (N:M resuelta con ComboItems).
- Un **Usuario** genera muchos registros de **Auditoria**.

## Normalización

- **1FN:** todas las columnas guardan un valor simple (no hay listas ni grupos repetidos). Lo que se repetía, como los productos de un pedido o de una factura, está en tablas de detalle con un renglón por producto.
- **2FN:** en las tablas de detalle cada dato depende de la clave completa de su renglón (`Cantidad` y `PrecioUnitario` dependen del renglón, no solo del pedido). Los datos del pedido (cliente, estado, fecha) viven en la cabecera y no se repiten en cada renglón. La única clave compuesta, `CategoriasCompatibles`, no tiene más columnas.
- **3FN:** no hay dependencias entre columnas que no son clave. La marca, el rol y el estado del pedido son tablas de referencia en vez de texto repetido, y los totales y subtotales no se guardan: se calculan con `Cantidad × Precio`.

## Decisiones de diseño

- **Precio congelado:** `DetallesPedido.PrecioUnitario` guarda el precio del momento de la compra (ya con la oferta o el combo aplicados). Un cambio posterior de precio no altera los pedidos anteriores.
- **Stock confiable:** el stock no se edita a mano: sube con las compras y baja con los pedidos, con una actualización condicional dentro de una transacción. Además existe `CHECK (Stock >= 0)`.
- **Ofertas sin duplicar precios:** el precio final se calcula al consultar (precio de lista y el mayor descuento vigente); no se guarda en el producto.
- **Bajas lógicas:** productos, clientes, proveedores, usuarios y combos se desactivan, no se borran (`ON DELETE RESTRICT`).
- **Trazabilidad:** `HistorialPedidos` registra los cambios de estado de cada pedido y `Auditoria` registra las operaciones de los usuarios sobre cualquier entidad.
