using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using QuillenBot.Models;
using QuillenBot.Services;

namespace QuillenBot.Handlers;

/// <summary>
/// Maneja la presentación personal del bot: bienvenida, menú principal
/// y las secciones informativas (proyectos, contacto, idioma).
/// </summary>
public class PresentationHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly SessionManager _sessions;

    private const string FOTO_PERFIL = "perfil.jpg";

    public PresentationHandler(ITelegramBotClient bot, SessionManager sessions)
    {
        _bot      = bot;
        _sessions = sessions;
    }

    // ── Menú principal ───────────────────────────────────────────────────

    public async Task EnviarPresentacionAsync(long chatId, ConversationSession session)
    {
        session.Step = ConversationStep.EsperandoTipo;
        var teclado  = MenuBuilder.MenuPrincipal(session);

        if (File.Exists(FOTO_PERFIL))
        {
            await using var foto = File.OpenRead(FOTO_PERFIL);
            await _bot.SendPhoto(
                chatId,
                InputFile.FromStream(foto, "perfil.jpg"),
                caption: BotTexts.Get(session, "bienvenida"),
                parseMode: ParseMode.Markdown,
                replyMarkup: teclado
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                BotTexts.Get(session, "bienvenida"),
                parseMode: ParseMode.Markdown,
                replyMarkup: teclado
            );
        }
    }

    // ── Secciones del menú ───────────────────────────────────────────────

    public async Task MostrarProyectosAsync(long chatId, ConversationSession session) =>
        await _bot.SendMessage(
            chatId,
            BotTexts.Get(session, "proyectos"),
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.BtnVolver(session)
        );

    public async Task MostrarContactoAsync(long chatId, ConversationSession session) =>
        await _bot.SendMessage(
            chatId,
            BotTexts.Get(session, "contacto_humano"),
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.BtnVolver(session)
        );

    public async Task MostrarSelectorIdiomaAsync(long chatId, ConversationSession session) =>
        await _bot.SendMessage(
            chatId,
            BotTexts.Get(session, "elegir_idioma"),
            parseMode: ParseMode.Markdown,
            replyMarkup: MenuBuilder.SelectorIdioma()
        );
}
