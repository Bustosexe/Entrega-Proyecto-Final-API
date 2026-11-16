using System.Globalization;

namespace TechHub.App.ViewModels;

// =====================================================================
// Panel (dashboard)
// =====================================================================

public partial class PanelViewModel(ApiClient api, SesionService sesion) : BaseViewModel
{
    public ObservableCollection<ProductoDto> StockBajo { get; } = [];
    public ObservableCollection<TopProductoDto> TopProductos { get; } = [];
    public ObservableCollection<VentaMarcaDto> VentasPorMarca { get; } = [];

    /// <summary>Gráfico de facturación de los últimos 7 días (se reemplaza en cada carga para redibujar).</summary>
    [ObservableProperty] private Graficos.GraficoBarras? graficoVentas;
    [ObservableProperty] private string totalSemana = "-";
    [ObservableProperty] private string pedidosSemana = string.Empty;
    [ObservableProperty] private bool sinVentasTop;
    [ObservableProperty] private bool sinVentasMarca;

    [ObservableProperty] private string saludo = string.Empty;
    [ObservableProperty] private string facturacionHoy = "-";
    [ObservableProperty] private string ventasHoy = "-";
    [ObservableProperty] private string facturacionMes = "-";
    [ObservableProperty] private string pendientes = "-";
    [ObservableProperty] private string enRevision = "-";
    [ObservableProperty] private string unidades = "-";
    [ObservableProperty] private string productos = "-";
    [ObservableProperty] private string valorInventario = "-";
    [ObservableProperty] private string stockBajoCantidad = "-";
    [ObservableProperty] private string clientesProveedores = "-";
    [ObservableProperty] private bool sinStockBajo;

    public Task AlAparecerAsync()
    {
        Saludo = $"Hola, {sesion.Usuario?.NombreCompleto}";
        return CargarAsync();
    }

    [RelayCommand]
    private Task CargarAsync() => EjecutarAsync(async () =>
    {
        var r = await api.GetAsync<ResumenDto>("api/dashboard/resumen");
        FacturacionHoy = Formato.Moneda(r.FacturacionHoy);
        VentasHoy = r.VentasHoy == 1 ? "1 venta" : $"{r.VentasHoy} ventas";
        FacturacionMes = Formato.Moneda(r.FacturacionMes);
        Pendientes = r.PedidosPendientes.ToString();
        EnRevision = r.PagosEnRevision.ToString();
        Unidades = r.UnidadesEnStock.ToString("N0", Formato.Cultura);
        Productos = $"{r.TotalProductos} productos activos";
        ValorInventario = Formato.Moneda(r.ValorInventario);
        StockBajoCantidad = r.ProductosStockBajo.ToString();
        ClientesProveedores = $"{r.Clientes} clientes · {r.Proveedores} proveedores";

        var bajos = await api.GetAsync<PagedResult<ProductoDto>>("api/productos?soloStockBajo=true&orden=stock&tamanio=10");
        StockBajo.Clear();
        foreach (var p in bajos.Items) StockBajo.Add(p);
        SinStockBajo = StockBajo.Count == 0;

        // Ventas de los últimos 7 días
        var dias = await api.GetAsync<List<VentaDiaDto>>("api/dashboard/ventas-diarias?dias=7");
        GraficoVentas = new Graficos.GraficoBarras(dias.Select(d => (d.Etiqueta, (double)d.Total)).ToList());
        TotalSemana = Formato.Moneda(dias.Sum(d => d.Total));
        var pedidos = dias.Sum(d => d.Pedidos);
        PedidosSemana = pedidos == 1 ? "1 pedido en 7 días" : $"{pedidos} pedidos en 7 días";

        // Ranking de productos más vendidos
        var top = await api.GetAsync<List<TopProductoDto>>("api/dashboard/top-productos?cantidad=5");
        var maximo = Math.Max(1, top.Count == 0 ? 1 : top.Max(t => t.Unidades));
        TopProductos.Clear();
        for (var i = 0; i < top.Count; i++)
        {
            top[i].Puesto = i + 1;
            top[i].Proporcion = top[i].Unidades / (double)maximo;
            TopProductos.Add(top[i]);
        }
        SinVentasTop = TopProductos.Count == 0;

        // Ventas por marca (tabla Marcas)
        var marcas = await api.GetAsync<List<VentaMarcaDto>>("api/dashboard/ventas-por-marca?cantidad=6");
        var mayor = marcas.Count == 0 ? 1m : Math.Max(1m, marcas.Max(x => x.Total));
        VentasPorMarca.Clear();
        foreach (var m in marcas)
        {
            m.Proporcion = (double)(m.Total / mayor);
            VentasPorMarca.Add(m);
        }
        SinVentasMarca = VentasPorMarca.Count == 0;
    });

