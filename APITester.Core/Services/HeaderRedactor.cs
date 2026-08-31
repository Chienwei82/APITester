namespace APITester.Core.Services;

/// <summary>
/// Redacta headers con credenciales para que no se persistan en los archivos
/// de salida ni se muestren en consola. El redactado se aplica al recolectar
/// los headers para la salida: el request HTTP real siempre viaja completo.
/// </summary>
public static class HeaderRedactor
{
    public const string MaskValue = "***";

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie"
    };

    public static bool IsSensitive(string headerName) =>
        SensitiveHeaders.Contains(headerName);

    public static Dictionary<string, string>? Redact(Dictionary<string, string>? headers)
    {
        if (headers is null) return null;

        return headers.ToDictionary(
            kv => kv.Key,
            kv => IsSensitive(kv.Key) ? MaskValue : kv.Value);
    }
}
