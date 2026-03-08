using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using QuillenBot.Models;
using QuillenBot.Services;

namespace QuillenBot.Handlers;

/// <summary>
/// Maneja la notificación y resolución de aprobaciones de pedidos
/// y el registro de nuevos proveedores hacia el aprobador.
/// </summary>
public class ApprovalHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly SessionManager _sessions;
    private readonly GoogleSheetsService _sheets;
    private readonly long _approverChatId;

    public ApprovalHandler(
        ITelegramBotClient bot,
        SessionManager sessions,
        GoogleSheetsService sheets,
        long approverChatId)
    {
        _bot            = bot;
        _sessions       = sessions;
        _sheets         = sheets;
        _approverChatId = approverChatId;
    }

    // ── Notificaciones al aprobador ──────────────────────────────────────

    public async Task NotificarAprobadorAsync(PedidoCliente pedido)
    {
        if (_approverChatId == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🔔 *NUEVO PEDIDO #{pedido.Id} — REQUIERE APROBACIÓN*\n");
        sb.AppendLine($"🏢 Empresa: {pedido.Empresa}");
        sb.AppendLine($"👤 Contacto: {pedido.NombreContacto}");
        sb.AppendLine($"📱 Tel: {pedido.Telefono}");
        sb.AppendLine($"📧 Email: {pedido.Email}");
        sb.AppendLine($"📍 Entrega: {pedido.DireccionEntrega}\n");
        sb.AppendLine("*Productos:*");
        foreach (var item in pedido.Items)
            sb.AppendLine($"  • {item.Producto}: {item.Cantidad}kg = ${item.Subtotal:N0}");
        sb.AppendLine($"\n💰 *TOTAL: ${pedido.Total:N0}*");

        await _bot.SendMessage(
            _approverChatId,
            sb.ToString(),
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.AprobacionPedido(pedido.Id)
        );
    }

    public async Task NotificarNuevoProveedorAsync(RegistroProveedor proveedor)
    {
        if (_approverChatId == 0) return;

        await _bot.SendMessage(
            _approverChatId,
            $"📥 *Nuevo proveedor registrado*\n\n🏢 {proveedor.Empresa}\n👤 {proveedor.NombreContacto}\n🔧 {proveedor.ServicioOfrecido}\n📱 {proveedor.Telefono}\n📧 {proveedor.Email}",
            parseMode: ParseMode.Markdown
        );
    }

    // ── Procesamiento de aprobación/rechazo ──────────────────────────────

    public async Task HandleApprovalCallbackAsync(long approverChatId, string data)
    {
        var partes   = data.Split('_');
        var accion   = partes[0];
        var pedidoId = int.Parse(partes[1]);

        var (pedido, chatIdCliente) = await _sessions.ObtenerPedidoPendienteAsync(pedidoId);

        if (pedido is null)
        {
            await _bot.SendMessage(
                approverChatId,
                $"⚠️ No se encontró el pedido #{pedidoId} en memoria. Ya fue procesado o el servidor se reinició."
            );
            return;
        }

        OrderStatus nuevoEstado;
        string mensajeCliente, mensajeAprobador;

        if (accion == "aprobar")
        {
            nuevoEstado      = OrderStatus.Aprobado;
            mensajeCliente   = $"🎉 *¡Tu pedido #{pedidoId} fue APROBADO!*\n\nEl equipo coordinará la entrega a {pedido.DireccionEntrega} a la brevedad. ¡Gracias por tu compra!";
            mensajeAprobador = $"✅ Pedido #{pedidoId} *aprobado* correctamente.";
        }
        else
        {
            nuevoEstado      = OrderStatus.Rechazado;
            mensajeCliente   = $"❌ *Tu pedido #{pedidoId} fue rechazado.*\n\nNos comunicaremos contigo para más información.";
            mensajeAprobador = $"❌ Pedido #{pedidoId} *rechazado*.";
        }

        await _sheets.ActualizarEstadoPedidoAsync(pedidoId, nuevoEstado);

        if (chatIdCliente != 0)
            await _bot.SendMessage(chatIdCliente, mensajeCliente, parseMode: ParseMode.Markdown);

        await _bot.SendMessage(approverChatId, mensajeAprobador, parseMode: ParseMode.Markdown);
        await _sessions.RemoverPedidoPendienteAsync(pedidoId);
    }

    public async Task HandleApproverCommandAsync(long chatId, string comando)
    {
        if (comando.StartsWith("/aprobar_") || comando.StartsWith("/rechazar_"))
        {
            var data = comando.TrimStart('/');
            await HandleApprovalCallbackAsync(chatId, data);
        }
        else
        {
            await _bot.SendMessage(chatId, "Comandos disponibles:\n/aprobar_ID\n/rechazar_ID");
        }
    }
}