    [RelayCommand]
    private Task VerPendientesAsync() => IrAsync("//admin/pedidosadmin");

    public Task AbrirProductoAsync(ProductoDto p) =>
        IrAsync("productoform", new Dictionary<string, object> { ["Producto"] = p });
}

// =====================================================================
// Pedidos (admin)
// =====================================================================

public partial class PedidosAdminViewModel(ApiClient api) : BaseViewModel
{
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<PedidoDto> Pedidos { get; } = [];
    public List<string> Estados { get; } = EstadosPedido.Filtros.Select(f => f.Texto).ToList();

    [ObservableProperty] private string estado = "Todos";
    [ObservableProperty] private string buscar = string.Empty;
    [ObservableProperty] private string resultadoTexto = string.Empty;
    [ObservableProperty] private string mensajeVacio = "Cargando...";

    public Task AlAparecerAsync() => CargarAsync(1);

    partial void OnEstadoChanged(string value) => Demorar(() => CargarAsync(1), 0);
    partial void OnBuscarChanged(string value) => Demorar(() => CargarAsync(1));

    [RelayCommand]
    private Task RecargarAsync() => CargarAsync(1);

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (_hayMas && !EstaOcupado) await CargarAsync(_pagina + 1);
    }

    public Task AbrirAsync(PedidoDto p) =>
        IrAsync("pedido", new Dictionary<string, object> { ["Codigo"] = p.Codigo });

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        var url = $"api/pedidos?pagina={pagina}&tamanio=10";
        var codigoEstado = EstadosPedido.Filtros.FirstOrDefault(f => f.Texto == Estado).Codigo;
        if (!string.IsNullOrEmpty(codigoEstado)) url += $"&estado={codigoEstado}";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";

        var r = await api.GetAsync<PagedResult<PedidoDto>>(url);
        if (pagina == 1) Pedidos.Clear();
        foreach (var p in r.Items) Pedidos.Add(p);
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        ResultadoTexto = r.TotalItems == 1 ? "1 pedido" : $"{r.TotalItems} pedidos";
        MensajeVacio = "No hay pedidos con ese filtro.";
    });
}

// =====================================================================
// Productos (admin)
// =====================================================================

public partial class ProductosAdminViewModel(ApiClient api) : BaseViewModel
{
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<ProductoDto> Productos { get; } = [];

    [ObservableProperty] private string buscar = string.Empty;
    [ObservableProperty] private bool incluirInactivos;
    [ObservableProperty] private bool soloStockBajo;
    [ObservableProperty] private string resultadoTexto = string.Empty;

    public Task AlAparecerAsync() => CargarAsync(1);

    partial void OnBuscarChanged(string value) => Demorar(() => CargarAsync(1));
    partial void OnIncluirInactivosChanged(bool value) => Demorar(() => CargarAsync(1), 0);
    partial void OnSoloStockBajoChanged(bool value) => Demorar(() => CargarAsync(1), 0);

    [RelayCommand]
    private Task RecargarAsync() => CargarAsync(1);

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (_hayMas && !EstaOcupado) await CargarAsync(_pagina + 1);
    }

    [RelayCommand]
    private Task NuevoAsync() => IrAsync("productoform");

    public Task AbrirAsync(ProductoDto p) =>
        IrAsync("productoform", new Dictionary<string, object> { ["Producto"] = p });

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        var url = $"api/productos?pagina={pagina}&tamanio=15&incluirInactivos={IncluirInactivos.ToString().ToLowerInvariant()}";
        if (SoloStockBajo) url += "&soloStockBajo=true&orden=stock";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";

        var r = await api.GetAsync<PagedResult<ProductoDto>>(url);
        if (pagina == 1) Productos.Clear();
        foreach (var p in r.Items) Productos.Add(p);
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        ResultadoTexto = r.TotalItems == 1 ? "1 producto" : $"{r.TotalItems} productos";
    });
}

// =====================================================================
// Alta / edición de producto + foto
// =====================================================================

