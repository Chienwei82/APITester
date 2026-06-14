using System.Net;
using System.Text;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public static class RequestBuilder
{
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Length", "Transfer-Encoding", "Host", "Connection",
        "Upgrade", "Proxy-Connection", "Keep-Alive", "TE", "Trailer"
    };

    public static HttpRequestMessage Build(RestRequestConfig config)
    {
        var method = new HttpMethod(config.Method.ToUpperInvariant());
        var url = BuildUrlWithQuery(config);
        var request = new HttpRequestMessage(method, url);

        var resolvedHeaders = config.Headers is not null
            ? EnvVarResolver.Resolve(config.Headers)
            : null;

        foreach (var (key, value) in resolvedHeaders ?? [])
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ForbiddenHeaders.Contains(key))
                throw new InvalidOperationException($"Header '{key}' no esta permitido por seguridad");

            if (value.IndexOfAny(['\r', '\n']) >= 0)
                throw new InvalidOperationException($"El valor del header '{key}' contiene caracteres invalidos");

            request.Headers.TryAddWithoutValidation(key, value);
        }

        if (HasBody(method) && config.Body is not null)
        {
            var contentType = ResolveContentType(resolvedHeaders);
            var resolvedBody = EnvVarResolver.Resolve(config.Body)!;
            request.Content = new StringContent(resolvedBody, Encoding.UTF8, contentType);
        }

        return request;
    }

    public static Dictionary<string, string> GetRequestHeaders(RestRequestConfig config)
    {
        if (config.Headers is null || config.Headers.Count == 0)
            return [];

        var resolvedHeaders = EnvVarResolver.Resolve(config.Headers);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in resolvedHeaders)
        {
            if (!key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                && !ForbiddenHeaders.Contains(key)
                && value.IndexOfAny(['\r', '\n']) < 0)
            {
                result[key] = value;
            }
        }

        return result;
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

    private static bool HasBody(HttpMethod method) =>
        method.Method is "POST" or "PUT" or "PATCH";

    private static string ResolveContentType(Dictionary<string, string>? headers)
    {
        if (headers?.TryGetValue("Content-Type", out var ct) == true)
            return ct;
        return "application/json";
    }
}