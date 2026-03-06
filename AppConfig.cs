namespace QuillenBot.Config;

public class BotConfiguration
{
    public string BotToken { get; set; } = string.Empty;
    public long ApproverChatId { get; set; }
    public string SpreadsheetId { get; set; } = string.Empty;
    public string NombreEmpresa { get; set; } = "Nuestra Empresa";
    public string RubroEmpresa { get; set; } = "productos";
}

public class GeminiConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";
}
