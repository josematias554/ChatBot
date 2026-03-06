using Microsoft.Extensions.Logging;

namespace QuillenBot.Services;


public class StockService
{
    private readonly ILogger<StockService> _logger;
    private Dictionary<string, decimal> _stock = new();
    private DateTime _ultimaActualizacion = DateTime.MinValue;

    private const int CACHE_MINUTOS = 15;

    private static readonly Dictionary<string, decimal> STOCK_TEMPORAL = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Frutilla",    999999m },
        { "Arándanos",   999999m },
        { "Moras",       999999m },
        { "Frambuesas",  999999m },
        { "Mango",       999999m },
        { "Piña",        999999m },
        { "Maracuyá",    999999m },
    };

    public StockService(ILogger<StockService> logger)
    {
        _logger = logger;
        _stock  = new Dictionary<string, decimal>(STOCK_TEMPORAL, StringComparer.OrdinalIgnoreCase);
        _logger.LogWarning("StockService usando valores TEMPORALES. Pendiente integración con Excel de Quillen Berries.");
    }

    // ─── Consulta de stock ─────────────────────────────────────────────────
    public async Task<(bool disponible, decimal stockActual, string mensaje)> ConsultarStockAsync(string producto, decimal cantidadSolicitada)
    {
        // Intentar recargar desde Excel si está configurado
        await IntentarRecargarAsync();

        if (!_stock.TryGetValue(producto, out var stockActual))
        {
            _logger.LogWarning("Producto '{Producto}' no encontrado en stock", producto);
            return (false, 0, $"⚠️ No encontré stock para *{producto}*. Contactá a Quillen directamente.");
        }

        if (stockActual == 999999m)
        {
            // Stock temporal — no validar cantidad, aceptar todo
            return (true, stockActual, string.Empty);
        }

        if (stockActual <= 0)
            return (false, 0, $"❌ *{producto}* no tiene stock disponible en este momento.");

        if (cantidadSolicitada > stockActual)
            return (false, stockActual,
                $"⚠️ Solo hay *{stockActual:N0} kg* disponibles de *{producto}*.\n¿Querés pedir esa cantidad o menos?");

        return (true, stockActual, string.Empty);
    }

    public decimal ObtenerStock(string producto)
    {
        return _stock.TryGetValue(producto, out var s) ? s : 0;
    }

    private const string _excelPath = ""; 

    private async Task IntentarRecargarAsync()
    {
        if (string.IsNullOrEmpty(_excelPath)) return;
        if ((DateTime.Now - _ultimaActualizacion).TotalMinutes < CACHE_MINUTOS) return;

        try
        {
            await CargarDesdeExcelAsync(_excelPath);
            _ultimaActualizacion = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recargando stock desde Excel. Usando valores anteriores.");
        }
    }

    private Task CargarDesdeExcelAsync(string rutaExcel)
    {
        //Implementar cuando la empresa provea el archivo
        // Ejemplo con ClosedXML (agregar al .csproj si se activa):
        //
        // using var wb = new XLWorkbook(rutaExcel);
        // var ws = wb.Worksheet(1);
        // var nuevoStock = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        //
        // foreach (var row in ws.RowsUsed().Skip(1)) // skip header
        // {
        //     var nombre = row.Cell(1).GetValue<string>().Trim();
        //     var cantidad = row.Cell(2).GetValue<decimal>();
        //     if (!string.IsNullOrEmpty(nombre))
        //         nuevoStock[nombre] = cantidad;
        // }
        //
        // _stock = nuevoStock;
        // _logger.LogInformation("Stock recargado desde Excel: {Count} productos", nuevoStock.Count);

        _logger.LogInformation("CargarDesdeExcelAsync pendiente de implementación");
        return Task.CompletedTask;
    }
}
