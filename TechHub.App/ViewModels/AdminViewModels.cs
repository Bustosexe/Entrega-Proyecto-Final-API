using System.Globalization;

namespace TechHub.App.ViewModels;

// =====================================================================
// Panel (dashboard)
// =====================================================================

public partial class PanelViewModel(ApiClient api, SesionService sesion) : BaseViewModel
{
    public ObservableCollection<ProductoDto> StockBajo { get; } = [];

    [ObservableProperty] private string saludo = string.Empty;
    [ObservableProperty] private string facturacionHoy = "-";
    [ObservableProperty] private string ventasHoy = "-";
    [ObservableProperty] private string facturacionMes = "-";
    [ObservableProperty] private string pendientes = "-";
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
        Unidades = r.UnidadesEnStock.ToString("N0", Formato.Cultura);
        Productos = $"{r.TotalProductos} productos activos";
        ValorInventario = Formato.Moneda(r.ValorInventario);
        StockBajoCantidad = r.ProductosStockBajo.ToString();
        ClientesProveedores = $"{r.Clientes} clientes · {r.Proveedores} proveedores";

        var bajos = await api.GetAsync<PagedResult<ProductoDto>>("api/productos?soloStockBajo=true&orden=stock&tamanio=10");
        StockBajo.Clear();
        foreach (var p in bajos.Items) StockBajo.Add(p);
        SinStockBajo = StockBajo.Count == 0;
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
    public List<string> Estados { get; } = ["Todos", "Pendiente", "Confirmado", "Entregado", "Cancelado"];

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
        if (Estado != "Todos") url += $"&estado={Estado}";
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

    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private string marca = string.Empty;
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Producto", out var v) && v is ProductoDto p)
        {
            _producto = p;
            EsEdicion = true;
            Titulo = "Editar producto";
            Nombre = p.Nombre;
            Marca = p.Marca;
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
            await Dialogos.Alerta("Galería", $"No se pudo abrir la galería: {ex.Message}");
            return;
        }
        if (archivo is null) return;

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            await Dialogos.Alerta("Formato no permitido", "Elegí una imagen JPG, PNG o WEBP.");
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
        if (!await Dialogos.Confirmar("Quitar foto", "¿Eliminar la foto del producto?")) return;
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
        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Marca) || Categoria is null)
        {
            await Dialogos.Alerta("Datos incompletos", "Completá nombre, marca y categoría.");
            return;
        }
        if (!TryParseDecimal(Precio, out var precio) || precio < 0)
        {
            await Dialogos.Alerta("Precio inválido", "Ingresá un precio válido. Ej: 850000 o 850000,50");
            return;
        }
        if (!int.TryParse(StockMinimo, out var minimo) || minimo < 0)
        {
            await Dialogos.Alerta("Stock mínimo inválido", "Ingresá un número entero mayor o igual a 0.");
            return;
        }

        var cuerpo = new
        {
            nombre = Nombre.Trim(),
            marca = Marca.Trim(),
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

        await Dialogos.Alerta("Guardado", EsEdicion
            ? "Los cambios se guardaron."
            : "Producto creado. Registrá una compra para cargarle stock.");
        await IrAsync("..");
    }

    [RelayCommand]
    private async Task DarDeBajaAsync()
    {
        if (_producto is null) return;
        if (!await Dialogos.Confirmar("Dar de baja",
                $"¿Dar de baja \"{_producto.Nombre}\"? Deja de mostrarse en el catálogo, pero se conserva su historial."))
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
// Compras a proveedores (ingresos de stock)
// =====================================================================

public partial class IngresosViewModel(ApiClient api) : BaseViewModel
{
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<IngresoDto> Ingresos { get; } = [];

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
    private Task NuevoAsync() => IrAsync("ingresoform");

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        var url = $"api/ingresos?pagina={pagina}&tamanio=15";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";
        var r = await api.GetAsync<PagedResult<IngresoDto>>(url);
        if (pagina == 1) Ingresos.Clear();
        foreach (var i in r.Items) Ingresos.Add(i);
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        MensajeVacio = "Todavía no se registraron compras.";
    });
}

public partial class IngresoFormViewModel(ApiClient api) : BaseViewModel
{
    private bool _inicializado;

    public ObservableCollection<ProductoDto> Productos { get; } = [];
    public ObservableCollection<ProveedorDto> Proveedores { get; } = [];

    [ObservableProperty] private string buscarProducto = string.Empty;
    [ObservableProperty] private ProductoDto? producto;
    [ObservableProperty] private ProveedorDto? proveedor;
    [ObservableProperty] private string cantidad = "1";
    [ObservableProperty] private string costoUnitario = string.Empty;
    [ObservableProperty] private string numeroComprobante = string.Empty;
    [ObservableProperty] private string observaciones = string.Empty;

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
    private async Task GuardarAsync()
    {
        if (Producto is null || Proveedor is null)
        {
            await Dialogos.Alerta("Datos incompletos", "Elegí el producto y el proveedor.");
            return;
        }
        if (!int.TryParse(Cantidad, out var cant) || cant <= 0)
        {
            await Dialogos.Alerta("Cantidad inválida", "Ingresá una cantidad mayor a 0.");
            return;
        }
        if (!ProductoFormViewModel.TryParseDecimal(CostoUnitario, out var costo) || costo < 0)
        {
            await Dialogos.Alerta("Costo inválido", "Ingresá el costo unitario. Ej: 700000");
            return;
        }

        IngresoDto? ingreso = null;
        var ok = await EjecutarAsync(async () =>
        {
            ingreso = await api.PostAsync<IngresoDto>("api/ingresos", new
            {
                productoId = Producto.Id,
                proveedorId = Proveedor.Id,
                cantidad = cant,
                costoUnitario = costo,
                numeroComprobante = string.IsNullOrWhiteSpace(NumeroComprobante) ? null : NumeroComprobante.Trim(),
                observaciones = string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim()
            });
        });

        if (ok && ingreso is not null)
        {
            await Dialogos.Alerta("Stock actualizado", $"+{ingreso.Cantidad} unidades de {ingreso.Producto}.");
            await IrAsync("..");
        }
    }
}
