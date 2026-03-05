using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using QuillenBot.Models;
using QuillenBot.Services;
using Microsoft.Extensions.Logging;

namespace QuillenBot.Handlers;

public class MessageHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly SessionManager      _sessions;
    private readonly GeminiService       _gemini;
    private readonly GoogleSheetsService _sheets;
    private readonly StockService        _stock;
    private readonly long                _approverChatId;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        ITelegramBotClient bot,
        SessionManager sessions,
        GeminiService gemini,
        GoogleSheetsService sheets,
        StockService stock,
        long approverChatId,
        ILogger<MessageHandler> logger)
    {
        _bot            = bot;
        _sessions       = sessions;
        _gemini         = gemini;
        _sheets         = sheets;
        _stock          = stock;
        _approverChatId = approverChatId;
        _logger         = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTRADA PRINCIPAL
    // ═══════════════════════════════════════════════════════════════════════
    public async Task HandleMessageAsync(Message message)
    {
        if (message.Text is null) return;

        var chatId = message.Chat.Id;
        var texto  = message.Text.Trim();

        _logger.LogInformation("[{ChatId}] Mensaje: {Texto}", chatId, texto);

        // Comando /start siempre reinicia
        if (texto.StartsWith("/start"))
        {
            await _sessions.EliminarSesionAsync(chatId);
            var session = await _sessions.ObtenerOCrearSesionAsync(chatId);
            await EnviarBienvenidaAsync(chatId, session);
            return;
        }

        // Comando /cancelar
        if (texto.StartsWith("/cancelar"))
        {
            await _sessions.EliminarSesionAsync(chatId);
            await _bot.SendMessage(chatId, "❌ Operación cancelada. Escribí /start para comenzar de nuevo.");
            return;
        }

        // Si es el aprobador usando comandos de gestión
        if (chatId == _approverChatId && texto.StartsWith("/"))
        {
            await HandleApproverCommandAsync(chatId, texto);
            return;
        }

        var ses = await _sessions.ObtenerOCrearSesionAsync(chatId);
        await DispatchStepAsync(ses, texto, chatId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BIENVENIDA
    // ═══════════════════════════════════════════════════════════════════════
    private async Task EnviarBienvenidaAsync(long chatId, ConversationSession session)
    {
        session.Step = ConversationStep.EsperandoTipo;

        var teclado = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🛒 Soy cliente mayorista", "tipo_cliente") },
            new[] { InlineKeyboardButton.WithCallbackData("🤝 Soy proveedor / ofrezco servicios", "tipo_proveedor") }
        });

        await _bot.SendMessage(
            chatId,
            "🍓 ¡Bienvenido a *Quillen Berries*!\n\nSomos productores y comercializadores de fruta fresca y congelada del norte argentino.\n\n¿Con qué puedo ayudarte hoy?",
            parseMode: ParseMode.Markdown,
            replyMarkup: teclado
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CALLBACK DE BOTONES INLINE
    // ═══════════════════════════════════════════════════════════════════════
    public async Task HandleCallbackAsync(CallbackQuery callback)
    {
        if (callback.Message is null) return;

        var chatId = callback.Message.Chat.Id;
        var data   = callback.Data ?? "";

        await _bot.AnswerCallbackQuery(callback.Id);

        var session = await _sessions.ObtenerOCrearSesionAsync(chatId);

        switch (data)
        {
            case "tipo_cliente":
                session.UserType = UserType.Cliente;
                session.Step     = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, "¡Perfecto! Para empezar, ¿cuál es tu *nombre completo*?", parseMode: ParseMode.Markdown);
                break;

            case "tipo_proveedor":
                session.UserType = UserType.Proveedor;
                session.Step     = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, "¡Bienvenido! ¿Cuál es tu *nombre completo*?", parseMode: ParseMode.Markdown);
                break;

            case "agregar_item":
                session.Step = ConversationStep.PidiendoProducto;
                await EnviarCatalogoAsync(chatId);
                break;

            case "finalizar_pedido":
                await PedirDireccionAsync(chatId, session);
                break;

            default:
                // Confirmaciones de aprobación/rechazo: formato "aprobar_ID" o "rechazar_ID"
                if (data.StartsWith("aprobar_") || data.StartsWith("rechazar_"))
                    await HandleApprovalCallbackAsync(chatId, data);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPATCH POR STEP
    // ═══════════════════════════════════════════════════════════════════════
    private async Task DispatchStepAsync(ConversationSession session, string texto, long chatId)
    {
        // Si el step es Inicio o EsperandoTipo, usar Gemini para clasificar
        if (session.Step == ConversationStep.Inicio || session.Step == ConversationStep.EsperandoTipo)
        {
            await ClasificarYRedirigirAsync(session, texto, chatId);
            return;
        }

        switch (session.Step)
        {
            // ── Compartidos ────────────────────────────────────────────
            case ConversationStep.PidiendoNombre:
            {
                var (ok, error) = Validador.ValidarNombre(texto);
                if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }
                session.NombreContacto = texto.Trim();
                session.ResetErrores();
                session.Step = ConversationStep.PidiendoEmpresa;
                await _bot.SendMessage(chatId, "¿Cuál es el nombre de tu *empresa*?", parseMode: ParseMode.Markdown);
                break;
            }

            case ConversationStep.PidiendoEmpresa:
            {
                var (ok, error) = Validador.ValidarEmpresa(texto);
                if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }
                session.Empresa = texto.Trim();
                session.ResetErrores();
                session.Step = ConversationStep.PidiendoTelefono;
                await _bot.SendMessage(chatId, "¿Cuál es tu *número de teléfono*?", parseMode: ParseMode.Markdown);
                break;
            }

            case ConversationStep.PidiendoTelefono:
            {
                var (ok, error) = Validador.ValidarTelefono(texto);
                if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }
                session.Telefono = texto.Trim();
                session.ResetErrores();
                session.Step = ConversationStep.PidiendoEmail;
                await _bot.SendMessage(chatId, "¿Cuál es tu *email*?", parseMode: ParseMode.Markdown);
                break;
            }

            case ConversationStep.PidiendoEmail:
            {
                var (ok, error) = Validador.ValidarEmail(texto);
                if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }
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
                    await _bot.SendMessage(chatId, "¿Qué *servicio o producto* ofrecés a Quillen Berries? (ej: logística, packaging, insumos, etc.)", parseMode: ParseMode.Markdown);
                }
                break;
            }

            // ── Flujo Cliente ──────────────────────────────────────────
            case ConversationStep.PidiendoProducto:
                await ProcesarProductoAsync(session, texto, chatId);
                break;

            case ConversationStep.PidiendoCantidad:
                await ProcesarCantidadAsync(session, texto, chatId);
                break;

            case ConversationStep.PreguntandoMasItems:
                if (texto.ToLower() is "sí" or "si" or "s" or "yes")
                {
                    session.Step = ConversationStep.PidiendoProducto;
                    await EnviarCatalogoAsync(chatId);
                }
                else
                {
                    await PedirDireccionAsync(chatId, session);
                }
                break;

            case ConversationStep.PidiendoDireccion:
            {
                var (ok, error) = Validador.ValidarDireccion(texto);
                if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }
                session.DireccionEntrega = texto.Trim();
                session.ResetErrores();
                session.Step = ConversationStep.ConfirmandoPedido;
                await MostrarResumenPedidoAsync(chatId, session);
                break;
            }

            case ConversationStep.ConfirmandoPedido:
                if (texto.ToLower() is "confirmar" or "sí" or "si" or "s" or "ok")
                    await FinalizarPedidoAsync(chatId, session);
                else
                    await _bot.SendMessage(chatId, "¿Querés confirmar el pedido? Respondé *confirmar* para proceder o /cancelar para cancelar.", parseMode: ParseMode.Markdown);
                break;

            // ── Flujo Proveedor ────────────────────────────────────────
            case ConversationStep.PidiendoServicio:
                session.ServicioOfrecido = texto;
                session.Step             = ConversationStep.PidiendoDescripcionServicio;
                await _bot.SendMessage(chatId, "Contanos un poco más: *¿qué incluye el servicio?* (capacidad, cobertura, condiciones, etc.)", parseMode: ParseMode.Markdown);
                break;

            case ConversationStep.PidiendoDescripcionServicio:
                session.DescripcionServicio = texto;
                session.Step                = ConversationStep.ConfirmandoProveedor;
                await MostrarResumenProveedorAsync(chatId, session);
                break;

            case ConversationStep.ConfirmandoProveedor:
                if (texto.ToLower() is "confirmar" or "sí" or "si" or "s" or "ok")
                    await FinalizarRegistroProveedorAsync(chatId, session);
                else
                    await _bot.SendMessage(chatId, "Respondé *confirmar* para enviar tus datos o /cancelar para cancelar.", parseMode: ParseMode.Markdown);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLASIFICACIÓN CON GEMINI
    // ═══════════════════════════════════════════════════════════════════════
    private async Task ClasificarYRedirigirAsync(ConversationSession session, string texto, long chatId)
    {
        await _bot.SendChatAction(chatId, ChatAction.Typing);
        var clasificacion = await _gemini.ClasificarUsuarioAsync(texto);

        session.UserType = clasificacion.Tipo;

        switch (clasificacion.Tipo)
        {
            case UserType.Cliente:
                session.Step = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, "Entiendo que sos cliente mayorista 🛒\n\n¿Cuál es tu *nombre completo*?", parseMode: ParseMode.Markdown);
                break;

            case UserType.Proveedor:
                session.Step = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, "Entiendo que querés ofrecer un servicio a Quillen 🤝\n\n¿Cuál es tu *nombre completo*?", parseMode: ParseMode.Markdown);
                break;

            default:
                await EnviarBienvenidaAsync(chatId, session);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MANEJO DE ERRORES DE VALIDACIÓN
    // ═══════════════════════════════════════════════════════════════════════
    private async Task EnviarErrorAsync(long chatId, ConversationSession session, string mensajeError)
    {
        session.ErroresConsecutivos++;

        if (session.SuperoLimiteErrores())
        {
            await _sessions.EliminarSesionAsync(chatId);
            await _bot.SendMessage(chatId,
                "❌ *Demasiados intentos fallidos.*\n\nSe canceló la operación. Escribí /start para comenzar de nuevo o contactá a Quillen Berries directamente.",
                parseMode: ParseMode.Markdown);
            return;
        }

        var intentosRestantes = ConversationSession.MaxErrores - session.ErroresConsecutivos;
        await _bot.SendMessage(chatId,
            $"{mensajeError}\n\n_Intentos restantes: {intentosRestantes}_",
            parseMode: ParseMode.Markdown);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS: CLIENTE
    // ═══════════════════════════════════════════════════════════════════════
    private async Task EnviarCatalogoAsync(long chatId)
    {
        await _bot.SendMessage(
            chatId,
            Catalogo.TextoCatalogo() + "\n¿Qué producto querés agregar?",
            parseMode: ParseMode.Markdown
        );
    }

    private async Task ProcesarProductoAsync(ConversationSession session, string texto, long chatId)
    {
        // Primero búsqueda directa en catálogo
        var (encontrado, nombreReal, precio) = Catalogo.BuscarProducto(texto);

        // Si no encontrado, usar Gemini
        if (!encontrado)
        {
            await _bot.SendChatAction(chatId, ChatAction.Typing);
            var sugerido = await _gemini.InterpretarProductoAsync(texto);
            (encontrado, nombreReal, precio) = Catalogo.BuscarProducto(sugerido);
        }

        if (!encontrado)
        {
            await _bot.SendMessage(chatId,
                $"❌ No encontré *{texto}* en nuestro catálogo.\n\n{Catalogo.TextoCatalogo()}\n¿Cuál querés pedir?",
                parseMode: ParseMode.Markdown);
            return;
        }

        // Guardamos el producto temporal
        session.Items.Add(new OrderItem { Producto = nombreReal, PrecioUnitario = precio });
        session.Step = ConversationStep.PidiendoCantidad;
        await _bot.SendMessage(chatId,
            $"*{nombreReal}* — ${precio:N0}/kg ✅\n¿Cuántos *kilos* necesitás?",
            parseMode: ParseMode.Markdown);
    }

    private async Task ProcesarCantidadAsync(ConversationSession session, string texto, long chatId)
    {
        var (ok, cantidad, error) = Validador.ValidarCantidad(texto);
        if (!ok) { await EnviarErrorAsync(chatId, session, error); return; }

        var lastItem = session.Items.Last();

        // Validar contra stock disponible
        var (disponible, stockActual, mensajeStock) = await _stock.ConsultarStockAsync(lastItem.Producto, cantidad);
        if (!disponible)
        {
            await EnviarErrorAsync(chatId, session, mensajeStock);
            return;
        }

        lastItem.Cantidad = cantidad;
        session.ResetErrores();
        session.Step = ConversationStep.PreguntandoMasItems;

        var teclado = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Agregar otro producto", "agregar_item") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Finalizar pedido", "finalizar_pedido") }
        });

        await _bot.SendMessage(chatId,
            $"Agregado: *{lastItem.Producto}* x {cantidad}kg = ${lastItem.Subtotal:N0}\n\n¿Querés agregar más productos?",
            parseMode: ParseMode.Markdown,
            replyMarkup: teclado);
    }

    private async Task PedirDireccionAsync(long chatId, ConversationSession session)
    {
        session.Step = ConversationStep.PidiendoDireccion;
        await _bot.SendMessage(chatId, "📍 ¿Cuál es la *dirección de entrega*?", parseMode: ParseMode.Markdown);
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
        sb.AppendLine("\n¿Confirmás el pedido? Escribí *confirmar*");

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

        // Confirmar al cliente
        await _bot.SendMessage(chatId,
            $"✅ *¡Pedido #{id} recibido!*\n\nEsta en revisión. Te avisaremos cuando sea aprobado.\n\n_Quillen Berries te contactará para coordinar la entrega._",
            parseMode: ParseMode.Markdown);

        // Notificar al aprobador
        await NotificarAprobadorAsync(pedido);

        session.Step = ConversationStep.Finalizado;
        await _sessions.EliminarSesionAsync(chatId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS: PROVEEDOR
    // ═══════════════════════════════════════════════════════════════════════
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
        sb.AppendLine("\n¿Confirmás el envío de tus datos? Escribí *confirmar*");

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

        await _bot.SendMessage(chatId,
            $"✅ *¡Registro enviado!*\n\nTus datos fueron registrados (ID #{id}). El equipo de Quillen Berries te contactará a la brevedad.\n\n¡Gracias por comunicarte!",
            parseMode: ParseMode.Markdown);

        // Notificar al aprobador (informativo, no requiere acción)
        if (_approverChatId != 0)
        {
            await _bot.SendMessage(_approverChatId,
                $"📥 *Nuevo proveedor registrado #{id}*\n\n🏢 {proveedor.Empresa}\n👤 {proveedor.NombreContacto}\n🔧 {proveedor.ServicioOfrecido}\n📱 {proveedor.Telefono}\n📧 {proveedor.Email}",
                parseMode: ParseMode.Markdown);
        }

        session.Step = ConversationStep.Finalizado;
        await _sessions.EliminarSesionAsync(chatId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // APROBACIÓN DE PEDIDOS
    // ═══════════════════════════════════════════════════════════════════════
    private async Task NotificarAprobadorAsync(PedidoCliente pedido)
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

        var teclado = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData($"✅ Aprobar #{pedido.Id}", $"aprobar_{pedido.Id}"),
                InlineKeyboardButton.WithCallbackData($"❌ Rechazar #{pedido.Id}", $"rechazar_{pedido.Id}")
            }
        });

        await _bot.SendMessage(
            _approverChatId,
            sb.ToString(),
            parseMode: ParseMode.Markdown,
            replyMarkup: teclado
        );
    }

    private async Task HandleApprovalCallbackAsync(long approverChatId, string data)
    {
        var partes    = data.Split('_');
        var accion    = partes[0];
        var pedidoId  = int.Parse(partes[1]);

        var (pedido, chatIdCliente) = await _sessions.ObtenerPedidoPendienteAsync(pedidoId);

        if (pedido is null)
        {
            await _bot.SendMessage(approverChatId, $"⚠️ No se encontró el pedido #{pedidoId} en memoria. Ya fue procesado o el servidor se reinició.");
            return;
        }

        OrderStatus nuevoEstado;
        string mensajeCliente, mensajeAprobador;

        if (accion == "aprobar")
        {
            nuevoEstado       = OrderStatus.Aprobado;
            mensajeCliente    = $"🎉 *¡Tu pedido #{pedidoId} fue APROBADO!*\n\nEl equipo de Quillen Berries coordinará la entrega a {pedido.DireccionEntrega} a la brevedad. ¡Gracias por tu compra!";
            mensajeAprobador  = $"✅ Pedido #{pedidoId} *aprobado* correctamente.";
        }
        else
        {
            nuevoEstado       = OrderStatus.Rechazado;
            mensajeCliente    = $"❌ *Tu pedido #{pedidoId} fue rechazado.*\n\nEl equipo de Quillen se comunicará contigo para más información. Podés iniciar un nuevo pedido con /start.";
            mensajeAprobador  = $"❌ Pedido #{pedidoId} *rechazado*.";
        }

        await _sheets.ActualizarEstadoPedidoAsync(pedidoId, nuevoEstado);

        // Notificar cliente
        if (chatIdCliente != 0)
            await _bot.SendMessage(chatIdCliente, mensajeCliente, parseMode: ParseMode.Markdown);

        // Confirmar al aprobador
        await _bot.SendMessage(approverChatId, mensajeAprobador, parseMode: ParseMode.Markdown);

        await _sessions.RemoverPedidoPendienteAsync(pedidoId);
    }

    private async Task HandleApproverCommandAsync(long chatId, string comando)
    {
        // /aprobar_5 o /rechazar_5 por si prefieren comandos de texto
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