public partial class ProductoFormViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private ProductoDto? _producto;
    private FileResult? _fotoPendiente;
    private bool _inicializado;

    public ObservableCollection<CategoriaDto> Categorias { get; } = [];
    public ObservableCollection<MarcaDto> Marcas { get; } = [];

    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private MarcaDto? marca;
    [ObservableProperty] private string descripcion = string.Empty;
    [ObservableProperty] private string precio = string.Empty;
    [ObservableProperty] private string stockMinimo = "2";
    [ObservableProperty] private bool activo = true;
    [ObservableProperty] private CategoriaDto? categoria;
    [ObservableProperty] private ImageSource? imagen;
    [ObservableProperty] private string stockTexto = "Stock actual: 0 (se carga con Compras)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneImagen))]
    private bool esEdicion;

    public bool TieneImagen => !string.IsNullOrEmpty(_producto?.ImagenUrl);

    private bool _parametrosAplicados;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell vuelve a llamar a este método cuando se cierra una alerta o un selector; sin esta guarda
        // se pisaba lo que el usuario ya había escrito.
        if (_parametrosAplicados) return;
        _parametrosAplicados = true;

        if (query.TryGetValue("Producto", out var v) && v is ProductoDto p)
        {
            _producto = p;
            EsEdicion = true;
            Titulo = "Editar producto";
            Nombre = p.Nombre;
            Descripcion = p.Descripcion ?? string.Empty;
            Precio = p.Precio.ToString("0.##", Formato.Cultura);
            StockMinimo = p.StockMinimo.ToString();
            Activo = p.Activo;
            StockTexto = $"Stock actual: {p.Stock} (se modifica con Compras y Ventas)";
            Imagen = string.IsNullOrEmpty(p.ImagenUrl)
                ? (ImageSource)"producto_placeholder.png"
                : ImageSource.FromUri(new Uri(p.ImagenCompleta));
        }
        else
        {
            Titulo = "Nuevo producto";
            Imagen = "producto_placeholder.png";
        }
    }

    public async Task AlAparecerAsync()
    {
        if (_inicializado) return;
        _inicializado = true;
        await EjecutarAsync(async () =>
        {
            var r = await api.GetAsync<PagedResult<CategoriaDto>>("api/categorias?tamanio=50");
            Categorias.Clear();
            foreach (var c in r.Items.Where(c => c.Activo || c.Id == _producto?.CategoriaId))
                Categorias.Add(c);
            Categoria = Categorias.FirstOrDefault(c => c.Id == _producto?.CategoriaId);

            var m = await api.GetAsync<PagedResult<MarcaDto>>("api/marcas?tamanio=50");
            Marcas.Clear();
            foreach (var x in m.Items.Where(x => x.Activo || x.Id == _producto?.MarcaId))
                Marcas.Add(x);
            Marca = Marcas.FirstOrDefault(x => x.Id == _producto?.MarcaId);
        });
    }

    /// <summary>Crea una marca sin salir del formulario y la deja seleccionada.</summary>
    [RelayCommand]
    private async Task NuevaMarcaAsync()
    {
        var nombreMarca = await Dialogos.Pedir("Nueva marca", "Nombre de la marca:", Keyboard.Text, "Ej: Gigabyte");
        if (string.IsNullOrWhiteSpace(nombreMarca)) return;

        var existente = Marcas.FirstOrDefault(x => string.Equals(x.Nombre, nombreMarca.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null)
        {
            Marca = existente;
            return;
        }

        await EjecutarAsync(async () =>
        {
            var creada = await api.PostAsync<MarcaDto>("api/marcas", new { nombre = nombreMarca.Trim(), activo = true });
            var posicion = Marcas.TakeWhile(x => string.Compare(x.Nombre, creada.Nombre, StringComparison.OrdinalIgnoreCase) < 0).Count();
            Marcas.Insert(posicion, creada);
            Marca = creada;
            Dialogos.Aviso($"Marca creada: {creada.Nombre}");
        });
    }

    [RelayCommand]
    private async Task ElegirFotoAsync()
    {
        FileResult? archivo;
        try
        {
            archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Elegí la foto del producto",
                FileTypes = FilePickerFileType.Images
            });
        }
        catch (Exception ex)
        {
            await Dialogos.Alerta("Galería", $"No se pudo abrir la galería: {ex.Message}", TipoAlerta.Error);
            return;
        }
        if (archivo is null) return;

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            await Dialogos.Advertencia("Formato no permitido", "Elegí una imagen JPG, PNG o WEBP.");
            return;
        }

        if (EsEdicion && _producto is not null)
        {
            // Producto existente: se sube en el momento.
            await SubirFotoAsync(_producto.Id, archivo);
        }
        else
        {
            // Producto nuevo: se sube después de crearlo.
            _fotoPendiente = archivo;
            Imagen = ImageSource.FromFile(archivo.FullPath);
        }
    }

    [RelayCommand]
    private async Task QuitarFotoAsync()
    {
        if (_producto is null) return;
        if (!await Dialogos.Confirmar("Quitar foto", "¿Eliminar la foto del producto?", "Eliminar", "Cancelar", peligro: true)) return;
        await EjecutarAsync(async () =>
        {
            await api.DeleteAsync($"api/productos/{_producto.Id}/imagen");
            _producto.ImagenUrl = null;
            Imagen = "producto_placeholder.png";
            OnPropertyChanged(nameof(TieneImagen));
        });
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre) || Marca is null || Categoria is null)
        {
            await Dialogos.Advertencia("Datos incompletos", "Completá nombre, marca y categoría.");
            return;
        }
        if (!TryParseDecimal(Precio, out var precio) || precio < 0)
        {
            await Dialogos.Advertencia("Precio inválido", "Ingresá un precio válido. Ej: 850000 o 850000,50");
            return;
        }
        if (!int.TryParse(StockMinimo, out var minimo) || minimo < 0)
        {
            await Dialogos.Advertencia("Stock mínimo inválido", "Ingresá un número entero mayor o igual a 0.");
            return;
        }

        var cuerpo = new
        {
            nombre = Nombre.Trim(),
            marcaId = Marca.Id,
            descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
            precio,
            stockMinimo = minimo,
            categoriaId = Categoria.Id,
            activo = Activo
        };

        ProductoDto? guardado = null;
        var ok = await EjecutarAsync(async () =>
        {
            guardado = EsEdicion && _producto is not null
                ? await api.PutAsync<ProductoDto>($"api/productos/{_producto.Id}", cuerpo)
                : await api.PostAsync<ProductoDto>("api/productos", cuerpo);
        });
        if (!ok || guardado is null) return;

        if (_fotoPendiente is not null)
            await SubirFotoAsync(guardado.Id, _fotoPendiente);

        Dialogos.Aviso(EsEdicion
            ? "Cambios guardados."
            : "Producto creado. Registrá una compra para cargarle stock.");
        await IrAsync("..");
    }

    [RelayCommand]
    private async Task DarDeBajaAsync()
    {
        if (_producto is null) return;
        if (!await Dialogos.Confirmar("Dar de baja",
                $"¿Dar de baja \"{_producto.Nombre}\"? Deja de mostrarse en el catálogo, pero se conserva su historial.",
                "Dar de baja", "Cancelar", peligro: true))
            return;
        if (await EjecutarAsync(() => api.DeleteAsync($"api/productos/{_producto.Id}")))
            await IrAsync("..");
    }

    private async Task SubirFotoAsync(int productoId, FileResult archivo)
    {
        await EjecutarAsync(async () =>
        {
            await using var stream = await archivo.OpenReadAsync();
            if (stream.CanSeek && stream.Length > 5 * 1024 * 1024)
                throw new ApiException(400, "La imagen supera los 5 MB.");
            var p = await api.SubirArchivoAsync<ProductoDto>($"api/productos/{productoId}/imagen", stream, archivo.FileName);
            _producto = p;
            _fotoPendiente = null;
            Imagen = ImageSource.FromUri(new Uri(p.ImagenCompleta));
            OnPropertyChanged(nameof(TieneImagen));
        });
    }

    internal static bool TryParseDecimal(string texto, out decimal valor) =>
        decimal.TryParse(texto?.Trim(), NumberStyles.Number, Formato.Cultura, out valor) ||
        decimal.TryParse(texto?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
}

