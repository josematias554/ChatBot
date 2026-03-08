using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using QuillenBot.Models;
using QuillenBot.Services;
using Microsoft.Extensions.Logging;

namespace QuillenBot.Handlers;

/// <summary>
/// Punto de entrada del bot. Solo enruta mensajes y callbacks
/// a los handlers especializados correspondientes.
/// </summary>
public class MessageHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly SessionManager _sessions;
    private readonly PresentationHandler _presentation;
    private readonly DemoFlowHandler _demoFlow;
    private readonly ApprovalHandler _approval;
    private readonly long _approverChatId;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        ITelegramBotClient bot,
        SessionManager sessions,
        GeminiService gemini,
        GoogleSheetsService sheets,
        StockService stock,
        long approverChatId,
        string nombreEmpresa,
        string rubroEmpresa,
        ILogger<MessageHandler> logger)
    {
        _bot           = bot;
        _sessions      = sessions;
        _approverChatId = approverChatId;
        _logger        = logger;

        _approval     = new ApprovalHandler(bot, sessions, sheets, approverChatId);
        _presentation = new PresentationHandler(bot, sessions);
        _demoFlow     = new DemoFlowHandler(bot, sessions, gemini, sheets, stock, _approval,
                            logger as ILogger<DemoFlowHandler> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoFlowHandler>.Instance);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTRADA: MENSAJES DE TEXTO
    // ═══════════════════════════════════════════════════════════════════════
    public async Task HandleMessageAsync(Message message)
    {
        if (message.Text is null) return;

        var chatId = message.Chat.Id;
        var texto  = message.Text.Trim();

        _logger.LogInformation("[{ChatId}] Mensaje: {Texto}", chatId, texto);

        // Comandos globales
        if (texto.StartsWith("/start"))
        {
            await _sessions.EliminarSesionAsync(chatId);
            var session = await _sessions.ObtenerOCrearSesionAsync(chatId);
            await _presentation.EnviarPresentacionAsync(chatId, session);
            return;
        }

        if (texto.StartsWith("/cancelar"))
        {
            var ses = await _sessions.ObtenerOCrearSesionAsync(chatId);
            var idioma = ses.Idioma;
            await _sessions.EliminarSesionAsync(chatId);
            var nuevaSes = await _sessions.ObtenerOCrearSesionAsync(chatId);
            nuevaSes.Idioma = idioma;
            await _bot.SendMessage(chatId, BotTexts.Get(nuevaSes, "cancelar"));
            return;
        }

        // Comandos del aprobador
        if (chatId == _approverChatId && texto.StartsWith("/"))
        {
            await _approval.HandleApproverCommandAsync(chatId, texto);
            return;
        }

        var session2 = await _sessions.ObtenerOCrearSesionAsync(chatId);

        // Sin sesión activa → mostrar menú
        if (session2.Step == ConversationStep.Inicio)
        {
            await _presentation.EnviarPresentacionAsync(chatId, session2);
            return;
        }

        // Menú principal sin step activo → clasificar con Gemini
        if (session2.Step == ConversationStep.EsperandoTipo)
        {
            await _demoFlow.ClasificarYRedirigirAsync(session2, texto, chatId);
            return;
        }

        // Flujo del demo activo
        var handled = await _demoFlow.DispatchStepAsync(session2, texto, chatId);
        if (!handled)
            await _presentation.EnviarPresentacionAsync(chatId, session2);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTRADA: CALLBACKS DE BOTONES INLINE
    // ═══════════════════════════════════════════════════════════════════════
    public async Task HandleCallbackAsync(CallbackQuery callback)
    {
        if (callback.Message is null) return;

        var chatId  = callback.Message.Chat.Id;
        var data    = callback.Data ?? "";

        await _bot.AnswerCallbackQuery(callback.Id);

        var session = await _sessions.ObtenerOCrearSesionAsync(chatId);

        switch (data)
        {
            // ── Menú principal ───────────────────────────────────────────
            case "menu_demo":
                await _demoFlow.EnviarDemoIntroAsync(chatId, session);
                break;

            case "menu_proyectos":
                await _presentation.MostrarProyectosAsync(chatId, session);
                break;

            case "menu_contacto":
                await _presentation.MostrarContactoAsync(chatId, session);
                break;

            case "menu_idioma":
                await _presentation.MostrarSelectorIdiomaAsync(chatId, session);
                break;

            case "menu_volver":
                session.Step = ConversationStep.EsperandoTipo;
                await _presentation.EnviarPresentacionAsync(chatId, session);
                break;

            // ── Idiomas ──────────────────────────────────────────────────
            case "idioma_es":
                session.Idioma = "es";
                await _presentation.EnviarPresentacionAsync(chatId, session);
                break;

            case "idioma_en":
                session.Idioma = "en";
                await _presentation.EnviarPresentacionAsync(chatId, session);
                break;

            case "idioma_pt":
                session.Idioma = "pt";
                await _presentation.EnviarPresentacionAsync(chatId, session);
                break;

            // ── Demo: tipo de usuario ────────────────────────────────────
            case "tipo_cliente":
                session.UserType = UserType.Cliente;
                session.Step     = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_nombre"), parseMode: ParseMode.Markdown);
                break;

            case "tipo_proveedor":
                session.UserType = UserType.Proveedor;
                session.Step     = ConversationStep.PidiendoNombre;
                await _bot.SendMessage(chatId, BotTexts.Get(session, "pedir_nombre"), parseMode: ParseMode.Markdown);
                break;

            // ── Demo: navegación de pedido ───────────────────────────────
            case "agregar_item":
                session.Step = ConversationStep.PidiendoProducto;
                await _bot.SendMessage(
                    chatId,
                    Catalogo.TextoCatalogo() + "\n¿Qué producto querés agregar?",
                    parseMode: ParseMode.Markdown
                );
                break;

            case "finalizar_pedido":
                await _demoFlow.PedirDireccionAsync(chatId, session);
                break;

            // ── Aprobaciones ─────────────────────────────────────────────
            default:
                if (data.StartsWith("aprobar_") || data.StartsWith("rechazar_"))
                    await _approval.HandleApprovalCallbackAsync(chatId, data);
                break;
        }
    }
}
