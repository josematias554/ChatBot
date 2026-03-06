using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuillenBot.Services;

public static class Validador
{
    // ─── Nombre y apellido ─────────────────────────────────────────────────
    // Solo letras (con acentos), espacios y apóstrofes. Sin números ni símbolos.
    private static readonly Regex _regexNombre = new(@"^[\p{L}\s''\-]{2,60}$", RegexOptions.Compiled);

    public static (bool ok, string error) ValidarNombre(string texto)
    {
        texto = texto.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return (false, "⚠️ El nombre no puede estar vacío. Ingresá tu nombre y apellido.");

        if (!_regexNombre.IsMatch(texto))
            return (false, "⚠️ El nombre solo puede contener letras y espacios. Sin números ni símbolos.\nEjemplo: *Juan Pérez*");

        if (texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 1)
            return (false, "⚠️ Ingresá al menos un nombre.");

        return (true, string.Empty);
    }

    // ─── Empresa ───────────────────────────────────────────────────────────
    private static readonly Regex _regexEmpresa = new(@"^[\p{L}0-9\s\.\,\-\&]{3,80}$", RegexOptions.Compiled);

    public static (bool ok, string error) ValidarEmpresa(string texto)
    {
        texto = texto.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return (false, "⚠️ El nombre de empresa no puede estar vacío.");

        if (texto.Length < 3)
            return (false, "⚠️ El nombre de empresa debe tener al menos 3 caracteres.");

        if (!_regexEmpresa.IsMatch(texto))
            return (false, "⚠️ Nombre de empresa inválido. Solo letras, números, espacios y los símbolos . , - &\nEjemplo: *Distribuidora San Martín*");

        return (true, string.Empty);
    }

    // ─── Teléfono ──────────────────────────────────────────────────────────
    private static readonly Regex _regexTelefono = new(@"^\+?[\d\s\-\(\)]{7,20}$", RegexOptions.Compiled);

    public static (bool ok, string error) ValidarTelefono(string texto)
    {
        texto = texto.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return (false, "⚠️ El teléfono no puede estar vacío.");

        // Solo dígitos, +, espacios, guiones y paréntesis
        if (!_regexTelefono.IsMatch(texto))
            return (false, "⚠️ Teléfono inválido. Solo se permiten números, el símbolo + y espacios.\nEjemplo: *+54 381 6525734* o *3816525734*");

        // Verificar que tenga al menos 7 dígitos reales
        var soloDigitos = Regex.Replace(texto, @"\D", "");
        if (soloDigitos.Length < 7)
            return (false, "⚠️ El teléfono debe tener al menos 7 dígitos.");

        if (soloDigitos.Length > 15)
            return (false, "⚠️ El teléfono es demasiado largo. Máximo 15 dígitos.");

        return (true, string.Empty);
    }

    // ─── Email ─────────────────────────────────────────────────────────────
    private static readonly Regex _regexEmail = new(
        @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (bool ok, string error) ValidarEmail(string texto)
    {
        texto = texto.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return (false, "⚠️ El email no puede estar vacío.");

        if (!_regexEmail.IsMatch(texto))
            return (false, "⚠️ Email inválido. Debe tener el formato *usuario@dominio.com*\nEjemplo: *ventas@empresa.com*");

        return (true, string.Empty);
    }

    // ─── Cantidad ──────────────────────────────────────────────────────────
    public static (bool ok, decimal cantidad, string error) ValidarCantidad(string texto)
    {
        texto = texto.Trim().Replace(",", ".");

        if (!decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var cantidad))
            return (false, 0, "⚠️ Cantidad inválida. Ingresá un número en kilos.\nEjemplo: *50* o *12.5*");

        if (cantidad <= 0)
            return (false, 0, "⚠️ La cantidad debe ser mayor a cero.");

        if (cantidad > 10_000)
            return (false, 0, "⚠️ La cantidad máxima por ítem es 10.000 kg. Para pedidos mayores, contactanos directamente.");

        // Redondear a 2 decimales
        cantidad = Math.Round(cantidad, 2);
        return (true, cantidad, string.Empty);
    }

    // ─── Dirección ─────────────────────────────────────────────────────────
    public static (bool ok, string error) ValidarDireccion(string texto)
    {
        texto = texto.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return (false, "⚠️ La dirección no puede estar vacía.");

        if (texto.Length < 10)
            return (false, "⚠️ La dirección parece muy corta. Ingresá calle, número y ciudad.\nEjemplo: *Lavalle 115, Tucumán*");

        if (texto.Length > 200)
            return (false, "⚠️ La dirección es demasiado larga (máximo 200 caracteres).");

        return (true, string.Empty);
    }

    // ─── Normalización de texto (quita acentos) ────────────────────────────
    public static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalizado)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
