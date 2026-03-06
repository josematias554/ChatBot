using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using QuillenBot.Models;
using Microsoft.Extensions.Logging;

namespace QuillenBot.Services;

public class GoogleSheetsService
{
    private readonly SheetsService _sheets;
    private readonly string _spreadsheetId;
    private readonly ILogger<GoogleSheetsService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string SHEET_PEDIDOS = "Pedidos";
    private const string SHEET_ITEMS = "Items_Pedidos";
    private const string SHEET_PROVEEDORES = "Proveedores";

    public GoogleSheetsService(string credentialsPath, string spreadsheetId, ILogger<GoogleSheetsService> logger)
    {
        _spreadsheetId = spreadsheetId;
        _logger = logger;

        var credential = GoogleCredential
            .FromFile(credentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheets = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "QuillenBot"
        });

        InicializarHojasAsync().GetAwaiter().GetResult();
    }

    // ─── Inicialización de hojas ───────────────────────────────────────────
    private async Task InicializarHojasAsync()
    {
        try
        {
            var spreadsheet = await _sheets.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            var hojasExistentes = spreadsheet.Sheets.Select(s => s.Properties.Title).ToHashSet();

            var requests = new List<Request>();

            // Crear hojas que no existen
            foreach (var hoja in new[] { SHEET_PEDIDOS, SHEET_ITEMS, SHEET_PROVEEDORES })
            {
                if (!hojasExistentes.Contains(hoja))
                {
                    requests.Add(new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties { Title = hoja }
                        }
                    });
                }
            }

            if (requests.Count > 0)
            {
                await _sheets.Spreadsheets.BatchUpdate(
                    new BatchUpdateSpreadsheetRequest { Requests = requests },
                    _spreadsheetId
                ).ExecuteAsync();
            }

            // Escribir encabezados si las hojas están vacías
            await EscribirEncabezadoSiVacioAsync(SHEET_PEDIDOS, new[]
            {
                "ID", "Fecha", "Empresa", "Contacto", "Teléfono", "Email",
                "Productos (Resumen)", "Total ($)", "Dirección Entrega", "Estado"
            });

            await EscribirEncabezadoSiVacioAsync(SHEET_ITEMS, new[]
            {
                "ID_Pedido", "Producto", "Cantidad (kg)", "Precio/kg ($)", "Subtotal ($)"
            });

            await EscribirEncabezadoSiVacioAsync(SHEET_PROVEEDORES, new[]
            {
                "ID", "Fecha", "Empresa", "Contacto", "Teléfono", "Email",
                "Servicio Ofrecido", "Descripción"
            });

            _logger.LogInformation("Google Sheets inicializado correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inicializando Google Sheets");
            throw;
        }
    }

    private async Task EscribirEncabezadoSiVacioAsync(string hoja, string[] headers)
    {
        var range = $"{hoja}!A1:Z1";
        var response = await _sheets.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();

        if (response.Values == null || response.Values.Count == 0)
        {
            var body = new ValueRange
            {
                Values = new List<IList<object>> { headers.Select(h => (object)h).ToList() }
            };
            var req = _sheets.Spreadsheets.Values.Update(body, _spreadsheetId, range);
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await req.ExecuteAsync();
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────
    private async Task<int> ObtenerProximoIdAsync(string hoja)
    {
        var range = $"{hoja}!A:A";
        var response = await _sheets.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
        // Filas: row 0 = header, resto = datos
        return (response.Values?.Count ?? 1); // id = cantidad de filas (sin header)
    }

    private async Task AppendRowAsync(string hoja, IList<object> valores)
    {
        var body = new ValueRange
        {
            Values = new List<IList<object>> { valores }
        };
        var req = _sheets.Spreadsheets.Values.Append(body, _spreadsheetId, $"{hoja}!A1");
        req.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
        req.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await req.ExecuteAsync();
    }

    // ─── Pedidos ───────────────────────────────────────────────────────────
    public async Task<int> GuardarPedidoAsync(PedidoCliente pedido)
    {
        await _lock.WaitAsync();
        try
        {
            var id = await ObtenerProximoIdAsync(SHEET_PEDIDOS);
            pedido.Id = id;

            // Fila resumen en Pedidos
            await AppendRowAsync(SHEET_PEDIDOS, new List<object>
            {
                pedido.Id,
                pedido.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                pedido.Empresa,
                pedido.NombreContacto,
                pedido.Telefono,
                pedido.Email,
                pedido.ItemsResumen,
                (double)pedido.Total,
                pedido.DireccionEntrega,
                pedido.Status.ToString()
            });

            // Filas detalle en Items_Pedidos
            foreach (var item in pedido.Items)
            {
                await AppendRowAsync(SHEET_ITEMS, new List<object>
                {
                    pedido.Id,
                    item.Producto,
                    (double)item.Cantidad,
                    (double)item.PrecioUnitario,
                    (double)item.Subtotal
                });
            }

            _logger.LogInformation("Pedido #{Id} guardado en Google Sheets", pedido.Id);
            return pedido.Id;
        }
        finally { _lock.Release(); }
    }

    public async Task ActualizarEstadoPedidoAsync(int pedidoId, OrderStatus nuevoEstado)
    {
        await _lock.WaitAsync();
        try
        {
            // Buscar la fila del pedido por ID
            var range = $"{SHEET_PEDIDOS}!A:A";
            var response = await _sheets.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();

            if (response.Values == null) return;

            for (int i = 0; i < response.Values.Count; i++)
            {
                var cell = response.Values[i].FirstOrDefault()?.ToString();
                if (cell == pedidoId.ToString())
                {
                    // Columna J = columna 10 = Estado
                    int fila = i + 1; // Google Sheets es 1-indexed
                    var updateRange = $"{SHEET_PEDIDOS}!J{fila}";
                    var body = new ValueRange
                    {
                        Values = new List<IList<object>> { new List<object> { nuevoEstado.ToString() } }
                    };
                    var req = _sheets.Spreadsheets.Values.Update(body, _spreadsheetId, updateRange);
                    req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                    await req.ExecuteAsync();

                    _logger.LogInformation("Pedido #{Id} actualizado a {Estado}", pedidoId, nuevoEstado);
                    break;
                }
            }
        }
        finally { _lock.Release(); }
    }

    // ─── Proveedores ───────────────────────────────────────────────────────
    public async Task<int> GuardarProveedorAsync(RegistroProveedor proveedor)
    {
        await _lock.WaitAsync();
        try
        {
            var id = await ObtenerProximoIdAsync(SHEET_PROVEEDORES);
            proveedor.Id = id;

            await AppendRowAsync(SHEET_PROVEEDORES, new List<object>
            {
                proveedor.Id,
                proveedor.FechaRegistro.ToString("dd/MM/yyyy HH:mm"),
                proveedor.Empresa,
                proveedor.NombreContacto,
                proveedor.Telefono,
                proveedor.Email,
                proveedor.ServicioOfrecido,
                proveedor.DescripcionServicio
            });

            _logger.LogInformation("Proveedor #{Id} - {Empresa} guardado en Google Sheets", proveedor.Id, proveedor.Empresa);
            return proveedor.Id;
        }
        finally { _lock.Release(); }
    }

    // ─── Datos de ejemplo para capturas ───────────────────────────────────────
    public async Task CargarEjemplosAsync()
    {
        Console.WriteLine("Cargando datos de ejemplo...");

        var pedidos = new List<IList<object>>
        {
            new List<object> { "ID", "Fecha", "Empresa", "Contacto", "Teléfono", "Email", "Productos (Resumen)", "Total ($)", "Dirección Entrega", "Estado" },
            new List<object> { 1, "02/05/2025 09:14", "Distribuidora Norte S.A.",   "Carlos Méndez",    "+54 381 4123456", "cmendes@distribnorte.com",  "Frutilla x50kg, Arándanos x20kg",    95000, "Av. Mate de Luna 2500, Tucumán",      "Aprobado"  },
            new List<object> { 2, "02/05/2025 11:32", "Supermercado El Familiar",   "Laura Gómez",      "+54 381 5234567", "lgomez@elfamiliar.com.ar",   "Mango x30kg, Piña x40kg",            46000, "Ruta 9 Km 12, Yerba Buena",           "Aprobado"  },
            new List<object> { 3, "03/05/2025 08:45", "Mayorista Don Pedro",        "Pedro Castillo",   "+54 385 6345678", "pcastillo@donpedro.com",     "Moras x25kg, Frambuesas x15kg",      40250, "San Martín 450, Concepción",          "Pendiente" },
            new List<object> { 4, "03/05/2025 14:20", "Frutería Los Andes",         "Valeria Torres",   "+54 381 7456789", "vtorres@losandes.com",       "Frutilla x100kg, Maracuyá x20kg",   101000, "Belgrano 890, San Miguel de Tucumán", "Aprobado"  },
            new List<object> { 5, "04/05/2025 10:05", "Supermercado Vital",         "Martín Ruiz",      "+54 381 8567890", "mruiz@supervital.com.ar",    "Arándanos x50kg, Moras x30kg",       88500, "Av. Roca 1200, Tafí Viejo",           "Rechazado" },
            new List<object> { 6, "04/05/2025 16:48", "Distribuidora del Sur",      "Ana Flores",       "+54 380 9678901", "aflores@distrsur.com",       "Mango x60kg, Frutilla x40kg",        76000, "España 340, Banda del Río Salí",      "Pendiente" },
            new List<object> { 7, "05/05/2025 09:30", "Mercado Central Tucumán",    "Jorge Soria",      "+54 381 1234567", "jsoria@mercadocentral.com",  "Piña x80kg, Maracuyá x35kg",         76000, "Mercado Central Local 45, Tucumán",   "Aprobado"  },
            new List<object> { 8, "05/05/2025 13:15", "Almacenes Mayoristas Díaz",  "Roberto Díaz",     "+54 381 2345678", "rdiaz@almacendiaz.com.ar",   "Frambuesas x20kg, Arándanos x30kg",  58000, "Los Sauces 780, Yerba Buena",         "Pendiente" },
        };

        var items = new List<IList<object>>
        {
            new List<object> { "ID_Pedido", "Producto", "Cantidad (kg)", "Precio/kg ($)", "Subtotal ($)" },
            new List<object> { 1, "Frutilla",   50,  850,  42500 },
            new List<object> { 1, "Arándanos",  20, 1200,  24000 },
            new List<object> { 2, "Mango",      30,  700,  21000 },
            new List<object> { 2, "Piña",       40,  600,  24000 },
            new List<object> { 3, "Moras",      25,  950,  23750 },
            new List<object> { 3, "Frambuesas", 15, 1100,  16500 },
            new List<object> { 4, "Frutilla",  100,  850,  85000 },
            new List<object> { 4, "Maracuyá",   20,  800,  16000 },
            new List<object> { 5, "Arándanos",  50, 1200,  60000 },
            new List<object> { 5, "Moras",      30,  950,  28500 },
            new List<object> { 6, "Mango",      60,  700,  42000 },
            new List<object> { 6, "Frutilla",   40,  850,  34000 },
            new List<object> { 7, "Piña",       80,  600,  48000 },
            new List<object> { 7, "Maracuyá",   35,  800,  28000 },
            new List<object> { 8, "Frambuesas", 20, 1100,  22000 },
            new List<object> { 8, "Arándanos",  30, 1200,  36000 },
        };

        var proveedores = new List<IList<object>>
        {
            new List<object> { "ID", "Fecha", "Empresa", "Contacto", "Teléfono", "Email", "Servicio Ofrecido", "Descripción" },
            new List<object> { 1, "01/05/2025 10:00", "LogiNorte S.R.L.",      "Santiago Vera",    "+54 381 3456789", "svera@loginorte.com",       "Logística y transporte", "Flota de 5 camiones refrigerados, cobertura NOA, entregas en 24hs."         },
            new List<object> { 2, "02/05/2025 15:30", "PackSur Packaging",     "Daniela Moreno",   "+54 381 4567890", "dmoreno@packsur.com.ar",    "Packaging y envases",    "Cajas, bandejas y film para frutas frescas y congeladas. Stock permanente." },
            new List<object> { 3, "04/05/2025 11:20", "AgroInsumos del Norte", "Felipe Gutiérrez", "+54 385 5678901", "fgutierrez@agroinsumos.com","Insumos agrícolas",       "Fertilizantes, fitosanitarios y riego. Asesoramiento técnico incluido."     },
        };

        await EscribirHojaCompletaAsync(SHEET_PEDIDOS, pedidos);
        await EscribirHojaCompletaAsync(SHEET_ITEMS, items);
        await EscribirHojaCompletaAsync(SHEET_PROVEEDORES, proveedores);

        Console.WriteLine("✅ Datos de ejemplo cargados. Abrí tu Google Sheets.");
    }

    private async Task EscribirHojaCompletaAsync(string hoja, List<IList<object>> datos)
    {
        await _sheets.Spreadsheets.Values.Clear(
            new Google.Apis.Sheets.v4.Data.ClearValuesRequest(),
            _spreadsheetId,
            $"{hoja}!A1:Z1000"
        ).ExecuteAsync();

        var body = new Google.Apis.Sheets.v4.Data.ValueRange { Values = datos };
        var req = _sheets.Spreadsheets.Values.Update(body, _spreadsheetId, $"{hoja}!A1");
        req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await req.ExecuteAsync();

        _logger.LogInformation("Hoja {Hoja}: {Count} filas cargadas", hoja, datos.Count - 1);
    }
}