// =====================================================================
// Compras a proveedores: una factura (Compras) con varios productos (DetallesCompra)
// =====================================================================

public partial class ComprasViewModel(ApiClient api) : BaseViewModel
{
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<CompraDto> Compras { get; } = [];

    [ObservableProperty] private string buscar = string.Empty;
    [ObservableProperty] private string mensajeVacio = "Cargando...";

    public Task AlAparecerAsync() => CargarAsync(1);

    partial void OnBuscarChanged(string value) => Demorar(() => CargarAsync(1));

    [RelayCommand]
    private Task RecargarAsync() => CargarAsync(1);

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (_hayMas && !EstaOcupado) await CargarAsync(_pagina + 1);
    }

    [RelayCommand]
    private Task NuevoAsync() => IrAsync("compraform");

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        var url = $"api/compras?pagina={pagina}&tamanio=10";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";
        var r = await api.GetAsync<PagedResult<CompraDto>>(url);
        if (pagina == 1) Compras.Clear();
        foreach (var c in r.Items) Compras.Add(c);
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        MensajeVacio = "Todavía no se registraron compras.";
    });
}

/// <summary>Renglón que se está cargando en la compra (todavía no guardado).</summary>
public class RenglonCompra
{
    public int ProductoId { get; init; }
    public string Producto { get; init; } = string.Empty;
    public string ImagenCompleta { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public decimal CostoUnitario { get; init; }

    public decimal Subtotal => Cantidad * CostoUnitario;
    public string Detalle => $"{Cantidad} × {Formato.Moneda(CostoUnitario)}";
    public string SubtotalTexto => Formato.Moneda(Subtotal);
}

public partial class CompraFormViewModel(ApiClient api) : BaseViewModel
{
    private bool _inicializado;

