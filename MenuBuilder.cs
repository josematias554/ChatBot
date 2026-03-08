using Telegram.Bot.Types.ReplyMarkups;
using QuillenBot.Models;

namespace QuillenBot.Handlers;

/// <summary>
/// Centraliza la construcción de todos los teclados inline del bot.
/// </summary>
public static class MenuBuilder
{
    public static InlineKeyboardMarkup MenuPrincipal(ConversationSession session) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_demo"),      "menu_demo")      },
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_proyectos"), "menu_proyectos") },
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_contacto"),  "menu_contacto")  },
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_idioma"),    "menu_idioma")    },
        });

    public static InlineKeyboardMarkup SelectorIdioma() =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🇦🇷 Español",   "idioma_es"),
                InlineKeyboardButton.WithCallbackData("🇺🇸 English",   "idioma_en"),
                InlineKeyboardButton.WithCallbackData("🇧🇷 Português", "idioma_pt"),
            }
        });

    public static InlineKeyboardMarkup TipoUsuario(ConversationSession session) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_cliente"),   "tipo_cliente")   },
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_proveedor"), "tipo_proveedor") },
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_volver"),    "menu_volver")    },
        });

    public static InlineKeyboardMarkup MasItemsOFinalizar() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Agregar otro producto", "agregar_item")    },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Finalizar pedido",       "finalizar_pedido") }
        });

    public static InlineKeyboardMarkup AprobacionPedido(int pedidoId) =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData($"✅ Aprobar #{pedidoId}",  $"aprobar_{pedidoId}"),
                InlineKeyboardButton.WithCallbackData($"❌ Rechazar #{pedidoId}", $"rechazar_{pedidoId}")
            }
        });

    public static InlineKeyboardMarkup BtnVolver(ConversationSession session) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(BotTexts.Get(session, "btn_volver"), "menu_volver") }
        });
}
