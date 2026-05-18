using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public class HttpExecutor : IApiExecutor<RestRequestConfig>
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public HttpExecutor(ILogger? logger = null, HttpClient? httpClient = null)
    {
        _logger = logger ?? new ConsoleLogger();
        _httpClient = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<ApiResponse> ExecuteAsync(RestRequestConfig config, CancellationToken cancellationToken = default)
    {
        var policy = new RetryPolicy
        {
            MaxRetries = config.Retries,
            DelayMs = config.RetryDelayMilliseconds
        };

        if (string.IsNullOrWhiteSpace(config.Url))
        {
            return new ApiResponse { Error = "La URL es requerida" };
        }

        try
        {
            return await policy.ExecuteAsync(
                ct => ExecuteOnceAsync(config, ct),
                _logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse { Error = ex.Message };
        }
    }

    private async Task<ApiResponse> ExecuteOnceAsync(RestRequestConfig config, CancellationToken cancellationToken = default)
    {
        var response = new ApiResponse
        {
            Request = new RequestInfo
            {
                Name = config.Name,
                Url = config.Url,
                Method = config.Method
            }
        };

        var url = BuildUrlWithQuery(config);

        using var request = BuildHttpRequest(config, url);
        response.Request.RequestHeaders = request.Headers
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.TimeoutInSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        using HttpClientHandler? handler = CertHandlerFactory.Create(config.Cert);
        using var ownedClient = handler is not null
            ? new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }
            : null;
        var client = ownedClient ?? _httpClient;

        var sw = Stopwatch.StartNew();
        using var httpResponse = await client.SendAsync(
            request, linkedCts.Token).ConfigureAwait(false);
        sw.Stop();

        await FillResponseAsync(response, httpResponse, sw).ConfigureAwait(false);
        return response;
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

    private static HttpRequestMessage BuildHttpRequest(RestRequestConfig config, string url)
    {
        var method = new HttpMethod(config.Method.ToUpperInvariant());
        var request = new HttpRequestMessage(method, url);

        var resolvedHeaders = config.Headers is not null
            ? EnvVarResolver.Resolve(config.Headers)
            : null;

        foreach (var (key, value) in resolvedHeaders ?? [])
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;
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

    private static async Task FillResponseAsync(ApiResponse target, HttpResponseMessage httpResponse, Stopwatch sw)
    {
        var bodyBytes = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var bodyText = Encoding.UTF8.GetString(bodyBytes);

        target.Response = new ResponseInfo
        {
            StatusCode = (int)httpResponse.StatusCode,
            StatusText = httpResponse.ReasonPhrase,
            Headers = HeaderCollector.CollectFrom(httpResponse),
            BodyRaw = bodyText,
            TimeMs = sw.ElapsedMilliseconds,
            SizeBytes = bodyBytes.Length
        };

        TrySetJsonBody(bodyText, target.Response);
    }

    private static void TrySetJsonBody(string rawBody, ResponseInfo response)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return;
        try { response.Body = JsonNode.Parse(rawBody); }
        catch (System.Text.Json.JsonException) { }
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
