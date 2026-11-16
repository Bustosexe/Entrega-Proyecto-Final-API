namespace TechHub.App.ViewModels;

// =====================================================================
// ABM genérico: Categorías, Proveedores, Clientes y Usuarios comparten
// la misma pantalla de lista y de formulario, configuradas por definición.
// =====================================================================

public enum TipoCampo { Texto, Email, Telefono, Numero, Password, Switch }

/// <summary>Fila de la lista genérica.</summary>
public class AbmItem
{
    public int Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string Subtitulo { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    /// <summary>Texto completo para las listas de solo lectura (auditoría).</summary>
    public string Completo { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public string Etiqueta { get; init; } = string.Empty;
    public Color EtiquetaColor { get; init; } = Paleta.Gris;
    public JsonElement Datos { get; init; }

    public bool TieneEtiqueta => Etiqueta.Length > 0;
    public bool TieneDetalle => Detalle.Length > 0;
    public double Opacidad => Activo ? 1 : 0.55;
}

/// <summary>Campo del formulario genérico.</summary>
public partial class AbmCampo : ObservableObject
{
    public string Clave { get; init; } = string.Empty;
    public string Etiqueta { get; init; } = string.Empty;
    public TipoCampo Tipo { get; init; } = TipoCampo.Texto;
    public bool Requerido { get; init; }
    public bool SoloLectura { get; init; }
    /// <summary>Se muestra pero no se envía a la API (ej: nombre de usuario al editar).</summary>
    public bool NoEnviar { get; init; }
    public string Ayuda { get; init; } = string.Empty;
    public int Maximo { get; init; } = 150;

    [ObservableProperty] private string valor = string.Empty;
    [ObservableProperty] private bool valorBool;

    public string Rotulo => Requerido ? $"{Etiqueta} *" : Etiqueta;
    public bool EsSwitch => Tipo == TipoCampo.Switch;
    public bool EsTexto => !EsSwitch;
    public bool EsPassword => Tipo == TipoCampo.Password;
    public bool TieneAyuda => Ayuda.Length > 0;

    public Keyboard Teclado => Tipo switch
    {
        TipoCampo.Email => Keyboard.Email,
        TipoCampo.Telefono => Keyboard.Telephone,
        TipoCampo.Numero => Keyboard.Numeric,
        _ => Keyboard.Default
    };
}

public class AbmDefinicion
{
    public string Titulo { get; init; } = string.Empty;
    public string Singular { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string TextoBorrar { get; init; } = "Dar de baja";
    public required Func<JsonElement, AbmItem> Mapear { get; init; }
    /// <summary>Campos del formulario. Recibe el registro a editar (null = alta).</summary>
    public required Func<JsonElement?, List<AbmCampo>> Campos { get; init; }
    public Func<AbmItem, bool> PuedeBorrar { get; init; } = i => i.Activo;
    /// <summary>Lista sin alta ni edición (auditoría): al tocar un registro se muestra su detalle.</summary>
    public bool SoloLectura { get; init; }
    /// <summary>Ruta de un formulario propio (ofertas, combos). Null = formulario genérico.</summary>
    public string? RutaFormulario { get; init; }
    /// <summary>Parámetros extra de la consulta, por ejemplo "&incluirInactivos=true".</summary>
    public string QueryExtra { get; init; } = string.Empty;
}

public static class Abms
{
    private static string S(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool B(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;

    private static int I(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static decimal D(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;

    /// <summary>"2026-11-15" → "15/11/2026".</summary>
    private static string FechaCorta(string iso) =>
        DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy") : iso;

    private static string Unir(params string[] partes) => string.Join(" · ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static AbmCampo Campo(string clave, string etiqueta, JsonElement? datos, TipoCampo tipo = TipoCampo.Texto,
        bool requerido = false, string ayuda = "", int max = 150, bool soloLectura = false, bool noEnviar = false)
    {
        var c = new AbmCampo
        {
            Clave = clave, Etiqueta = etiqueta, Tipo = tipo, Requerido = requerido,
            Ayuda = ayuda, Maximo = max, SoloLectura = soloLectura, NoEnviar = noEnviar
        };
        if (tipo == TipoCampo.Switch)
            c.ValorBool = datos is null || B(datos.Value, clave); // en altas, activo = true
        else if (datos is not null)
            c.Valor = S(datos.Value, clave);
        return c;
    }

    private static List<AbmCampo> CamposUsuarioNuevo() =>
    [
        Campo("nombreUsuario", "Usuario", null, requerido: true, ayuda: "Letras, números, punto o guion. Mínimo 3.", max: 50),
        Campo("nombreCompleto", "Nombre completo", null, requerido: true),
        Campo("email", "Email", null, TipoCampo.Email, requerido: true),
        Campo("password", "Contraseña", null, TipoCampo.Password, requerido: true, ayuda: "Mínimo 6 caracteres.", max: 100)
    ];

    private static List<AbmCampo> CamposUsuarioEdicion(JsonElement? d) =>
    [
        Campo("nombreUsuario", "Usuario", d, soloLectura: true, noEnviar: true),
        Campo("nombreCompleto", "Nombre completo", d, requerido: true),
        Campo("email", "Email", d, TipoCampo.Email, requerido: true),
        Campo("nuevaPassword", "Nueva contraseña", d, TipoCampo.Password, ayuda: "Dejala vacía para no cambiarla.", max: 100),
        Campo("activo", "Activa", d, TipoCampo.Switch)
    ];

    public static AbmDefinicion Obtener(string tipo) => tipo switch
    {
        "categorias" => new AbmDefinicion
        {
            Titulo = "Categorías",
            Singular = "categoría",
            Endpoint = "api/categorias",
            TextoBorrar = "Eliminar",
            PuedeBorrar = i => I(i.Datos, "cantidadProductos") == 0,
            Mapear = e => new AbmItem
            {
                Id = I(e, "id"),
                Titulo = S(e, "nombre"),
                Subtitulo = S(e, "descripcion"),
                Detalle = $"{I(e, "cantidadProductos")} productos activos",
                Activo = B(e, "activo"),
                Etiqueta = B(e, "activo") ? "" : "INACTIVA",
                EtiquetaColor = Paleta.Gris,
                Datos = e
            },
            Campos = d =>
            [
                Campo("nombre", "Nombre", d, requerido: true, max: 100),
                Campo("descripcion", "Descripción", d, max: 300),
                Campo("activo", "Activa", d, TipoCampo.Switch)
            ]
        },

        "marcas" => new AbmDefinicion
        {
            Titulo = "Marcas",
            Singular = "marca",
            Endpoint = "api/marcas",
            TextoBorrar = "Eliminar",
            PuedeBorrar = i => I(i.Datos, "cantidadProductos") == 0,
            Mapear = e => new AbmItem
            {
                Id = I(e, "id"),
                Titulo = S(e, "nombre"),
                Subtitulo = I(e, "cantidadProductos") == 1 ? "1 producto activo" : $"{I(e, "cantidadProductos")} productos activos",
                Activo = B(e, "activo"),
                Etiqueta = B(e, "activo") ? "" : "INACTIVA",
                EtiquetaColor = Paleta.Gris,
                Datos = e
            },
            Campos = d =>
            [
                Campo("nombre", "Nombre", d, requerido: true, max: 80),
                Campo("activo", "Activa", d, TipoCampo.Switch)
            ]
        },

        "proveedores" => new AbmDefinicion
        {
            Titulo = "Proveedores",
            Singular = "proveedor",
            Endpoint = "api/proveedores",
            Mapear = e => new AbmItem
            {
                Id = I(e, "id"),
                Titulo = S(e, "razonSocial"),
                Subtitulo = $"CUIT {S(e, "cuit")}",
                Detalle = Unir(S(e, "email"), S(e, "telefono")),
                Activo = B(e, "activo"),
                Etiqueta = B(e, "activo") ? "" : "INACTIVO",
                Datos = e
            },
            Campos = d =>
            [
                Campo("razonSocial", "Razón social", d, requerido: true),
                Campo("cuit", "CUIT", d, TipoCampo.Numero, requerido: true, ayuda: "Formato: 30-12345678-9", max: 13),
                Campo("email", "Email", d, TipoCampo.Email),
                Campo("telefono", "Teléfono", d, TipoCampo.Telefono, max: 30),
                Campo("direccion", "Dirección", d, max: 200),
                Campo("activo", "Activo", d, TipoCampo.Switch)
            ]
        },

        "clientes" => new AbmDefinicion
        {
            Titulo = "Clientes",
            Singular = "cliente",
            Endpoint = "api/clientes",
            Mapear = e => new AbmItem
            {
                Id = I(e, "id"),
                Titulo = S(e, "nombre"),
                Subtitulo = $"DNI/CUIT {S(e, "documento")}",
                Detalle = Unir(S(e, "email"), S(e, "telefono")),
                Activo = B(e, "activo"),
                Etiqueta = B(e, "activo") ? "" : "INACTIVO",
                Datos = e
            },
            Campos = d =>
            [
                Campo("nombre", "Nombre o razón social", d, requerido: true),
                Campo("documento", "DNI o CUIT", d, TipoCampo.Numero, requerido: true, max: 13),
                Campo("email", "Email", d, TipoCampo.Email),
                Campo("telefono", "Teléfono", d, TipoCampo.Telefono, max: 30),
                Campo("direccion", "Dirección", d, max: 200),
                Campo("activo", "Activo", d, TipoCampo.Switch)
            ]
        },

        "usuarios" => new AbmDefinicion
        {
            Titulo = "Usuarios",
            Singular = "administrador",
            Endpoint = "api/usuarios",
            TextoBorrar = "Desactivar",
            Mapear = e => new AbmItem
            {
                Id = I(e, "id"),
                Titulo = S(e, "nombreCompleto"),
                Subtitulo = S(e, "email"),
                Detalle = B(e, "activo") ? "" : "Cuenta desactivada",
                Activo = B(e, "activo"),
                Etiqueta = S(e, "rol"),
                EtiquetaColor = S(e, "rol") == "Admin" ? Paleta.Info : Paleta.Gris,
                Datos = e
            },
            Campos = d => d is null ? CamposUsuarioNuevo() : CamposUsuarioEdicion(d)
        },

        "ofertas" => new AbmDefinicion
        {
            Titulo = "Ofertas",
            Singular = "oferta",
            Endpoint = "api/ofertas",
            RutaFormulario = "ofertaform",
            Mapear = e =>
            {
                var estado = S(e, "estado");
                return new AbmItem
                {
                    Id = I(e, "id"),
                    Titulo = S(e, "producto"),
                    Subtitulo = $"-{I(e, "porcentaje")}% · {FechaCorta(S(e, "desde"))} al {FechaCorta(S(e, "hasta"))}",
                    Detalle = $"{Formato.Moneda(D(e, "precioLista"))} → {Formato.Moneda(D(e, "precioOferta"))}",
                    Activo = estado is "Vigente" or "Programada",
                    Etiqueta = estado.ToUpperInvariant(),
                    EtiquetaColor = estado switch { "Vigente" => Paleta.Exito, "Programada" => Paleta.Info, _ => Paleta.Gris },
                    Datos = e
                };
            },
            Campos = _ => []
        },

        "combos" => new AbmDefinicion
        {
            Titulo = "Combos",
            Singular = "combo",
            Endpoint = "api/combos",
            QueryExtra = "&incluirInactivos=true",
            RutaFormulario = "comboform",
            TextoBorrar = "Dar de baja",
            Mapear = e =>
            {
                var activo = B(e, "activo");
                var items = e.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array ? arr.GetArrayLength() : 0;
                return new AbmItem
                {
                    Id = I(e, "id"),
                    Titulo = S(e, "nombre"),
                    Subtitulo = $"{items} productos · -{I(e, "porcentaje")}%",
                    Detalle = $"{Formato.Moneda(D(e, "precioLista"))} → {Formato.Moneda(D(e, "precioCombo"))}",
                    Activo = activo,
                    Etiqueta = !activo ? "INACTIVO" : B(e, "disponible") ? "" : "SIN STOCK",
                    EtiquetaColor = !activo ? Paleta.Gris : Paleta.Alerta,
                    Datos = e
                };
            },
            Campos = _ => []
        },

        "auditoria" => new AbmDefinicion
        {
            Titulo = "Auditoría",
            Singular = "registro",
            Endpoint = "api/auditoria",
            SoloLectura = true,
            Mapear = e =>
            {
                var usuario = S(e, "usuario");
                var fecha = e.TryGetProperty("fecha", out var f) && f.TryGetDateTime(out var dt) ? Formato.Fecha(dt) : "";
                var detalle = S(e, "detalle");
                var entidadId = S(e, "entidadId");
                var cabecera = Unir(S(e, "entidad"), entidadId.Length > 0 ? "#" + entidadId : "");
                return new AbmItem
                {
                    Id = (int)Math.Min(int.MaxValue, e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : 0),
                    Titulo = S(e, "accion"),
                    Subtitulo = Unir(cabecera, usuario.Length > 0 ? usuario : "Sin sesión"),
                    Detalle = detalle.Length > 90 ? detalle[..90] + "…" : detalle,
                    Completo = $"{fecha}\n{Unir(usuario.Length > 0 ? usuario : "Sin sesión", S(e, "rol"))}\n{cabecera}\nIP: {S(e, "ip")}\n\n{detalle}",
                    Etiqueta = fecha,
                    EtiquetaColor = Paleta.Gris,
                    Datos = e
                };
            },
            Campos = _ => []
        },

        _ => throw new ArgumentException($"Tipo de ABM desconocido: {tipo}")
    };
}

// ---------------------------------------------------------------------

public partial class AbmListaViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private string _tipo = string.Empty;
    private AbmDefinicion? _def;
    private int _pagina;
    private bool _hayMas;

    public ObservableCollection<AbmItem> Items { get; } = [];

    [ObservableProperty] private string buscar = string.Empty;
    [ObservableProperty] private string resultadoTexto = string.Empty;
    [ObservableProperty] private string mensajeVacio = "Cargando...";
    [ObservableProperty] private string textoNuevo = "Nuevo";
    [ObservableProperty] private bool puedeCrear = true;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Tipo", out var v) && v is string tipo)
        {
            _tipo = tipo;
            _def = Abms.Obtener(tipo);
            Titulo = _def.Titulo;
            TextoNuevo = $"+ Nuevo {_def.Singular}";
            PuedeCrear = !_def.SoloLectura;
        }
    }

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
    private Task NuevoAsync() =>
        IrAsync(_def?.RutaFormulario ?? "abmform", new Dictionary<string, object> { ["Tipo"] = _tipo });

    public Task AbrirAsync(AbmItem item)
    {
        if (_def?.SoloLectura == true) return Dialogos.Alerta(item.Titulo, item.Completo);
        return IrAsync(_def?.RutaFormulario ?? "abmform", new Dictionary<string, object> { ["Tipo"] = _tipo, ["Item"] = item });
    }

    private Task CargarAsync(int pagina) => EjecutarAsync(async () =>
    {
        if (_def is null) return;
        var url = $"{_def.Endpoint}?pagina={pagina}&tamanio=20{_def.QueryExtra}";
        if (!string.IsNullOrWhiteSpace(Buscar)) url += $"&buscar={Uri.EscapeDataString(Buscar.Trim())}";

        var r = await api.GetAsync<PagedResult<JsonElement>>(url);
        if (pagina == 1) Items.Clear();
        foreach (var e in r.Items) Items.Add(_def.Mapear(e));
        _pagina = r.Pagina;
        _hayMas = r.Pagina < r.TotalPaginas;
        ResultadoTexto = $"{r.TotalItems} registros";
        MensajeVacio = "No hay registros.";
    });
}

public partial class AbmFormViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private AbmDefinicion? _def;
    private AbmItem? _item;

    public ObservableCollection<AbmCampo> Campos { get; } = [];

    [ObservableProperty] private bool puedeBorrar;
    [ObservableProperty] private string textoBorrar = string.Empty;

    private bool _parametrosAplicados;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell vuelve a llamar a este método cuando se cierra una alerta o un selector; sin esta guarda
        // se pisaba lo que el usuario ya había escrito.
        if (_parametrosAplicados) return;
        _parametrosAplicados = true;

        if (!query.TryGetValue("Tipo", out var v) || v is not string tipo) return;
        _def = Abms.Obtener(tipo);
        _item = query.TryGetValue("Item", out var i) ? i as AbmItem : null;

        Titulo = _item is null ? $"Nuevo {_def.Singular}" : $"Editar {_def.Singular}";
        TextoBorrar = _def.TextoBorrar;
        PuedeBorrar = _item is not null && _def.PuedeBorrar(_item);

        Campos.Clear();
        foreach (var c in _def.Campos(_item?.Datos)) Campos.Add(c);
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (_def is null) return;

        var faltantes = Campos.Where(c => c.Requerido && c.EsTexto && string.IsNullOrWhiteSpace(c.Valor)).Select(c => c.Etiqueta).ToList();
        if (faltantes.Count > 0)
        {
            await Dialogos.Advertencia("Datos incompletos", "Completá: " + string.Join(", ", faltantes) + ".");
            return;
        }

        var cuerpo = new Dictionary<string, object?>();
        foreach (var c in Campos.Where(c => !c.NoEnviar))
            cuerpo[c.Clave] = c.EsSwitch
                ? c.ValorBool
                : string.IsNullOrWhiteSpace(c.Valor) ? null : (object)c.Valor.Trim();

        var ok = await EjecutarAsync(async () =>
        {
            if (_item is null) await api.PostAsync(_def.Endpoint, cuerpo);
            else await api.PutAsync($"{_def.Endpoint}/{_item.Id}", cuerpo);
        });

        if (ok)
        {
            Dialogos.Aviso("Datos guardados.");
            await IrAsync("..");
        }
    }

    [RelayCommand]
    private async Task BorrarAsync()
    {
        if (_def is null || _item is null) return;
        if (!await Dialogos.Confirmar(_def.TextoBorrar, $"¿{_def.TextoBorrar} \"{_item.Titulo}\"? El historial se conserva.", _def.TextoBorrar, "Cancelar", peligro: true))
            return;
        if (await EjecutarAsync(() => api.DeleteAsync($"{_def.Endpoint}/{_item.Id}")))
            await IrAsync("..");
    }
}
