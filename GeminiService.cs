using System.Net.Http;
using System.Text;
using System.Text.Json;
using QuillenBot.Models;
using Microsoft.Extensions.Logging;

namespace QuillenBot.Services;

/// <summary>
/// Llama directamente a la REST API de Gemini sin depender de versiones del SDK.
/// Endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
/// </summary>
public class GeminiService
{
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GeminiService> _logger;
    private static readonly HttpClient _http = new();

    public GeminiService(string apiKey, string modelName, ILogger<GeminiService> logger)
    {
        _apiKey    = apiKey;
        _modelName = modelName;
        _logger    = logger;
    }

    // ─── Llamada base a la API ─────────────────────────────────────────────
    private async Task<string> LlamarGeminiAsync(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content);
        var raw      = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Gemini API error {response.StatusCode}: {raw}");

        // Extraer el texto de la respuesta
        // Estructura: candidates[0].content.parts[0].text
        using var doc  = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        return text.Trim();
    }

    // ─── Clasificar usuario ────────────────────────────────────────────────
    public async Task<ClasificacionUsuario> ClasificarUsuarioAsync(string mensaje)
    {
        var prompt =
            "Eres el asistente de Quillen Berries, empresa de frutas en Tucumán, Argentina. Clientes son MAYORISTAS.\n\n" +
            "Mensaje del usuario: \"" + mensaje + "\"\n\n" +
            "Clasifica si es cliente (quiere comprar/pedir) o proveedor (ofrece servicios) o desconocido.\n" +
            "Responde SOLO con JSON sin markdown: {\"tipo\":\"cliente\",\"intencion\":\"texto breve\"}\n" +
            "tipo puede ser: cliente, proveedor, desconocido";

        try
        {
            var text = await LlamarGeminiAsync(prompt);
            text = text.Replace("```json", "").Replace("```", "").Trim();

            using var doc = JsonDocument.Parse(text);
            var tipo  = doc.RootElement.GetProperty("tipo").GetString() ?? "desconocido";
            var inten = doc.RootElement.GetProperty("intencion").GetString() ?? "";

            return new ClasificacionUsuario
            {
                Tipo = tipo switch
                {
                    "cliente"   => UserType.Cliente,
                    "proveedor" => UserType.Proveedor,
                    _           => UserType.Desconocido
                },
                Intencion = inten
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error clasificando usuario con Gemini, usando Desconocido");
            return new ClasificacionUsuario { Tipo = UserType.Desconocido };
        }
    }

    // ─── Interpretar producto ──────────────────────────────────────────────
    public async Task<string> InterpretarProductoAsync(string mensajeUsuario)
    {
        var productos = string.Join(", ", Catalogo.Productos.Keys);
        var prompt =
            "Catálogo de Quillen Berries: " + productos + "\n" +
            "El usuario escribió: \"" + mensajeUsuario + "\"\n" +
            "Responde SOLO con el nombre exacto del producto del catálogo que mejor coincide, o \"ninguno\". Sin explicaciones.";

        try
        {
            return await LlamarGeminiAsync(prompt);
        }
        catch
        {
            return mensajeUsuario;
        }
    }
}