    public ObservableCollection<ProductoDto> Productos { get; } = [];
    public ObservableCollection<ProveedorDto> Proveedores { get; } = [];
    public ObservableCollection<RenglonCompra> Renglones { get; } = [];

    // Cabecera (la factura)
    [ObservableProperty] private ProveedorDto? proveedor;
    [ObservableProperty] private string numeroComprobante = string.Empty;
    [ObservableProperty] private string observaciones = string.Empty;

    // Renglón que se está agregando
    [ObservableProperty] private string buscarProducto = string.Empty;
    [ObservableProperty] private ProductoDto? producto;
    [ObservableProperty] private string cantidad = "1";
    [ObservableProperty] private string costoUnitario = string.Empty;

    [ObservableProperty] private string totalTexto = Formato.Moneda(0);
    [ObservableProperty] private string resumenTexto = "Todavía no agregaste productos.";
    [ObservableProperty] private bool tieneRenglones;

    public async Task AlAparecerAsync()
    {
        if (_inicializado) return;
        _inicializado = true;
        Titulo = "Registrar compra";
        await EjecutarAsync(async () =>
        {
            var provs = await api.GetAsync<PagedResult<ProveedorDto>>("api/proveedores?tamanio=50");
            Proveedores.Clear();
            foreach (var p in provs.Items.Where(p => p.Activo)) Proveedores.Add(p);
            await CargarProductosAsync();
        });
    }

    partial void OnBuscarProductoChanged(string value) => Demorar(() => EjecutarAsync(CargarProductosAsync));

    private async Task CargarProductosAsync()
    {
        var url = "api/productos?tamanio=50&incluirInactivos=true&orden=nombre";
        if (!string.IsNullOrWhiteSpace(BuscarProducto)) url += $"&buscar={Uri.EscapeDataString(BuscarProducto.Trim())}";
        var r = await api.GetAsync<PagedResult<ProductoDto>>(url);
        Productos.Clear();
        foreach (var p in r.Items) Productos.Add(p);
        Producto = Productos.Count == 1 ? Productos[0] : null;
    }

    [RelayCommand]
    private async Task AgregarRenglonAsync()
    {
        if (Producto is null)
        {
            await Dialogos.Advertencia("Elegí un producto", "Seleccioná el producto que llegó.");
            return;
        }
        if (!int.TryParse(Cantidad, out var cant) || cant <= 0)
        {
            await Dialogos.Advertencia("Cantidad inválida", "Ingresá una cantidad mayor a 0.");
            return;
        }
        if (!ProductoFormViewModel.TryParseDecimal(CostoUnitario, out var costo) || costo < 0)
        {
            await Dialogos.Advertencia("Costo inválido", "Ingresá el costo unitario. Ej: 700000");
            return;
        }

        // Si el producto ya estaba en la compra, se suman las cantidades.
        var existente = Renglones.FirstOrDefault(r => r.ProductoId == Producto.Id);
        if (existente is not null)
        {
            cant += existente.Cantidad;
            Renglones.Remove(existente);
        }

        Renglones.Add(new RenglonCompra
        {
            ProductoId = Producto.Id,
            Producto = Producto.Nombre,
            ImagenCompleta = Producto.ImagenCompleta,
            Cantidad = cant,
            CostoUnitario = costo
        });

        Producto = null;
        Cantidad = "1";
        CostoUnitario = string.Empty;
        ActualizarTotales();
        Dialogos.Aviso("Producto agregado a la compra.");
    }

    public void QuitarRenglon(RenglonCompra renglon)
    {
        Renglones.Remove(renglon);
        ActualizarTotales();
    }

