namespace TechHub.App.ViewModels;

// =====================================================================
// Catálogo (público): búsqueda, categorías, orden y scroll infinito
// =====================================================================

public partial class CategoriaFiltro : ObservableObject
{
    public int? Id { get; init; }
    public string Nombre { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fondo))]
    [NotifyPropertyChangedFor(nameof(ColorTexto))]
    private bool seleccionada;

    public Color Fondo => Seleccionada ? Paleta.Primario : Paleta.Blanco;
    public Color ColorTexto => Seleccionada ? Paleta.Blanco : Paleta.Oscuro;
}

public partial class CatalogoViewModel(ApiClient api, CarritoService carrito) : BaseViewModel
{
    private const int Tamanio = 10;
    private int _pagina;
    private bool _hayMas;
    private bool _inicializado;
    private int? _categoriaId;
    private decimal? _precioMin;
    private decimal? _precioMax;

    /// <summary>Rangos del filtro de precio (texto, mínimo, máximo).</summary>
    private static readonly (string Texto, decimal? Min, decimal? Max)[] RangosPrecio =
    [
        ("Todos los precios", null, null),
        ("Hasta $ 100.000", null, 100000),
        ("$ 100.000 a $ 300.000", 100000, 300000),
        ("$ 300.000 a $ 600.000", 300000, 600000),
        ("Más de $ 600.000", 600000, null)
    ];

    public ObservableCollection<ProductoDto> Productos { get; } = [];
    public ObservableCollection<ProductoDto> Destacados { get; } = [];
    public ObservableCollection<CategoriaFiltro> Categorias { get; } = [];
    public List<string> Ordenes { get; } = ["Nombre", "Más vendidos", "Novedades", "Menor precio", "Mayor precio"];

    [ObservableProperty] private string buscar = string.Empty;
    [ObservableProperty] private string orden = "Nombre";
    [ObservableProperty] private string resultadoTexto = string.Empty;
    [ObservableProperty] private string mensajeVacio = "Cargando productos...";
    [ObservableProperty] private string precioTexto = "Precio";
    [ObservableProperty] private bool mostrarDestacados;

    public async Task AlAparecerAsync()
    {
        if (_inicializado) return;
        _inicializado = true;
        await CargarCategoriasAsync();
        await CargarDestacadosAsync();
        await CargarPaginaAsync(1);
    }

    partial void OnBuscarChanged(string value) => Demorar(() => CargarPaginaAsync(1));

    partial void OnOrdenChanged(string value)
    {
        if (_inicializado) Demorar(() => CargarPaginaAsync(1), 0);
    }

