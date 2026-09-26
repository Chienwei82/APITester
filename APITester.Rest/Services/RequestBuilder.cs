using System.Net;
using System.Text;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public static class RequestBuilder
{
    private const string DefaultContentType = "application/json";

    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Length", "Transfer-Encoding", "Host", "Connection",
        "Upgrade", "Proxy-Connection", "Keep-Alive", "TE", "Trailer"
    };

    /// <summary>
    /// Construye el request y devuelve tambien los headers que viajan en el (sin
    /// Content-Type, que se envia con el body). Es el unico punto que resuelve
    /// variables de entorno y valida headers: si alguno esta prohibido o su valor
    /// contiene caracteres invalidos, lanza <see cref="InvalidOperationException"/>.
    /// </summary>
    public static (HttpRequestMessage Request, Dictionary<string, string> SentHeaders) Build(RestRequestConfig config)
    {
        var headers = ResolveHeaders(config);
        var sentHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var method = new HttpMethod(config.Method.ToUpperInvariant());
        var request = new HttpRequestMessage(method, BuildUrlWithQuery(config));

        foreach (var (key, value) in headers)
        {
            if (IsContentType(key)) continue;

            request.Headers.TryAddWithoutValidation(key, value);
            sentHeaders[key] = value;
        }

        if (HttpMethods.AllowsBody(method.Method) && config.Body is not null)
        {
            var contentType = headers.TryGetValue("Content-Type", out var configured)
                ? configured
                : DefaultContentType;
            var resolvedBody = EnvVarResolver.Resolve(config.Body)!;
            request.Content = new StringContent(resolvedBody, Encoding.UTF8, contentType);
        }

        return (request, sentHeaders);
    }

    private static Dictionary<string, string> ResolveHeaders(RestRequestConfig config)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (config.Headers is null || config.Headers.Count == 0)
        {
            return resolved;
        }

        foreach (var (key, value) in EnvVarResolver.Resolve(config.Headers))
        {
            if (ForbiddenHeaders.Contains(key))
                throw new InvalidOperationException($"Header '{key}' no esta permitido por seguridad");

            if (value.IndexOfAny(['\r', '\n']) >= 0)
                throw new InvalidOperationException($"El valor del header '{key}' contiene caracteres invalidos");

            resolved[key] = value;
        }

        return resolved;
    }

    private static string BuildUrlWithQuery(RestRequestConfig config)
    {
        var url = EnvVarResolver.Resolve(config.Url)!;

        var resolvedQuery = config.Query is not null
            ? EnvVarResolver.Resolve(config.Query)
            : null;

        if (resolvedQuery is not { Count: > 0 })
            return url;

        var segments = resolvedQuery.Select(entry =>
            $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value)}");
        var sep = url.Contains('?') ? '&' : '?';
        return $"{url}{sep}{string.Join("&", segments)}";
    }

    private static bool IsContentType(string key) =>
        key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase);
}
