namespace APITester.Core.Services;

/// <summary>
/// Conocimiento unico sobre metodos HTTP: cuales soporta la herramienta y
/// cuales admiten body. Es la unica fuente de verdad, compartida por la
/// validacion de la config y la construccion de los requests.
/// </summary>
public static class HttpMethods
{
    /// <summary>Metodos soportados, en el orden en que se presentan al usuario.</summary>
    public static readonly string[] All = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    private static readonly HashSet<string> Supported = new(All, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WithBody = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH"
    };

    public static bool IsSupported(string method) => Supported.Contains(method);

    public static bool AllowsBody(string method) => WithBody.Contains(method);
}