    [RelayCommand]
    private async Task RecargarAsync()
    {
        await CargarDestacadosAsync();
        await CargarPaginaAsync(1);
    }

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (_hayMas && !EstaOcupado)
            await CargarPaginaAsync(_pagina + 1);
    }

    [RelayCommand]
    private async Task ElegirPrecioAsync()
    {
        var elegido = await Dialogos.Opciones("Filtrar por precio", RangosPrecio.Select(r => r.Texto).ToArray());
        if (elegido is null) return;
        var rango = RangosPrecio.First(r => r.Texto == elegido);
        _precioMin = rango.Min;
        _precioMax = rango.Max;
        PrecioTexto = rango.Min is null && rango.Max is null ? "Precio" : rango.Texto;
        await CargarPaginaAsync(1);
    }

    public async Task SeleccionarCategoriaAsync(CategoriaFiltro categoria)
    {
        foreach (var c in Categorias) c.Seleccionada = c == categoria;
        _categoriaId = categoria.Id;
        await CargarPaginaAsync(1);
    }

    public Task AbrirProductoAsync(ProductoDto p) =>
        IrAsync("producto", new Dictionary<string, object> { ["Producto"] = p });

    /// <summary>Botón "+" de la tarjeta: agrega una unidad sin salir del catálogo.</summary>
    public void AgregarRapido(ProductoDto p)
    {
        if (!p.PuedeAgregar)
        {
            Dialogos.Aviso("Sin stock disponible.");
            return;
        }
        Dialogos.Aviso(carrito.Agregar(p, 1)
            ? $"Agregado al carrito: {p.Nombre}"
            : "No hay más unidades disponibles de este producto.");
    }

    private async Task CargarCategoriasAsync()
    {
        try
        {
            var r = await api.GetAsync<PagedResult<CategoriaDto>>("api/categorias?tamanio=50");
            Categorias.Clear();
            Categorias.Add(new CategoriaFiltro { Id = null, Nombre = "Todas", Seleccionada = true });
            foreach (var c in r.Items.Where(c => c.Activo && c.CantidadProductos > 0))
                Categorias.Add(new CategoriaFiltro { Id = c.Id, Nombre = c.Nombre });
        }
        catch
        {
            // Las categorías son un filtro opcional: si fallan, el catálogo igual se muestra.
        }
    }

    private async Task CargarDestacadosAsync()
    {
        try
        {
            var r = await api.GetAsync<PagedResult<ProductoDto>>("api/productos?orden=vendidos&tamanio=8");
            Destacados.Clear();
            foreach (var p in r.Items.Where(p => p.HayStock)) Destacados.Add(p);
        }
        catch
        {
            Destacados.Clear();
        }
        ActualizarDestacados();
    }

    private void ActualizarDestacados() =>
        MostrarDestacados = Destacados.Count > 0 && string.IsNullOrWhiteSpace(Buscar)
                            && _categoriaId is null && _precioMin is null && _precioMax is null;

    private Task CargarPaginaAsync(int pagina) => EjecutarAsync(async () =>
    {
        var ordenApi = Orden switch
        {
            "Menor precio" => "precio",
            "Mayor precio" => "precio_desc",
            "Más vendidos" => "vendidos",
            "Novedades" => "nuevos",
            _ => "nombre"
        };
        var url = $"api/productos?pagina={pagina}&tamanio={Tamanio}&orden={ordenApi}";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";
        if (_categoriaId is int id) url += $"&categoriaId={id}";
        if (_precioMin is decimal min) url += $"&precioMin={min.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (_precioMax is decimal max) url += $"&precioMax={max.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var r = await api.GetAsync<PagedResult<ProductoDto>>(url);
        if (pagina == 1) Productos.Clear();
        foreach (var p in r.Items) Productos.Add(p);

        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        ResultadoTexto = r.TotalItems == 1 ? "1 producto" : $"{r.TotalItems} productos";
        MensajeVacio = "No encontramos productos con esos filtros.";
        ActualizarDestacados();
    });
}

// =====================================================================
// Detalle de producto
// =====================================================================

public partial class ProductoDetalleViewModel(ApiClient api, CarritoService carrito) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeComprar))]
    private ProductoDto? producto;

    [ObservableProperty] private int cantidad = 1;
    [ObservableProperty] private bool tieneRelacionados;

    public ObservableCollection<ProductoDto> Relacionados { get; } = [];

    public bool PuedeComprar => Producto?.PuedeAgregar == true;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Producto", out var valor) && valor is ProductoDto p)
        {
            Producto = p;
            Titulo = p.Nombre;
        }
    }

    public async Task AlAparecerAsync()
    {
        if (Producto is null) return;
        try
        {
            // Refresca precio y stock por si cambiaron desde que se cargó el catálogo.
            Producto = await api.GetAsync<ProductoDto>($"api/productos/{Producto.Id}");
        }
        catch { /* se muestran los datos que ya teníamos */ }

        if (Relacionados.Count == 0)
        {
            try
            {
                var lista = await api.GetAsync<List<ProductoDto>>($"api/productos/{Producto.Id}/relacionados?cantidad=6");
                foreach (var r in lista) Relacionados.Add(r);
            }
            catch { /* sección opcional */ }
            TieneRelacionados = Relacionados.Count > 0;
        }
    }

    [RelayCommand]
    private void Sumar()
    {
        if (Producto is not null && Cantidad < Math.Min(Producto.Stock, CarritoService.MaximoPorProducto)) Cantidad++;
    }

    [RelayCommand]
    private void Restar()
    {
        if (Cantidad > 1) Cantidad--;
    }

    [RelayCommand]
    private async Task AgregarAsync()
    {
        if (Producto is null) return;
        if (!Producto.PuedeAgregar)
        {
            await Dialogos.Alerta("Sin stock", "Este producto no tiene unidades disponibles por el momento.");
            return;
        }
        if (!carrito.Agregar(Producto, Cantidad))
        {
            var enCarrito = carrito.Items.FirstOrDefault(i => i.ProductoId == Producto.Id)?.Cantidad ?? 0;
            await Dialogos.Alerta("Stock insuficiente",
                $"Hay {Producto.Stock} unidades disponibles y ya tenés {enCarrito} en el carrito.");
            return;
        }

        Dialogos.Aviso($"Agregado al carrito: {Cantidad} x {Producto.Nombre}");
        Cantidad = 1;
    }

    [RelayCommand]
    private Task IrAlCarritoAsync() => IrAsync("//tienda/carrito");

    public Task AbrirRelacionadoAsync(ProductoDto p) =>
        IrAsync("producto", new Dictionary<string, object> { ["Producto"] = p });
}

