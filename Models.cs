namespace QuillenBot.Models;

public enum UserType { Desconocido, Cliente, Proveedor }
public enum OrderStatus { Pendiente, Aprobado, Rechazado }

// ─── Sesión de conversación ────────────────────────────────────────────────
public class ConversationSession
{
    public long ChatId { get; set; }
    public UserType UserType { get; set; } = UserType.Desconocido;
    public ConversationStep Step { get; set; } = ConversationStep.Inicio;

    // Datos compartidos
    public string NombreContacto { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Solo clientes
    public List<OrderItem> Items { get; set; } = new();
    public string DireccionEntrega { get; set; } = string.Empty;

    // Solo proveedores
    public string ServicioOfrecido { get; set; } = string.Empty;
    public string DescripcionServicio { get; set; } = string.Empty;

    public DateTime InicioSesion { get; set; } = DateTime.Now;

    // Control de errores consecutivos
    public int ErroresConsecutivos { get; set; } = 0;
    public const int MaxErrores = 3;

    public void ResetErrores() => ErroresConsecutivos = 0;
    public bool SuperoLimiteErrores() => ErroresConsecutivos >= MaxErrores;
}

public enum ConversationStep
{
    Inicio,
    EsperandoTipo,          // Preguntando si es cliente o proveedor
    PidiendoNombre,
    PidiendoEmpresa,
    PidiendoTelefono,
    PidiendoEmail,

    // Cliente: flujo de pedido
    PidiendoProducto,
    PidiendoCantidad,
    ConfirmandoItem,
    PreguntandoMasItems,
    PidiendoDireccion,
    ConfirmandoPedido,

    // Proveedor
    PidiendoServicio,
    PidiendoDescripcionServicio,
    ConfirmandoProveedor,

    Finalizado
}

// ─── Pedido de cliente ─────────────────────────────────────────────────────
public class PedidoCliente
{
    public int Id { get; set; }
    public string NombreContacto { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string DireccionEntrega { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public long TelegramChatId { get; set; }

    public string ItemsResumen =>
        string.Join(", ", Items.Select(i => $"{i.Producto} x{i.Cantidad}kg"));
}

public class OrderItem
{
    public string Producto { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }       // en kg
    public decimal PrecioUnitario { get; set; } // por kg
    public decimal Subtotal => Cantidad * PrecioUnitario;

    // Temporal durante el flujo
    public string ProductoTemporal { get; set; } = string.Empty;
}

// ─── Registro de proveedor ─────────────────────────────────────────────────
public class RegistroProveedor
{
    public int Id { get; set; }
    public string NombreContacto { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ServicioOfrecido { get; set; } = string.Empty;
    public string DescripcionServicio { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public long TelegramChatId { get; set; }
}

// ─── Respuesta de clasificación de Gemini ─────────────────────────────────
public class ClasificacionUsuario
{
    public UserType Tipo { get; set; }
    public string Intencion { get; set; } = string.Empty;
}

// ─── Catálogo de productos ─────────────────────────────────────────────────
public static class Catalogo
{
    public static readonly Dictionary<string, decimal> Productos = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Frutilla",   850m  },
        { "Arándanos",  1200m },
        { "Moras",      950m  },
        { "Frambuesas", 1100m },
        { "Mango",      700m  },
        { "Piña",       600m  },
        { "Maracuyá",   800m  },
    };

    public static string TextoCatalogo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 *Catálogo de Quillen Berries* (precio por kg):\n");
        foreach (var (nombre, precio) in Productos)
            sb.AppendLine($"• {nombre}: ${precio:N0}/kg");
        return sb.ToString();
    }

    public static (bool encontrado, string nombreReal, decimal precio) BuscarProducto(string nombre)
    {
        // Normalizar el input (quitar acentos, minúsculas)
        var nombreNorm = QuillenBot.Services.Validador.NormalizarTexto(nombre);

        foreach (var (key, val) in Productos)
        {
            var keyNorm = QuillenBot.Services.Validador.NormalizarTexto(key);
            if (keyNorm.Contains(nombreNorm) || nombreNorm.Contains(keyNorm))
                return (true, key, val);
        }
        return (false, string.Empty, 0);
    }
}
