using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

public interface IReportesPdfService
{
    byte[] ComprobantePedido(PedidoDto pedido, string? clienteEmail, string? clienteDocumento);
    Task<byte[]> InventarioAsync(CancellationToken ct = default);
    Task<byte[]> VentasAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);
}

/// <summary>
/// Reportes en PDF generados con QuestPDF (licencia Community, gratuita para este uso).
/// Diseño claro y minimalista, coherente con la app.
/// </summary>
public class ReportesPdfService(AppDbContext db, IOptions<PagosOptions> pagos) : IReportesPdfService
{
    private static readonly CultureInfo Ar = new("es-AR");
    private const string Negro = "#111827";
    private const string Gris = "#6B7280";
    private const string Linea = "#E5E7EB";
    private const string FondoSuave = "#F9FAFB";

    private static string Moneda(decimal v) => v.ToString("C", Ar);
    private static DateTime HoraAr(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(-3);

    // ------------------------------------------------------------------
    // Comprobante de pedido (para el comprador)
    // ------------------------------------------------------------------
    public byte[] ComprobantePedido(PedidoDto p, string? clienteEmail, string? clienteDocumento)
    {
        var datosPago = pagos.Value;
        return Document.Create(doc => doc.Page(page =>
        {
            Pagina(page, "Comprobante de pedido", p.Codigo);

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("CLIENTE").FontSize(8).FontColor(Gris).LetterSpacing(0.08f);
                        c.Item().Text(p.Cliente).Bold();
                        if (!string.IsNullOrWhiteSpace(clienteDocumento)) c.Item().Text($"DNI/CUIT: {clienteDocumento}").FontColor(Gris);
                        if (!string.IsNullOrWhiteSpace(clienteEmail)) c.Item().Text(clienteEmail).FontColor(Gris);
                        if (!string.IsNullOrWhiteSpace(p.ClienteDireccion)) c.Item().Text(p.ClienteDireccion).FontColor(Gris);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("ESTADO").FontSize(8).FontColor(Gris).LetterSpacing(0.08f);
                        c.Item().AlignRight().Text(EstadosPedido.Texto(p.Estado)).Bold();
                        c.Item().AlignRight().Text(HoraAr(p.Fecha).ToString("dd/MM/yyyy HH:mm", Ar)).FontColor(Gris);
                    });
                });

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Element(Encabezado).Text("Producto");
                        h.Cell().Element(Encabezado).AlignRight().Text("Cant.");
                        h.Cell().Element(Encabezado).AlignRight().Text("Precio unit.");
                        h.Cell().Element(Encabezado).AlignRight().Text("Subtotal");
                    });
                    foreach (var i in p.Items)
                    {
                        t.Cell().Element(Celda).Text(i.Producto);
                        t.Cell().Element(Celda).AlignRight().Text(i.Cantidad.ToString());
                        t.Cell().Element(Celda).AlignRight().Text(Moneda(i.PrecioUnitario));
                        t.Cell().Element(Celda).AlignRight().Text(Moneda(i.Subtotal));
                    }
                });

                col.Item().AlignRight().Text(t =>
                {
                    t.Span("Total  ").FontColor(Gris);
                    t.Span(Moneda(p.Total)).FontSize(16).Bold();
                });

                if (p.Estado is EstadosPedido.Pendiente or EstadosPedido.EnRevision)
                {
                    col.Item().Background(FondoSuave).Border(1).BorderColor(Linea).Padding(12).Column(c =>
                    {
                        c.Spacing(3);
                        c.Item().Text("Datos para la transferencia").Bold();
                        c.Item().Text($"Alias: {datosPago.Alias}");
                        c.Item().Text($"CBU/CVU: {datosPago.Cbu}");
                        c.Item().Text($"Titular: {datosPago.Titular} · CUIT {datosPago.Cuit}");
                        c.Item().Text($"Banco: {datosPago.Banco}");
                        c.Item().PaddingTop(4).Text("Subí el comprobante desde la app (Mis pedidos) para que confirmemos tu compra.")
                            .FontColor(Gris).FontSize(9);
                    });
                }

                if (!string.IsNullOrWhiteSpace(p.Observaciones))
                    col.Item().Text($"Observaciones: {p.Observaciones}").FontColor(Gris);

                // Seguimiento: cada cambio de estado registrado en HistorialPedidos.
                if (p.Historial.Count > 0)
                {
                    col.Item().PaddingTop(4).Text("Seguimiento del pedido").Bold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2.4f); c.RelativeColumn(3); c.RelativeColumn(5); });
                        t.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Fecha");
                            h.Cell().Element(Encabezado).Text("Estado");
                            h.Cell().Element(Encabezado).Text("Detalle");
                        });
                        foreach (var h in p.Historial)
                        {
                            t.Cell().Element(Celda).Text(HoraAr(h.Fecha).ToString("dd/MM/yyyy HH:mm", Ar));
                            t.Cell().Element(Celda).Text(h.EstadoTexto);
                            t.Cell().Element(Celda).Text(h.Comentario ?? "").FontColor(Gris);
                        }
                    });
                }
            });
        })).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Inventario valorizado (admin)
    // ------------------------------------------------------------------
    public async Task<byte[]> InventarioAsync(CancellationToken ct = default)
    {
        var productos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Categoria.Nombre).ThenBy(p => p.Nombre)
            .Select(p => new { p.Nombre, Marca = p.Marca.Nombre, Categoria = p.Categoria.Nombre, p.Stock, p.StockMinimo, p.Precio })
            .ToListAsync(ct);

        var unidades = productos.Sum(p => p.Stock);
        var valor = productos.Sum(p => p.Stock * p.Precio);
        var reponer = productos.Count(p => p.Stock <= p.StockMinimo);

        return Document.Create(doc => doc.Page(page =>
        {
            Pagina(page, "Inventario valorizado", HoraAr(DateTime.UtcNow).ToString("dd/MM/yyyy HH:mm", Ar));

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);
                col.Item().Row(row =>
                {
                    Indicador(row.RelativeItem(), "Productos activos", productos.Count.ToString());
                    Indicador(row.RelativeItem(), "Unidades en stock", unidades.ToString("N0", Ar));
                    Indicador(row.RelativeItem(), "Valor a precio de venta", Moneda(valor));
                    Indicador(row.RelativeItem(), "Para reponer", reponer.ToString());
                });

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.4f);
                    });
                    t.Header(h =>
                    {
                        h.Cell().Element(Encabezado).Text("Producto");
                        h.Cell().Element(Encabezado).Text("Categoría");
                        h.Cell().Element(Encabezado).AlignRight().Text("Stock");
                        h.Cell().Element(Encabezado).AlignRight().Text("Mín.");
                        h.Cell().Element(Encabezado).AlignRight().Text("Precio");
                        h.Cell().Element(Encabezado).AlignRight().Text("Valorizado");
                    });
                    foreach (var p in productos)
                    {
                        var bajo = p.Stock <= p.StockMinimo;
                        t.Cell().Element(Celda).Text($"{p.Nombre} ({p.Marca})");
                        t.Cell().Element(Celda).Text(p.Categoria).FontColor(Gris);
                        t.Cell().Element(Celda).AlignRight().Text(p.Stock.ToString()).Bold().FontColor(bajo ? "#DC2626" : Negro);
                        t.Cell().Element(Celda).AlignRight().Text(p.StockMinimo.ToString()).FontColor(Gris);
                        t.Cell().Element(Celda).AlignRight().Text(Moneda(p.Precio));
                        t.Cell().Element(Celda).AlignRight().Text(Moneda(p.Stock * p.Precio));
                    }
                });

                col.Item().Text("Los productos con stock en rojo están en o por debajo del stock mínimo.").FontSize(8).FontColor(Gris);
            });
        })).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Ventas por período (admin). Fechas en hora de Argentina, "hasta" inclusive.
    // ------------------------------------------------------------------
    public async Task<byte[]> VentasAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = desde.Date.AddHours(3);
        var hastaUtc = hasta.Date.AddDays(1).AddHours(3);

        // Renglones de pedidos cobrados del período (DetallesPedido + Pedidos + Productos + Marcas + Clientes).
        var filas = await db.DetallesPedido.AsNoTracking()
            .Where(d => EstadosPedido.CobradosIds.Contains(d.Pedido.EstadoId) && d.Pedido.Fecha >= desdeUtc && d.Pedido.Fecha < hastaUtc)
            .Select(d => new
            {
                d.Pedido.Codigo, d.Pedido.Fecha, Cliente = d.Pedido.Cliente.Nombre, Estado = d.Pedido.Estado.Nombre,
                Producto = d.Producto.Nombre, Marca = d.Producto.Marca.Nombre,
                d.Cantidad, Total = d.Cantidad * d.PrecioUnitario
            })
            .ToListAsync(ct);

        var pedidos = filas.GroupBy(f => f.Codigo)
            .Select(g => new
            {
                Codigo = g.Key,
                Fecha = HoraAr(g.First().Fecha),
                g.First().Cliente,
                Unidades = g.Sum(x => x.Cantidad),
                Total = g.Sum(x => x.Total),
                g.First().Estado
            })
            .OrderBy(p => p.Fecha)
            .ToList();

        var top = filas.GroupBy(f => f.Producto)
            .Select(g => new { Producto = g.Key, Unidades = g.Sum(x => x.Cantidad), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Unidades).Take(5).ToList();

        var porMarca = filas.GroupBy(f => f.Marca)
            .Select(g => new { Marca = g.Key, Unidades = g.Sum(x => x.Cantidad), Total = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Total).ToList();

        var total = pedidos.Sum(p => p.Total);
        var ticket = pedidos.Count == 0 ? 0 : total / pedidos.Count;
        var periodo = $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

        return Document.Create(doc => doc.Page(page =>
        {
            Pagina(page, "Reporte de ventas", periodo);

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);
                col.Item().Row(row =>
                {
                    Indicador(row.RelativeItem(), "Facturación", Moneda(total));
                    Indicador(row.RelativeItem(), "Pedidos cobrados", pedidos.Count.ToString());
                    Indicador(row.RelativeItem(), "Ticket promedio", Moneda(ticket));
                    Indicador(row.RelativeItem(), "Unidades vendidas", pedidos.Sum(p => p.Unidades).ToString());
                });

                if (top.Count > 0)
                {
                    col.Item().Text("Productos más vendidos").Bold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(6); c.RelativeColumn(1.5f); c.RelativeColumn(2.5f); });
                        t.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Producto");
                            h.Cell().Element(Encabezado).AlignRight().Text("Unidades");
                            h.Cell().Element(Encabezado).AlignRight().Text("Total");
                        });
                        foreach (var x in top)
                        {
                            t.Cell().Element(Celda).Text(x.Producto);
                            t.Cell().Element(Celda).AlignRight().Text(x.Unidades.ToString());
                            t.Cell().Element(Celda).AlignRight().Text(Moneda(x.Total));
                        }
                    });
                }

                if (porMarca.Count > 0)
                {
                    col.Item().Text("Ventas por marca").Bold().FontSize(12);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1.5f); c.RelativeColumn(2.5f); c.RelativeColumn(1.5f); });
                        t.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Marca");
                            h.Cell().Element(Encabezado).AlignRight().Text("Unidades");
                            h.Cell().Element(Encabezado).AlignRight().Text("Total");
                            h.Cell().Element(Encabezado).AlignRight().Text("% ventas");
                        });
                        foreach (var x in porMarca)
                        {
                            t.Cell().Element(Celda).Text(x.Marca);
                            t.Cell().Element(Celda).AlignRight().Text(x.Unidades.ToString());
                            t.Cell().Element(Celda).AlignRight().Text(Moneda(x.Total));
                            t.Cell().Element(Celda).AlignRight().Text(total == 0 ? "-" : (x.Total / total).ToString("P0", Ar));
                        }
                    });
                }

                col.Item().Text("Detalle de pedidos").Bold().FontSize(12);
                if (pedidos.Count == 0)
                {
                    col.Item().Text("No hay ventas cobradas en el período.").FontColor(Gris);
                }
                else
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.4f);
                            c.RelativeColumn(2.6f);
                            c.RelativeColumn(4);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(2.4f);
                            c.RelativeColumn(2.4f);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Fecha");
                            h.Cell().Element(Encabezado).Text("Pedido");
                            h.Cell().Element(Encabezado).Text("Cliente");
                            h.Cell().Element(Encabezado).AlignRight().Text("Unid.");
                            h.Cell().Element(Encabezado).Text("Estado");
                            h.Cell().Element(Encabezado).AlignRight().Text("Total");
                        });
                        foreach (var p in pedidos)
                        {
                            t.Cell().Element(Celda).Text(p.Fecha.ToString("dd/MM HH:mm", Ar));
                            t.Cell().Element(Celda).Text(p.Codigo).FontColor(Gris);
                            t.Cell().Element(Celda).Text(p.Cliente);
                            t.Cell().Element(Celda).AlignRight().Text(p.Unidades.ToString());
                            t.Cell().Element(Celda).Text(p.Estado).FontColor(Gris);
                            t.Cell().Element(Celda).AlignRight().Text(Moneda(p.Total));
                        }
                    });
                }

                col.Item().Text("Se consideran ventas los pedidos con pago confirmado o entregados.").FontSize(8).FontColor(Gris);
            });
        })).GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Elementos comunes del diseño
    // ------------------------------------------------------------------
    private static void Pagina(PageDescriptor page, string titulo, string subtitulo)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Negro));

        page.Header().BorderBottom(1).BorderColor(Linea).PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("TechHub").FontSize(20).Bold().FontColor(Negro);
                c.Item().Text("Hardware y componentes de PC").FontSize(9).FontColor(Gris);
            });
            row.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text(titulo).FontSize(14).Bold();
                c.Item().AlignRight().Text(subtitulo).FontColor(Gris);
            });
        });

        page.Footer().BorderTop(1).BorderColor(Linea).PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text($"Generado el {HoraAr(DateTime.UtcNow):dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Gris);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Gris));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    private static void Indicador(IContainer contenedor, string titulo, string valor) =>
        contenedor.Background(FondoSuave).Border(1).BorderColor(Linea).Padding(10).Column(c =>
        {
            c.Item().Text(titulo).FontSize(8).FontColor(Gris);
            c.Item().Text(valor).FontSize(13).Bold();
        });

    private static IContainer Encabezado(IContainer c) =>
        c.BorderBottom(1).BorderColor(Negro).PaddingVertical(6).DefaultTextStyle(x => x.FontSize(8).Bold().FontColor(Gris));

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(1).BorderColor(Linea).PaddingVertical(6);
}
