using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using QuillenBot.Config;
using QuillenBot.Handlers;
using QuillenBot.Services;

// ─── Cargar configuración ──────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var botConfig = config.GetSection("BotConfiguration").Get<BotConfiguration>() ?? throw new Exception("Falta BotConfiguration en appsettings.json");
var geminiConfig = config.GetSection("GeminiConfiguration").Get<GeminiConfiguration>() ?? throw new Exception("Falta GeminiConfiguration en appsettings.json");

// ─── Setup de logging ──────────────────────────────────────────────────────
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();
var geminiLogger = loggerFactory.CreateLogger<GeminiService>();
var handlerLogger = loggerFactory.CreateLogger<MessageHandler>();

// ─── Instanciar servicios ──────────────────────────────────────────────────
var botClient  = new TelegramBotClient(botConfig.BotToken);
var sessions   = new SessionManager();
var sheetsSvc  = new GoogleSheetsService("credentials.json", botConfig.SpreadsheetId, loggerFactory.CreateLogger<GoogleSheetsService>());
var geminiSvc  = new GeminiService(geminiConfig.ApiKey, geminiConfig.Model, botConfig.NombreEmpresa, botConfig.RubroEmpresa, geminiLogger);
var stockSvc   = new StockService(loggerFactory.CreateLogger<StockService>());

var handler = new MessageHandler(
    botClient,
    sessions,
    geminiSvc,
    sheetsSvc,
    stockSvc,
    botConfig.ApproverChatId,
    botConfig.NombreEmpresa,
    botConfig.RubroEmpresa,
    handlerLogger
);

// ─── Verificar conexión ────────────────────────────────────────────────────
var me = await botClient.GetMe();
logger.LogInformation("Bot iniciado: @{Username} ({Id})", me.Username, me.Id);
logger.LogInformation("Aprobador configurado: ChatId = {Id}", botConfig.ApproverChatId);
logger.LogInformation("Google Sheets ID: {Id}", botConfig.SpreadsheetId);

// ─── TEMPORAL: Cargar datos de ejemplo ────────────────────────────────────
// Descomentar las 2 líneas de abajo, correr el bot UNA vez, volver a comentar
//await sheetsSvc.CargarEjemplosAsync();
//return;

// ─── Long Polling ──────────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    logger.LogInformation("Deteniendo bot...");
    cts.Cancel();
};

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

logger.LogInformation("Bot escuchando... Presioná Ctrl+C para detener.");
await Task.Delay(Timeout.Infinite, cts.Token).ContinueWith(_ => { });
logger.LogInformation("Bot detenido.");

// ─── Handlers de updates ───────────────────────────────────────────────────
async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
{
    try
    {
        if (update.Message is { } message)
            await handler.HandleMessageAsync(message);

        else if (update.CallbackQuery is { } callback)
            await handler.HandleCallbackAsync(callback);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error procesando update {UpdateId}", update.Id);
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken ct)
{
    logger.LogError(ex, "Error de polling");
    return Task.CompletedTask;
}