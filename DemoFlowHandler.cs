using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using QuillenBot.Models;
using QuillenBot.Services;
using Microsoft.Extensions.Logging;

namespace QuillenBot.Handlers;

/// <summary>
/// Maneja el flujo completo del demo: registro de clientes mayoristas
/// y proveedores, incluyendo validaciones y pasos conversacionales.
/// </summary>
public class DemoFlowHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly SessionManager _sessions;
    private readonly GeminiService _gemini;
    private readonly GoogleSheetsService _sheets;
    private readonly StockService _stock;
    private readonly ApprovalHandler _approval;
    private readonly ILogger<DemoFlowHandler> _logger;

    public DemoFlowHandler(
        ITelegramBotClient bot,
        SessionManager sessions,
        GeminiService gemini,
        GoogleSheetsService sheets,
        StockService stock,
        ApprovalHandler approval,
        ILogger<DemoFlowHandler> logger)
    {
        _bot      = bot;
        _sessions = sessions;
        _gemini   = gemini;
        _sheets   = sheets;
        _stock    = stock;
        _approval = approval;
        _logger   = logger;
    }

    // ── Intro demo ───────────────────────────────────────────────────────

    public async Task EnviarDemoIntroAsync(long chatId, ConversationSession session)
    {
        session.Step = ConversationStep.EsperandoTipo;
        await _bot.SendMessage(
            chatId,
            BotTexts.Get(session, "demo_intro"),
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.TipoUsuario(session)
        );
    }

    // ── Clasificación con Gemini ─────────────────────────────────────────

    public async Task ClasificarYRedirigirAsync(ConversationSession session, string texto, long chatId)
    {
        await _bot.SendChatAction(chatId, ChatAction.Typing);
        var clasificacion = await _gemini.ClasificarUsuarioAsync(texto);
        session.UserType = clasificacion.Tipo;

        switch (clasificacion.Tipo)
        {
            case UserType.Cliente:
            case UserType.Proveedor:
                session.Step = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_nombre"), parseMode: ParseMode.Markdown);
                break;

            default:
                // Texto no clasificable → volver al menú (lo delega el MessageHandler)
                break;
        }
    }

    // ── Dispatch de pasos ────────────────────────────────────────────────

    public async Task<bool> DispatchStepAsync(ConversationSession session, string texto, long chatId)
    {
        switch (session.Step)
        {
            case ConversationStep.PidiendoNombre:
                return await ProcesarNombreAsync(session, texto, chatId);

            case ConversationStep.PidiendoEmpresa:
                return await ProcesarEmpresaAsync(session, texto, chatId);

            case ConversationStep.PidiendoTelefono:
                return await ProcesarTelefonoAsync(session, texto, chatId);

            case ConversationStep.PidiendoEmail:
                return await ProcesarEmailAsync(session, texto, chatId);

            case ConversationStep.PidiendoProducto:
                await ProcesarProductoAsync(session, texto, chatId);
                return true;

            case ConversationStep.PidiendoCantidad:
                await ProcesarCantidadAsync(session, texto, chatId);
                return true;

            case ConversationStep.PreguntandoMasItems:
                await ProcesarMasItemsAsync(session, texto, chatId);
                return true;

            case ConversationStep.PidiendoDireccion:
                return await ProcesarDireccionAsync(session, texto, chatId);

            case ConversationStep.ConfirmandoPedido:
                await ProcesarConfirmacionPedidoAsync(session, texto, chatId);
                return true;

            case ConversationStep.PidiendoServicio:
                await ProcesarServicioAsync(session, texto, chatId);
                return true;

            case ConversationStep.PidiendoDescripcionServicio:
                await ProcesarDescripcionServicioAsync(session, texto, chatId);
                return true;

            case ConversationStep.ConfirmandoProveedor:
                await ProcesarConfirmacionProveedorAsync(session, texto, chatId);
                return true;

            default:
                return false;
        }
    }

    // ── Pasos compartidos (nombre, empresa, teléfono, email) ─────────────

    private async Task<bool> ProcesarNombreAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, error) = Validador.ValidarNombre(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return true; }
        session.NombreContacto = texto.Trim();
        session.ResetErrores();
        session.Step = ConversationStep.PidiendoEmpresa;
        await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_empresa"), parseMode: ParseMode.Markdown);
        return true;
    }

    private async Task<bool> ProcesarEmpresaAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, error) = Validador.ValidarEmpresa(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return true; }
        session.Empresa = texto.Trim();
        session.ResetErrores();
        session.Step = ConversationStep.PidiendoTelefono;
        await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_telefono"), parseMode: ParseMode.Markdown);
        return true;
    }

    private async Task<bool> ProcesarTelefonoAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, error) = Validador.ValidarTelefono(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return true; }
        session.Telefono = texto.Trim();
        session.ResetErrores();
        session.Step = ConversationStep.PidiendoEmail;
        await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_email"), parseMode: ParseMode.Markdown);
        return true;
    }

    private async Task<bool> ProcesarEmailAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, error) = Validador.ValidarEmail(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return true; }
        session.Email = texto.Trim();
        session.ResetErrores();

        if (session.UserType == UserType.Cliente)
        {
            session.Step = ConversationStep.PidiendoProducto;
            await _bot.SendMessage(chatId, $"Genial, {session.NombreContacto.Split(' ')[0]}. Ahora armémos tu pedido 🛒");
            await EnviarCatalogoAsync(chatId);
        }
        else
        {
            session.Step = ConversationStep.PidiendoServicio;
            await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_servicio"), parseMode: ParseMode.Markdown);
        }
        return true;
    }

    // ── Pasos cliente: producto, cantidad, dirección, confirmación ────────

    private async Task EnviarCatalogoAsync(long chatId) =>
        await _bot.SendMessage(
            chatId,
            Catalogo.TextoCatalogo() + "\n¿Qué producto querés agregar?",
            parseMode: ParseMode.Markdown
        );

    private async Task ProcesarProductoAsync(ConversationSession session, string texto, long chatId)
    {
        var (encontrado, nombreReal, precio) = Catalogo.BuscarProducto(texto);

        if (!encontrado)
        {
            await _bot.SendChatAction(chatId, ChatAction.Typing);
            var sugerido = await _gemini.InterpretarProductoAsync(texto);
            (encontrado, nombreReal, precio) = Catalogo.BuscarProducto(sugerido);
        }

        if (!encontrado)
        {
            await _bot.SendMessage(
                chatId,
                $"❌ No encontré *{texto}* en nuestro catálogo.\n\n{Catalogo.TextoCatalogo()}\n¿Cuál querés pedir?",
                parseMode: ParseMode.Markdown
            );
            return;
        }

        session.Items.Add(new OrderItem { Producto = nombreReal, PrecioUnitario = precio });
        session.Step = ConversationStep.PidiendoCantidad;
        await _bot.SendMessage(
            chatId,
            $"*{nombreReal}* — ${precio:N0}/kg ✅\n¿Cuántos *kilos* necesitás?",
            parseMode: ParseMode.Markdown
        );
    }

    private async Task ProcesarCantidadAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, cantidad, error) = Validador.ValidarCantidad(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }

        var lastItem = session.Items.Last();
        var (disponible, _, mensajeStock) = await _stock.ConsultarStockAsync(lastItem.Producto, cantidad);
        if (!disponible) { await EnviarErrorAsync(chatId, session, mensajeStock); return; }

        lastItem.Cantidad = cantidad;
        session.ResetErrores();
        session.Step = ConversationStep.PreguntandoMasItems;

        await _bot.SendMessage(
            chatId,
            $"Agregado: *{lastItem.Producto}* x {cantidad}kg = ${lastItem.Subtotal:N0}\n\n¿Querés agregar más productos?",
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.MasItemsOFinalizar()
        );
    }

    private async Task ProcesarMasItemsAsync(ConversationSession session, string texto, long chatId)
    {
        if (texto.ToLower() is "sí" or "si" or "s" or "yes" or "sim")
        {
            session.Step = ConversationStep.PidiendoProducto;
            await EnviarCatalogoAsync(chatId);
        }
        else
        {
            await PedirDireccionAsync(chatId, session);
        }
    }

    public async Task PedirDireccionAsync(long chatId, ConversationSession session)
    {
        session.Step = ConversationStep.PidiendoDireccion;
        await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_direccion"), parseMode: ParseMode.Markdown);
    }

    private async Task<bool> ProcesarDireccionAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, error) = Validador.ValidarDireccion(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return true; }
        session.DireccionEntrega = texto.Trim();
        session.ResetErrores();
        session.Step = ConversationStep.ConfirmandoPedido;
        await MostrarResumenPedidoAsync(chatId, session);
        return true;
    }

    private async Task ProcesarConfirmacionPedidoAsync(ConversationSession session, string texto, long chatId)
    {
        if (texto.ToLower() is "confirmar" or "confirm" or "sí" or "si" or "s" or "ok")
            await FinalizarPedidoAsync(chatId, session);
        else
            await _bot.SendMessage(chatId, BotTexts.Get(session, "confirmar"), parseMode: ParseMode.Markdown);
    }

    private async Task MostrarResumenPedidoAsync(long chatId, ConversationSession session)
    {
        var total = session.Items.Sum(i => i.Subtotal);
        var sb    = new System.Text.StringBuilder();

        sb.AppendLine("📋 *Resumen de tu pedido:*\n");
        sb.AppendLine($"👤 Contacto: {session.NombreContacto}");
        sb.AppendLine($"🏢 Empresa: {session.Empresa}");
        sb.AppendLine($"📱 Tel: {session.Telefono}");
        sb.AppendLine($"📧 Email: {session.Email}");
        sb.AppendLine($"📍 Dirección: {session.DireccionEntrega}\n");
        sb.AppendLine("*Productos:*");
        foreach (var item in session.Items)
            sb.AppendLine($"  • {item.Producto}: {item.Cantidad}kg × ${item.PrecioUnitario:N0} = *${item.Subtotal:N0}*");
        sb.AppendLine($"\n💰 *TOTAL: ${total:N0}*");
        sb.AppendLine($"\n{BotTexts.Get(session, "confirmar")}");

        await _bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown);
    }

    private async Task FinalizarPedidoAsync(long chatId, ConversationSession session)
    {
        var pedido = new PedidoCliente
        {
            NombreContacto   = session.NombreContacto,
            Empresa          = session.Empresa,
            Telefono         = session.Telefono,
            Email            = session.Email,
            Items            = session.Items,
            DireccionEntrega = session.DireccionEntrega,
            Total            = session.Items.Sum(i => i.Subtotal),
            Status           = OrderStatus.Pendiente,
            TelegramChatId   = chatId
        };

        var id = await _sheets.GuardarPedidoAsync(pedido);
        await _sessions.RegistrarPedidoPendienteAsync(pedido, chatId);

        await _bot.SendMessage(
            chatId,
            $"✅ *¡Pedido #{id} recibido!*\n\nEstá en revisión. Te avisaremos cuando sea aprobado.\n\n_Luego te contactaremos para coordinar la entrega._",
            parseMode: ParseMode.Markdown
        );

        await _approval.NotificarAprobadorAsync(pedido);

        session.Step = ConversationStep.Finalizado;
        await _sessions.EliminarSesionAsync(chatId);
    }

    // ── Pasos proveedor ──────────────────────────────────────────────────

    private async Task ProcesarServicioAsync(ConversationSession session, string texto, long chatId)
    {
        session.ServicioOfrecido = texto;
        session.Step = ConversationStep.PidiendoDescripcionServicio;
        await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_desc"), parseMode: ParseMode.Markdown);
    }

    private async Task ProcesarDescripcionServicioAsync(ConversationSession session, string texto, long chatId)
    {
        session.DescripcionServicio = texto;
        session.Step = ConversationStep.ConfirmandoProveedor;
        await MostrarResumenProveedorAsync(chatId, session);
    }

    private async Task ProcesarConfirmacionProveedorAsync(ConversationSession session, string texto, long chatId)
    {
        if (texto.ToLower() is "confirmar" or "confirm" or "sí" or "si" or "s" or "ok")
            await FinalizarRegistroProveedorAsync(chatId, session);
        else
            await _bot.SendMessage(chatId, BotTexts.Get(session, "confirmar"), parseMode: ParseMode.Markdown);
    }

    private async Task MostrarResumenProveedorAsync(long chatId, ConversationSession session)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 *Resumen de tu registro:*\n");
        sb.AppendLine($"👤 Nombre: {session.NombreContacto}");
        sb.AppendLine($"🏢 Empresa: {session.Empresa}");
        sb.AppendLine($"📱 Tel: {session.Telefono}");
        sb.AppendLine($"📧 Email: {session.Email}");
        sb.AppendLine($"🔧 Servicio: {session.ServicioOfrecido}");
        sb.AppendLine($"📝 Descripción: {session.DescripcionServicio}");
        sb.AppendLine($"\n{BotTexts.Get(session, "confirmar")}");
        await _bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown);
    }

    private async Task FinalizarRegistroProveedorAsync(long chatId, ConversationSession session)
    {
        var proveedor = new RegistroProveedor
        {
            NombreContacto      = session.NombreContacto,
            Empresa             = session.Empresa,
            Telefono            = session.Telefono,
            Email               = session.Email,
            ServicioOfrecido    = session.ServicioOfrecido,
            DescripcionServicio = session.DescripcionServicio,
            TelegramChatId      = chatId
        };

        var id = await _sheets.GuardarProveedorAsync(proveedor);

        await _bot.SendMessage(
            chatId,
            $"✅ *¡Registro enviado!*\n\nTus datos fueron registrados (ID #{id}). Te contactaremos a la brevedad.\n\n¡Gracias por comunicarte!",
            parseMode: ParseMode.Markdown
        );

        await _approval.NotificarNuevoProveedorAsync(proveedor);

        session.Step = ConversationStep.Finalizado;
        await _sessions.EliminarSesionAsync(chatId);
    }

    // ── Errores de validación ────────────────────────────────────────────

    private async Task EnviarErrorAsync(long chatId, ConversationSession session, string mensajeError)
    {
        session.ErroresConsecutivos++;

        if (session.SuperoLimiteErrores())
        {
            await _sessions.EliminarSesionAsync(chatId);
            await _bot.SendMessage(chatId, BotTexts.Get(session, "demasiados_errores"), parseMode: ParseMode.Markdown);
            return;
        }

        var intentosRestantes = ConversationSession.MaxErrores - session.ErroresConsecutivos;
        await _bot.SendMessage(
            chatId,
            $"{mensajeError}\n\n_{BotTexts.Get(session, "intentos")}: {intentosRestantes}_",
            parseMode: ParseMode.Markdown
        );
    }
}
