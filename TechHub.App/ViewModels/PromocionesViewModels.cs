namespace TechHub.App.ViewModels;

// =====================================================================
// Pantallas del administrador: ofertas y combos de productos
// =====================================================================

/// <summary>Elegir un producto buscándolo por nombre (evita cargar un Picker enorme).</summary>
public static class SelectorProducto
{
    public static async Task<ProductoDto?> ElegirAsync(ApiClient api)
    {
        var texto = await Dialogos.Pedir("Buscar producto", "Escribí parte del nombre (ej: Ryzen, RTX, DDR5).", placeholder: "Nombre del producto");
        if (texto is null) return null;

        var url = "api/productos?tamanio=12&orden=nombre";
        if (!string.IsNullOrWhiteSpace(texto)) url += $"&buscar={Uri.EscapeDataString(texto.Trim())}";

        PagedResult<ProductoDto> r;
        try { r = await api.GetAsync<PagedResult<ProductoDto>>(url); }
        catch (Exception ex) { await Dialogos.Error(ex); return null; }

        if (r.Items.Count == 0)
        {
            await Dialogos.Advertencia("Sin resultados", "No se encontraron productos con ese nombre.");
            return null;
        }

        // Se numera para que dos productos con el mismo nombre no se confundan.
        var etiquetas = r.Items.Select((p, i) => $"{i + 1}. {p.Nombre} · {p.Categoria}").ToArray();
        var elegido = await Dialogos.Opciones("Elegí un producto", etiquetas);
        if (elegido is null) return null;
        var indice = Array.IndexOf(etiquetas, elegido);
        return indice >= 0 ? r.Items[indice] : null;
    }
}

// ---------------------------------------------------------------------
// Oferta
// ---------------------------------------------------------------------

public partial class OfertaFormViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private int? _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProductoTexto), nameof(Vista))]
    private ProductoDto? producto;

    [ObservableProperty] private string tituloOferta = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vista))]
    private string porcentajeTexto = "10";

    [ObservableProperty] private DateTime desde = DateTime.Today;
    [ObservableProperty] private DateTime hasta = DateTime.Today.AddDays(7);
    [ObservableProperty] private bool activa = true;
    [ObservableProperty] private bool puedeBorrar;

    public string ProductoTexto => Producto is null ? "Elegir producto" : Producto.Nombre;

    /// <summary>Vista previa del precio con el descuento.</summary>
    public string Vista
    {
        get
        {
            if (Producto is null || !int.TryParse(PorcentajeTexto, out var pct) || pct is < 1 or > 90) return string.Empty;
            var final = Math.Round(Producto.Precio * (100 - pct) / 100m, 2, MidpointRounding.AwayFromZero);
            return $"{Formato.Moneda(Producto.Precio)} → {Formato.Moneda(final)}";
        }
    }

    private bool _parametrosAplicados;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell vuelve a llamar a este método cuando se cierra una alerta o un selector; sin esta guarda
        // se pisaba lo que el usuario ya había escrito.
        if (_parametrosAplicados) return;
        _parametrosAplicados = true;

        if (query.TryGetValue("Item", out var v) && v is AbmItem { Datos.ValueKind: JsonValueKind.Object } item)
        {
            var e = item.Datos;
            _id = e.GetProperty("id").GetInt32();
            Titulo = "Editar oferta";
            Producto = new ProductoDto
            {
                Id = e.GetProperty("productoId").GetInt32(),
                Nombre = e.GetProperty("producto").GetString() ?? "",
                Precio = e.GetProperty("precioLista").GetDecimal()
            };
            TituloOferta = e.TryGetProperty("titulo", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            PorcentajeTexto = e.GetProperty("porcentaje").GetInt32().ToString();
            Desde = DateTime.Parse(e.GetProperty("desde").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Hasta = DateTime.Parse(e.GetProperty("hasta").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            Activa = e.GetProperty("activa").GetBoolean();
            PuedeBorrar = true;
        }
        else
        {
            _id = null;
            Titulo = "Nueva oferta";
            PuedeBorrar = false;
        }
    }

    [RelayCommand]
    private async Task ElegirProductoAsync()
    {
        var p = await SelectorProducto.ElegirAsync(api);
        if (p is not null) Producto = p;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (Producto is null)
        {
            await Dialogos.Advertencia("Datos incompletos", "Elegí el producto de la oferta.");
            return;
        }
        if (!int.TryParse(PorcentajeTexto, out var pct) || pct is < 1 or > 90)
        {
            await Dialogos.Advertencia("Descuento inválido", "El descuento debe ser un número entre 1 y 90.");
            return;
        }
        if (Hasta.Date < Desde.Date)
        {
            await Dialogos.Advertencia("Fechas inválidas", "La fecha de fin no puede ser anterior a la de inicio.");
            return;
        }

        var cuerpo = new Dictionary<string, object?>
        {
            ["productoId"] = Producto.Id,
            ["titulo"] = string.IsNullOrWhiteSpace(TituloOferta) ? null : TituloOferta.Trim(),
            ["porcentaje"] = pct,
            ["desde"] = Desde.ToString("yyyy-MM-dd"),
            ["hasta"] = Hasta.ToString("yyyy-MM-dd"),
            ["activa"] = Activa
        };

        var ok = await EjecutarAsync(async () =>
        {
            if (_id is null) await api.PostAsync("api/ofertas", cuerpo);
            else await api.PutAsync($"api/ofertas/{_id}", cuerpo);
        });
        if (ok)
        {
            Dialogos.Aviso("Oferta guardada.");
            await IrAsync("..");
        }
    }

    [RelayCommand]
    private async Task EliminarAsync()
    {
        if (_id is null) return;
        if (!await Dialogos.Confirmar("Eliminar oferta", "¿Eliminar esta oferta? El producto vuelve a su precio de lista.", "Eliminar", "Cancelar", peligro: true))
            return;
        if (await EjecutarAsync(() => api.DeleteAsync($"api/ofertas/{_id}")))
            await IrAsync("..");
    }
}

// ---------------------------------------------------------------------
// Combo
// ---------------------------------------------------------------------

/// <summary>Renglón de un combo: un producto y su cantidad.</summary>
public partial class ComboLinea : ObservableObject
{
    public required ProductoDto Producto { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detalle))]
    private int cantidad = 1;

    public string Nombre => Producto.Nombre;
    public string Detalle => $"{Producto.Categoria} · {Formato.Moneda(Producto.Precio)} c/u";
}