// =====================================================================
// Carrito
// =====================================================================

public partial class CarritoViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    public CarritoService Carrito { get; }

    [ObservableProperty] private string observaciones = string.Empty;

    public string TotalTexto => Formato.Moneda(Carrito.Total);
    public string UnidadesTexto => Carrito.Unidades == 1 ? "1 unidad" : $"{Carrito.Unidades} unidades";
    public bool TieneItems => !Carrito.EstaVacio;

    public CarritoViewModel(ApiClient api, SesionService sesion, CarritoService carrito)
    {
        _api = api;
        _sesion = sesion;
        Carrito = carrito;
        Carrito.Cambio += (_, _) => ActualizarTotales();
    }

    public void ActualizarTotales()
    {
        OnPropertyChanged(nameof(TotalTexto));
        OnPropertyChanged(nameof(UnidadesTexto));
        OnPropertyChanged(nameof(TieneItems));
    }

    public void Sumar(CarritoItem item)
    {
        if (item.Cantidad >= item.Stock)
        {
            Dialogos.Aviso($"Solo hay {item.Stock} unidades disponibles de este producto.");
            return;
        }
        Carrito.CambiarCantidad(item, 1);
    }

    public void Restar(CarritoItem item) => Carrito.CambiarCantidad(item, -1);

    public async Task QuitarAsync(CarritoItem item)
    {
        if (await Dialogos.Confirmar("Quitar producto", $"¿Quitar {item.Nombre} del carrito?", "Quitar", "Cancelar"))
            Carrito.Quitar(item);
    }

    [RelayCommand]
    private Task IrAlCatalogoAsync() => IrAsync("//tienda/catalogo");

    [RelayCommand]
    private async Task ConfirmarAsync()
    {
        if (Carrito.EstaVacio) return;

        if (!_sesion.EstaLogueado)
        {
            if (await Dialogos.Confirmar("Necesitás una cuenta",
                    "Para confirmar la compra iniciá sesión o registrate. Tu carrito se mantiene.", "Ingresar", "Ahora no"))
                await IrAsync("login", new Dictionary<string, object> { ["Volver"] = "//tienda/carrito" });
            return;
        }

        if (_sesion.EsAdmin)
        {
            await Dialogos.Alerta("Cuenta de administrador", "Las compras se hacen con una cuenta de comprador.");
            return;
        }

        if (!await Dialogos.Confirmar("Confirmar pedido",
                $"{UnidadesTexto} por un total de {TotalTexto}.\nEl stock queda reservado hasta que la tienda confirme el pedido.",
                "Confirmar", "Revisar"))
            return;

        PedidoDto? pedido = null;
        var ok = await EjecutarAsync(async () =>
        {
            var cuerpo = new
            {
                items = Carrito.Items.Select(i => new { productoId = i.ProductoId, cantidad = i.Cantidad }).ToList(),
                observaciones = string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim()
            };
            pedido = await _api.PostAsync<PedidoDto>("api/pedidos", cuerpo);
        });

        if (ok && pedido is not null)
        {
            Carrito.Vaciar();
            Observaciones = string.Empty;
            await Dialogos.Alerta("¡Pedido realizado!",
                $"Tu pedido {pedido.Codigo} por {pedido.TotalTexto} quedó pendiente de confirmación.");
            await IrAsync("//tienda/mispedidos");
        }
    }
}

// =====================================================================
// Login y registro
// =====================================================================

public partial class LoginViewModel(ApiClient api, SesionService sesion) : BaseViewModel, IQueryAttributable
{
    private string? _volver;