    private void ActualizarTotales()
    {
        TieneRenglones = Renglones.Count > 0;
        TotalTexto = Formato.Moneda(Renglones.Sum(r => r.Subtotal));
        var unidades = Renglones.Sum(r => r.Cantidad);
        ResumenTexto = TieneRenglones
            ? $"{Renglones.Count} {(Renglones.Count == 1 ? "producto" : "productos")} · {unidades} {(unidades == 1 ? "unidad" : "unidades")}"
            : "Todavía no agregaste productos.";
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (Proveedor is null)
        {
            await Dialogos.Advertencia("Falta el proveedor", "Elegí el proveedor de la factura.");
            return;
        }
        if (Renglones.Count == 0)
        {
            await Dialogos.Advertencia("Compra vacía", "Agregá al menos un producto con su cantidad y costo.");
            return;
        }

        CompraDto? compra = null;
        var ok = await EjecutarAsync(async () =>
        {
            compra = await api.PostAsync<CompraDto>("api/compras", new
            {
                proveedorId = Proveedor.Id,
                numeroComprobante = string.IsNullOrWhiteSpace(NumeroComprobante) ? null : NumeroComprobante.Trim(),
                observaciones = string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim(),
                items = Renglones.Select(r => new { productoId = r.ProductoId, cantidad = r.Cantidad, costoUnitario = r.CostoUnitario }).ToList()
            });
        });

        if (ok && compra is not null)
        {
            await Dialogos.Exito("¡Compra registrada!", $"{compra.Resumen} sumadas al stock. Total {compra.TotalTexto}.", 1800);
            await IrAsync("..");
        }
    }
}

// =====================================================================
// Reportes en PDF (descargar, compartir o enviar por email)
// =====================================================================

public partial class ReportesViewModel(ApiClient api, SesionService sesion) : BaseViewModel
{
    [ObservableProperty] private DateTime desde = DateTime.Today.AddDays(-29);
    [ObservableProperty] private DateTime hasta = DateTime.Today;
    [ObservableProperty] private string emailDestino = string.Empty;

    public void AlAparecer()
    {
        Titulo = "Reportes";
        if (string.IsNullOrWhiteSpace(EmailDestino))
            EmailDestino = sesion.Usuario?.Email ?? string.Empty;
    }

    private string UrlVentas => $"api/reportes/ventas?desde={Desde:yyyy-MM-dd}&hasta={Hasta:yyyy-MM-dd}";

    [RelayCommand]
    private void Periodo(string dias)
    {
        Hasta = DateTime.Today;
        Desde = dias == "mes"
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : DateTime.Today.AddDays(-(int.Parse(dias) - 1));
    }

    [RelayCommand]
    private Task VerInventarioAsync() => EjecutarAsync(async () =>
        await Archivos.AbrirAsync(await api.DescargarAsync("api/reportes/inventario", "TechHub-Inventario.pdf")));

    [RelayCommand]
    private Task CompartirInventarioAsync() => EjecutarAsync(async () =>
        await Archivos.CompartirAsync(await api.DescargarAsync("api/reportes/inventario", "TechHub-Inventario.pdf"), "Inventario valorizado"));

    [RelayCommand]
    private Task EnviarInventarioAsync() => EnviarAsync(new { tipo = "inventario", email = Destino() });

    [RelayCommand]
    private Task VerVentasAsync() => EjecutarAsync(async () =>
        await Archivos.AbrirAsync(await api.DescargarAsync(UrlVentas, "TechHub-Ventas.pdf")));

    [RelayCommand]
    private Task CompartirVentasAsync() => EjecutarAsync(async () =>
        await Archivos.CompartirAsync(await api.DescargarAsync(UrlVentas, "TechHub-Ventas.pdf"), "Reporte de ventas"));

    [RelayCommand]
    private Task EnviarVentasAsync() =>
        EnviarAsync(new { tipo = "ventas", email = Destino(), desde = Desde.ToString("yyyy-MM-dd"), hasta = Hasta.ToString("yyyy-MM-dd") });

    private string? Destino() => string.IsNullOrWhiteSpace(EmailDestino) ? null : EmailDestino.Trim();

    private async Task EnviarAsync(object cuerpo)
    {
        if (Destino() is null)
        {
            await Dialogos.Advertencia("Email", "Escribí el email de destino.");
            return;
        }
        if (await EjecutarAsync(() => api.PostAsync("api/reportes/enviar", cuerpo)))
            Dialogos.Aviso($"Reporte enviado a {Destino()}.");
    }
}