public partial class ComboFormViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private int? _id;
    // Pares de categorías compatibles (por nombre, en cualquier orden), tal como los guarda la API.
    private HashSet<(string, string)>? _compatibles;

    public ObservableCollection<ComboLinea> Lineas { get; } = [];

    [ObservableProperty] private string nombre = string.Empty;
    [ObservableProperty] private string descripcion = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resumen))]
    private string porcentajeTexto = "8";

    [ObservableProperty] private bool activo = true;
    [ObservableProperty] private bool puedeBorrar;

    public string Resumen
    {
        get
        {
            if (Lineas.Count == 0) return "Todavía no agregaste productos.";
            var lista = Lineas.Sum(l => l.Producto.Precio * l.Cantidad);
            if (!int.TryParse(PorcentajeTexto, out var pct) || pct is < 1 or > 50) return $"Precio de lista: {Formato.Moneda(lista)}";
            var final = Lineas.Sum(l => Precio(l.Producto.Precio, pct) * l.Cantidad);
            return $"Lista {Formato.Moneda(lista)} → combo {Formato.Moneda(final)} (aprox., sin contar ofertas)";
        }
    }

    private static decimal Precio(decimal lista, int pct) => Math.Round(lista * (100 - pct) / 100m, 2, MidpointRounding.AwayFromZero);

    private bool _parametrosAplicados;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell vuelve a llamar a este método cuando se cierra una alerta o un selector; sin esta guarda
        // se pisaba lo que el usuario ya había escrito.
        if (_parametrosAplicados) return;
        _parametrosAplicados = true;

        Lineas.Clear();
        if (query.TryGetValue("Item", out var v) && v is AbmItem { Datos.ValueKind: JsonValueKind.Object } item)
        {
            var e = item.Datos;
            _id = e.GetProperty("id").GetInt32();
            Titulo = "Editar combo";
            Nombre = e.GetProperty("nombre").GetString() ?? "";
            Descripcion = e.TryGetProperty("descripcion", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? "" : "";
            PorcentajeTexto = e.GetProperty("porcentaje").GetInt32().ToString();
            Activo = e.GetProperty("activo").GetBoolean();
            PuedeBorrar = Activo;
            foreach (var i in e.GetProperty("items").EnumerateArray())
            {
                Lineas.Add(new ComboLinea
                {
                    Cantidad = i.GetProperty("cantidad").GetInt32(),
                    Producto = new ProductoDto
                    {
                        Id = i.GetProperty("productoId").GetInt32(),
                        Nombre = i.GetProperty("producto").GetString() ?? "",
                        Categoria = i.GetProperty("categoria").GetString() ?? "",
                        Precio = i.GetProperty("precioLista").GetDecimal()
                    }
                });
            }
        }
        else
        {
            _id = null;
            Titulo = "Nuevo combo";
            Nombre = Descripcion = string.Empty;
            PorcentajeTexto = "8";
            Activo = true;
            PuedeBorrar = false;
        }
        OnPropertyChanged(nameof(Resumen));
    }

    public async Task AlAparecerAsync()
    {
        if (_compatibles is not null) return;
        try
        {
            var pares = await api.GetAsync<List<CompatibilidadDto>>("api/combos/compatibilidad");
            _compatibles = pares.SelectMany(p => new[] { (p.CategoriaA, p.CategoriaB), (p.CategoriaB, p.CategoriaA) }).ToHashSet();
        }
        catch
        {
            // Sin la lista no se valida en el celular: igual la API rechaza las combinaciones inválidas.
            _compatibles = [];
        }
    }

    /// <summary>Devuelve un mensaje si el producto no puede sumarse al combo actual; null si es válido.</summary>
    private string? Incompatibilidad(ProductoDto nuevo)
    {
        if (Lineas.Any(l => l.Producto.Id == nuevo.Id))
            return $"\"{nuevo.Nombre}\" ya está en el combo: subí la cantidad en lugar de repetirlo.";
        return null;
    }

    [RelayCommand]
    private async Task AgregarProductoAsync()
    {
        await AlAparecerAsync();
        var p = await SelectorProducto.ElegirAsync(api);
        if (p is null) return;

        if (Incompatibilidad(p) is { } motivo)
        {
            await Dialogos.Advertencia("Producto no compatible", motivo);
            return;
        }
        if (Lineas.Count >= 10)
        {
            await Dialogos.Advertencia("Combo completo", "Un combo puede tener hasta 10 productos distintos.");
            return;
        }
        Lineas.Add(new ComboLinea { Producto = p });
        OnPropertyChanged(nameof(Resumen));
    }

    public void Quitar(ComboLinea l) { Lineas.Remove(l); OnPropertyChanged(nameof(Resumen)); }
    public void Mas(ComboLinea l) { if (l.Cantidad < 20) l.Cantidad++; OnPropertyChanged(nameof(Resumen)); }
    public void Menos(ComboLinea l) { if (l.Cantidad > 1) l.Cantidad--; OnPropertyChanged(nameof(Resumen)); }

    partial void OnPorcentajeTextoChanged(string value) => OnPropertyChanged(nameof(Resumen));

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            await Dialogos.Advertencia("Datos incompletos", "Escribí el nombre del combo.");
            return;
        }
        if (!int.TryParse(PorcentajeTexto, out var pct) || pct is < 1 or > 50)
        {
            await Dialogos.Advertencia("Descuento inválido", "El descuento del combo debe estar entre 1 y 50 %.");
            return;
        }
        if (Lineas.Count < 2)
        {
            await Dialogos.Advertencia("Combo incompleto", "Un combo necesita al menos dos productos.");
            return;
        }

        var cuerpo = new Dictionary<string, object?>
        {
            ["nombre"] = Nombre.Trim(),
            ["descripcion"] = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
            ["porcentaje"] = pct,
            ["activo"] = Activo,
            ["items"] = Lineas.Select(l => new Dictionary<string, object> { ["productoId"] = l.Producto.Id, ["cantidad"] = l.Cantidad }).ToList()
        };

        var ok = await EjecutarAsync(async () =>
        {
            if (_id is null) await api.PostAsync("api/combos", cuerpo);
            else await api.PutAsync($"api/combos/{_id}", cuerpo);
        });
        if (ok)
        {
            Dialogos.Aviso("Combo guardado.");
            await IrAsync("..");
        }
    }

    [RelayCommand]
    private async Task BajaAsync()
    {
        if (_id is null) return;
        if (!await Dialogos.Confirmar("Dar de baja", $"¿Dar de baja \"{Nombre}\"? Deja de verse en la tienda; los pedidos anteriores se conservan.", "Dar de baja", "Cancelar", peligro: true))
            return;
        if (await EjecutarAsync(() => api.DeleteAsync($"api/combos/{_id}")))
            await IrAsync("..");
    }
}