    [ObservableProperty] private string usuario = string.Empty;
    [ObservableProperty] private string password = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Volver", out var v) && v is string ruta && ruta.Length > 0)
            _volver = ruta;
    }

    [RelayCommand]
    private async Task IngresarAsync()
    {
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Password))
        {
            await Dialogos.Alerta("Datos incompletos", "Ingresá tu email (o usuario) y tu contraseña.");
            return;
        }

        LoginResponse? respuesta = null;
        var ok = await EjecutarAsync(async () =>
        {
            respuesta = await api.PostAsync<LoginResponse>("api/auth/login", new { usuario = Usuario.Trim(), password = Password });
        });

        if (ok && respuesta is not null)
        {
            await sesion.IniciarAsync(respuesta);
            Password = string.Empty;
            await AppShell.IrAInicioAsync(sesion, _volver);
        }
    }

    [RelayCommand]
    private Task IrARegistroAsync() =>
        IrAsync("registro", new Dictionary<string, object> { ["Volver"] = _volver ?? string.Empty });
}

public partial class RegistroViewModel(ApiClient api, SesionService sesion) : BaseViewModel, IQueryAttributable
{
    private string? _volver;

    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private string documento = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string telefono = string.Empty;
    [ObservableProperty] private string direccion = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string password2 = string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Volver", out var v) && v is string ruta && ruta.Length > 0)
            _volver = ruta;
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Documento) ||
            string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            await Dialogos.Alerta("Datos incompletos", "Completá nombre, DNI/CUIT, email y contraseña.");
            return;
        }
        if (Password != Password2)
        {
            await Dialogos.Alerta("Contraseña", "Las contraseñas no coinciden.");
            return;
        }

        LoginResponse? respuesta = null;
        var ok = await EjecutarAsync(async () =>
        {
            respuesta = await api.PostAsync<LoginResponse>("api/auth/registro", new
            {
                nombre = Nombre.Trim(),
                documento = Documento.Trim(),
                email = Email.Trim(),
                telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim(),
                direccion = string.IsNullOrWhiteSpace(Direccion) ? null : Direccion.Trim(),
                password = Password
            });
        });

        if (ok && respuesta is not null)
        {
            await sesion.IniciarAsync(respuesta);
            await Dialogos.Alerta("¡Bienvenido!", $"Tu cuenta fue creada, {respuesta.Usuario.NombreCompleto}.");
            await AppShell.IrAInicioAsync(sesion, _volver);
        }
    }
}

// =====================================================================
// Mis pedidos (comprador)
// =====================================================================

public partial class MisPedidosViewModel(ApiClient api, SesionService sesion) : BaseViewModel
{
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<PedidoDto> Pedidos { get; } = [];

    [ObservableProperty] private bool estaLogueado;
    [ObservableProperty] private bool esVisitante;
    [ObservableProperty] private string mensajeVacio = "Cargando...";

    public async Task AlAparecerAsync()
    {
        EstaLogueado = sesion.EsComprador;
        EsVisitante = !EstaLogueado;
        if (EsVisitante)
        {
            Pedidos.Clear();
            return;
        }
        await CargarAsync(1);
    }

    [RelayCommand]
    private Task RecargarAsync() => CargarAsync(1);

    [RelayCommand]
    private async Task CargarMasAsync()
    {
        if (_hayMas && !EstaOcupado) await CargarAsync(_pagina + 1);
    }

    [RelayCommand]
    private Task IrALoginAsync() =>
        IrAsync("login", new Dictionary<string, object> { ["Volver"] = "//tienda/mispedidos" });

    public Task AbrirAsync(PedidoDto p) =>
        IrAsync("pedido", new Dictionary<string, object> { ["Codigo"] = p.Codigo });

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        var r = await api.GetAsync<PagedResult<PedidoDto>>($"api/pedidos/mios?pagina={pagina}&tamanio=10");
        if (pagina == 1) Pedidos.Clear();
        foreach (var p in r.Items) Pedidos.Add(p);
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        MensajeVacio = "Todavía no hiciste ninguna compra.";
    });
}

// =====================================================================
// Detalle de pedido (comprador y admin)
// =====================================================================

public partial class PedidoDetalleViewModel(ApiClient api, SesionService sesion, CarritoService carrito) : BaseViewModel, IQueryAttributable
{
    private static readonly Color ColorPendiente = Color.FromArgb("#CBD5E1");

