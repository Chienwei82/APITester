namespace APITester.Core.Services;

/// <summary>
/// Clasificacion de metodos HTTP compartida entre el modelo (validacion) y el
/// builder de requests (armado del contenido), para no duplicar la logica.
/// </summary>
public static class HttpMethods
{
    public static bool SupportsBody(string method) =>
        method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    public static bool IsValidMethod(string method) =>
        method.ToUpperInvariant() is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS";
}
