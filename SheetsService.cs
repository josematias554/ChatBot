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

    private const string SHEET_PEDIDOS    = "Pedidos";
    private const string SHEET_ITEMS      = "Items_Pedidos";
    private const string SHEET_PROVEEDORES = "Proveedores";

    public GoogleSheetsService(string credentialsPath, string spreadsheetId, ILogger<GoogleSheetsService> logger)
    {
        _spreadsheetId = spreadsheetId;
        _logger        = logger;

        var credential = GoogleCredential
            .FromFile(credentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheets = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "QuillenBot"
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
        var range    = $"{hoja}!A1:Z1";
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
        var range    = $"{hoja}!A:A";
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
            var range    = $"{SHEET_PEDIDOS}!A:A";
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
}