    private string _codigo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCancelarComprador))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmar))]
    [NotifyPropertyChangedFor(nameof(PuedeEntregar))]
    [NotifyPropertyChangedFor(nameof(PuedeCancelarAdmin))]
    [NotifyPropertyChangedFor(nameof(PuedeVolverAComprar))]
    [NotifyPropertyChangedFor(nameof(EstadoAyuda))]
    [NotifyPropertyChangedFor(nameof(TieneObservaciones))]
    [NotifyPropertyChangedFor(nameof(EsCancelado))]
    [NotifyPropertyChangedFor(nameof(MostrarLineaDeTiempo))]
    [NotifyPropertyChangedFor(nameof(Paso1Color))]
    [NotifyPropertyChangedFor(nameof(Paso2Color))]
    [NotifyPropertyChangedFor(nameof(Paso3Color))]
    [NotifyPropertyChangedFor(nameof(Linea1Color))]
    [NotifyPropertyChangedFor(nameof(Linea2Color))]
    private PedidoDto? pedido;

    public bool EsAdmin => sesion.EsAdmin;
    public bool PuedeCancelarComprador => !EsAdmin && Pedido?.Estado == "Pendiente";
    public bool PuedeConfirmar => EsAdmin && Pedido?.Estado == "Pendiente";
    public bool PuedeEntregar => EsAdmin && Pedido?.Estado is "Pendiente" or "Confirmado";
    public bool PuedeCancelarAdmin => EsAdmin && Pedido?.Estado is "Pendiente" or "Confirmado";
    public bool PuedeVolverAComprar => !EsAdmin && Pedido?.Estado is "Entregado" or "Cancelado";
    public bool TieneObservaciones => !string.IsNullOrWhiteSpace(Pedido?.Observaciones);

    // ----- Línea de tiempo: Pendiente → Confirmado → Entregado -----
    public bool EsCancelado => Pedido?.Estado == "Cancelado";
    public bool MostrarLineaDeTiempo => Pedido is not null && !EsCancelado;

    private int Paso => Pedido?.Estado switch
    {
        "Pendiente" => 1,
        "Confirmado" => 2,
        "Entregado" => 3,
        _ => 0
    };

    private Color ColorPaso(int n) => Paso >= n ? (Paso == 3 ? Paleta.Exito : Paleta.Primario) : ColorPendiente;
    public Color Paso1Color => ColorPaso(1);
    public Color Paso2Color => ColorPaso(2);
    public Color Paso3Color => ColorPaso(3);
    public Color Linea1Color => ColorPaso(2);
    public Color Linea2Color => ColorPaso(3);

    public string EstadoAyuda => Pedido?.Estado switch
    {
        "Pendiente" => EsAdmin
            ? "El stock está reservado. Confirmá el pedido cuando recibas el pago."
            : "Tu pedido está reservado. La tienda se comunicará para coordinar el pago y la entrega.",
        "Confirmado" => "Pago confirmado. Listo para retirar o enviar.",
        "Entregado" => "Pedido entregado. ¡Gracias por tu compra!",
        "Cancelado" => "Pedido cancelado. Las unidades volvieron al stock.",
        _ => string.Empty
    };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Codigo", out var v) && v is string codigo)
        {
            _codigo = codigo;
            Titulo = $"Pedido {codigo}";
        }
    }

    public Task AlAparecerAsync() => CargarAsync();

    [RelayCommand]
    private Task CargarAsync() => EjecutarAsync(async () =>
    {
        Pedido = await api.GetAsync<PedidoDto>($"api/pedidos/{_codigo}");
    });

    [RelayCommand]
    private async Task CancelarAsync()
    {
        if (!await Dialogos.Confirmar("Cancelar pedido", "¿Seguro que querés cancelar este pedido? Las unidades vuelven al stock.", "Cancelar pedido", "Volver"))
            return;

        var ok = await EjecutarAsync(async () =>
        {
            Pedido = EsAdmin
                ? await api.PutAsync<PedidoDto>($"api/pedidos/{_codigo}/estado", new { estado = "Cancelado" })
                : await api.PostAsync<PedidoDto>($"api/pedidos/{_codigo}/cancelar", null);
        });
        if (ok) Dialogos.Aviso("Pedido cancelado.");
    }

    [RelayCommand]
    private Task ConfirmarAsync() => CambiarEstadoAsync("Confirmado", "¿Marcar el pedido como confirmado (pago recibido)?");

    [RelayCommand]
    private Task EntregarAsync() => CambiarEstadoAsync("Entregado", "¿Marcar el pedido como entregado? La venta queda cerrada.");

    /// <summary>Agrega al carrito los productos del pedido que sigan disponibles, con el precio y stock actuales.</summary>
    [RelayCommand]
    private async Task VolverAComprarAsync()
    {
        if (Pedido is null) return;
        int agregados = 0, sinStock = 0;

        var ok = await EjecutarAsync(async () =>
        {
            foreach (var item in Pedido.Items)
            {
                ProductoDto? actual;
                try { actual = await api.GetAsync<ProductoDto>($"api/productos/{item.ProductoId}"); }
                catch (ApiException) { actual = null; }

                var enCarrito = carrito.Items.FirstOrDefault(i => i.ProductoId == item.ProductoId)?.Cantidad ?? 0;
                var cantidad = actual is null ? 0 : Math.Min(item.Cantidad, actual.Stock - enCarrito);
                if (actual is { PuedeAgregar: true } && cantidad > 0 && carrito.Agregar(actual, cantidad))
                    agregados++;
                else
                    sinStock++;
            }
        });
        if (!ok) return;

        if (agregados == 0)
        {
            await Dialogos.Alerta("Sin stock", "Ninguno de los productos de este pedido tiene stock disponible en este momento.");
            return;
        }
        Dialogos.Aviso(sinStock == 0
            ? "Productos agregados al carrito."
            : $"Se agregaron {agregados} productos. {sinStock} sin stock disponible.");
        await IrAsync("//tienda/carrito");
    }

    private async Task CambiarEstadoAsync(string estado, string pregunta)
    {
        if (!await Dialogos.Confirmar("Cambiar estado", pregunta)) return;
        var ok = await EjecutarAsync(async () =>
        {
            Pedido = await api.PutAsync<PedidoDto>($"api/pedidos/{_codigo}/estado", new { estado });
        });
        if (ok) Dialogos.Aviso($"Pedido {estado.ToLowerInvariant()}.");
    }
}

