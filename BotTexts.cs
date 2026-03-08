using QuillenBot.Models;

namespace QuillenBot.Handlers;

/// <summary>
/// Centraliza todos los textos multiidioma del bot.
/// </summary>
public static class BotTexts
{
    private static readonly Dictionary<string, Dictionary<string, string>> Textos = new()
    {
        ["es"] = new()
        {
            ["bienvenida"]         = "👋 ¡Hola! Soy *José Matías*, desarrollador de software especializado en automatización y chatbots.\n\n¿En qué puedo ayudarte?",
            ["bio"]                = "👨‍💻 *José Matías*\nDesarrollador de Software\nEstudiante de Ing. en Sistemas — UTN FRT\n\n📍 Tucumán, Argentina\n\n✉️ jomatias108@gmail.com\n📱 3816525734\n\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)\n🐙 [GitHub](https://github.com/josematias554)",
            ["proyectos"]          = "🚀 *Mis proyectos*\n\n🤖 *Bot de gestión de pedidos*\nAutomatización completa para empresas mayoristas. Gestión de pedidos, proveedores y stock via Telegram + Google Sheets.\n_¡Estás probando este bot ahora mismo!_\n\n🍺 *App de bar* _(en desarrollo)_\nDivisión inteligente de pagos entre comensales. Calculá fácilmente cuánto paga cada uno.\n\n📌 Más proyectos próximamente...",
            ["contacto_humano"]    = "📞 *Contacto directo*\n\n✉️ jomatias108@gmail.com\n📱 WhatsApp/Tel: 3816525734\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)",
            ["demo_intro"]         = "🤖 *Demo — Bot de pedidos mayoristas*\n\nEste bot gestiona pedidos de clientes mayoristas y registros de proveedores de forma automática.\n\n¿Cómo querés continuar?",
            ["btn_demo"]           = "🤖 Ver demo del bot",
            ["btn_proyectos"]      = "📋 Mis proyectos",
            ["btn_contacto"]       = "📞 Contactarme",
            ["btn_idioma"]         = "🌐 Cambiar idioma",
            ["btn_volver"]         = "🔙 Volver al menú",
            ["btn_cliente"]        = "🛒 Soy cliente mayorista",
            ["btn_proveedor"]      = "🤝 Soy proveedor / ofrezco servicios",
            ["elegir_idioma"]      = "🌐 *Seleccioná tu idioma:*",
            ["cancelar"]           = "❌ Operación cancelada. Escribí cualquier mensaje para volver al menú.",
            ["pedir_nombre"]       = "¿Cuál es tu *nombre completo*?",
            ["pedir_empresa"]      = "¿Cuál es el nombre de tu *empresa*?",
            ["pedir_telefono"]     = "¿Cuál es tu *número de teléfono*?",
            ["pedir_email"]        = "¿Cuál es tu *email*?",
            ["pedir_direccion"]    = "📍 ¿Cuál es la *dirección de entrega*?",
            ["pedir_servicio"]     = "¿Qué *servicio o producto* ofrecés? (ej: logística, packaging, insumos, etc.)",
            ["pedir_desc"]         = "Contanos un poco más: *¿qué incluye el servicio?* (capacidad, cobertura, condiciones, etc.)",
            ["confirmar"]          = "¿Confirmás? Escribí *confirmar*",
            ["demasiados_errores"] = "❌ *Demasiados intentos fallidos.* Se canceló la operación. Escribí cualquier mensaje para volver al menú.",
            ["intentos"]           = "Intentos restantes",
        },
        ["en"] = new()
        {
            ["bienvenida"]         = "👋 Hi! I'm *José Matías*, a software developer specialized in automation and chatbots.\n\nHow can I help you?",
            ["bio"]                = "👨‍💻 *José Matías*\nSoftware Developer\nSystems Engineering Student — UTN FRT\n\n📍 Tucumán, Argentina\n\n✉️ jomatias108@gmail.com\n📱 3816525734\n\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)\n🐙 [GitHub](https://github.com/josematias554)",
            ["proyectos"]          = "🚀 *My projects*\n\n🤖 *Order management bot*\nFull automation for wholesale companies. Order, supplier and stock management via Telegram + Google Sheets.\n_You're testing this bot right now!_\n\n🍺 *Bar app* _(in development)_\nSmart bill splitting between diners. Easily calculate how much each person pays.\n\n📌 More projects coming soon...",
            ["contacto_humano"]    = "📞 *Direct contact*\n\n✉️ jomatias108@gmail.com\n📱 WhatsApp/Phone: 3816525734\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)",
            ["demo_intro"]         = "🤖 *Demo — Wholesale order bot*\n\nThis bot automatically handles wholesale customer orders and supplier registrations.\n\nHow would you like to continue?",
            ["btn_demo"]           = "🤖 Try the bot demo",
            ["btn_proyectos"]      = "📋 My projects",
            ["btn_contacto"]       = "📞 Contact me",
            ["btn_idioma"]         = "🌐 Change language",
            ["btn_volver"]         = "🔙 Back to menu",
            ["btn_cliente"]        = "🛒 I'm a wholesale client",
            ["btn_proveedor"]      = "🤝 I'm a supplier / offer services",
            ["elegir_idioma"]      = "🌐 *Select your language:*",
            ["cancelar"]           = "❌ Operation cancelled. Write any message to return to the menu.",
            ["pedir_nombre"]       = "What is your *full name*?",
            ["pedir_empresa"]      = "What is your *company name*?",
            ["pedir_telefono"]     = "What is your *phone number*?",
            ["pedir_email"]        = "What is your *email*?",
            ["pedir_direccion"]    = "📍 What is the *delivery address*?",
            ["pedir_servicio"]     = "What *service or product* do you offer? (e.g. logistics, packaging, supplies, etc.)",
            ["pedir_desc"]         = "Tell us more: *what does the service include?* (capacity, coverage, conditions, etc.)",
            ["confirmar"]          = "Do you confirm? Type *confirm*",
            ["demasiados_errores"] = "❌ *Too many failed attempts.* Operation cancelled. Write any message to return to the menu.",
            ["intentos"]           = "Remaining attempts",
        },
        ["pt"] = new()
        {
            ["bienvenida"]         = "👋 Olá! Sou *José Matías*, desenvolvedor de software especializado em automação e chatbots.\n\nComo posso ajudá-lo?",
            ["bio"]                = "👨‍💻 *José Matías*\nDesenvolvedor de Software\nEstudante de Eng. de Sistemas — UTN FRT\n\n📍 Tucumán, Argentina\n\n✉️ jomatias108@gmail.com\n📱 3816525734\n\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)\n🐙 [GitHub](https://github.com/josematias554)",
            ["proyectos"]          = "🚀 *Meus projetos*\n\n🤖 *Bot de gestão de pedidos*\nAutomação completa para empresas atacadistas. Gestão de pedidos, fornecedores e estoque via Telegram + Google Sheets.\n_Você está testando este bot agora mesmo!_\n\n🍺 *App de bar* _(em desenvolvimento)_\nDivisão inteligente de contas entre comensais. Calcule facilmente quanto cada pessoa paga.\n\n📌 Mais projetos em breve...",
            ["contacto_humano"]    = "📞 *Contato direto*\n\n✉️ jomatias108@gmail.com\n📱 WhatsApp/Tel: 3816525734\n🔗 [LinkedIn](https://www.linkedin.com/in/jose-matias-64194520b/)",
            ["demo_intro"]         = "🤖 *Demo — Bot de pedidos atacadistas*\n\nEste bot gerencia pedidos de clientes atacadistas e registros de fornecedores de forma automática.\n\nComo deseja continuar?",
            ["btn_demo"]           = "🤖 Testar demo do bot",
            ["btn_proyectos"]      = "📋 Meus projetos",
            ["btn_contacto"]       = "📞 Entrar em contato",
            ["btn_idioma"]         = "🌐 Mudar idioma",
            ["btn_volver"]         = "🔙 Voltar ao menu",
            ["btn_cliente"]        = "🛒 Sou cliente atacadista",
            ["btn_proveedor"]      = "🤝 Sou fornecedor / ofereço serviços",
            ["elegir_idioma"]      = "🌐 *Selecione seu idioma:*",
            ["cancelar"]           = "❌ Operação cancelada. Escreva qualquer mensagem para voltar ao menu.",
            ["pedir_nombre"]       = "Qual é o seu *nome completo*?",
            ["pedir_empresa"]      = "Qual é o nome da sua *empresa*?",
            ["pedir_telefono"]     = "Qual é o seu *número de telefone*?",
            ["pedir_email"]        = "Qual é o seu *e-mail*?",
            ["pedir_direccion"]    = "📍 Qual é o *endereço de entrega*?",
            ["pedir_servicio"]     = "Que *serviço ou produto* você oferece? (ex: logística, embalagem, insumos, etc.)",
            ["pedir_desc"]         = "Conte-nos mais: *o que inclui o serviço?* (capacidade, cobertura, condições, etc.)",
            ["confirmar"]          = "Você confirma? Digite *confirmar*",
            ["demasiados_errores"] = "❌ *Muitas tentativas falhas.* Operação cancelada. Escreva qualquer mensagem para voltar ao menu.",
            ["intentos"]           = "Tentativas restantes",
        }
    };

    public static string Get(ConversationSession session, string key)
    {
        var lang = session.Idioma ?? "es";
        if (Textos.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
            return val;
        return Textos["es"].TryGetValue(key, out var fallback) ? fallback : key;
    }
}
