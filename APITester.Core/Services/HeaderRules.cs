namespace APITester.Core.Services;

/// <summary>
/// Reglas unicas sobre headers del request: cuales estan prohibidos y que
/// valores no pueden contener. Las comparten la construccion del request y el
/// asistente interactivo.
/// </summary>
public static class HeaderRules
{
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Length", "Transfer-Encoding", "Host", "Connection",
        "Upgrade", "Proxy-Connection", "Keep-Alive", "TE", "Trailer"
    };

    /// <summary>
    /// Devuelve el mensaje de error si el header no esta permitido; null si es valido.
    /// </summary>
    public static string? Validate(string key, string value)
    {
        if (ForbiddenHeaders.Contains(key))
            return $"Header '{key}' no esta permitido por seguridad";

        if (value.IndexOfAny(['\r', '\n']) >= 0)
            return $"El valor del header '{key}' contiene caracteres invalidos";

        return null;
    }

    public static bool IsContentType(string key) =>
        key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase);
}