// =====================================================================
// Cuenta: visitante (ingresar / registrarse) o comprador (perfil)
// =====================================================================

public partial class CuentaViewModel(ApiClient api, SesionService sesion) : BaseViewModel
{
    [ObservableProperty] private bool estaLogueado;
    [ObservableProperty] private bool esVisitante;

    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string documento = string.Empty;
    [ObservableProperty] private string telefono = string.Empty;
    [ObservableProperty] private string direccion = string.Empty;
    [ObservableProperty] private string passwordActual = string.Empty;
    [ObservableProperty] private string nuevaPassword = string.Empty;

    public string ServidorTexto => $"Servidor: {Config.ApiConfig.BaseUrl}";

    public async Task AlAparecerAsync()
    {
        EstaLogueado = sesion.EstaLogueado;
        EsVisitante = !EstaLogueado;
        if (!EstaLogueado) return;

        await EjecutarAsync(async () =>
        {
            var p = await api.GetAsync<PerfilDto>("api/auth/perfil");
            Nombre = p.Usuario.NombreCompleto;
            Email = p.Usuario.Email;
            Documento = p.Cliente?.Documento ?? "-";
            Telefono = p.Cliente?.Telefono ?? string.Empty;
            Direccion = p.Cliente?.Direccion ?? string.Empty;
        });
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            await Dialogos.Alerta("Datos incompletos", "El nombre es obligatorio.");
            return;
        }

        PerfilDto? perfil = null;
        var ok = await EjecutarAsync(async () =>
        {
            perfil = await api.PutAsync<PerfilDto>("api/auth/perfil", new
            {
                nombre = Nombre.Trim(),
                telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim(),
                direccion = string.IsNullOrWhiteSpace(Direccion) ? null : Direccion.Trim(),
                passwordActual = string.IsNullOrEmpty(PasswordActual) ? null : PasswordActual,
                nuevaPassword = string.IsNullOrEmpty(NuevaPassword) ? null : NuevaPassword
            });
        });

        if (ok && perfil is not null)
        {
            sesion.ActualizarUsuario(perfil.Usuario);
            PasswordActual = string.Empty;
            NuevaPassword = string.Empty;
            Dialogos.Aviso("Tus datos fueron actualizados.");
        }
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        if (!await Dialogos.Confirmar("Cerrar sesión", "¿Querés cerrar la sesión?")) return;
        sesion.Cerrar();
        await AlAparecerAsync();
    }

    [RelayCommand]
    private Task IrALoginAsync() =>
        IrAsync("login", new Dictionary<string, object> { ["Volver"] = "//tienda/cuenta" });

    [RelayCommand]
    private Task IrARegistroAsync() =>
        IrAsync("registro", new Dictionary<string, object> { ["Volver"] = "//tienda/cuenta" });
}
