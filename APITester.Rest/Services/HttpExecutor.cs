using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public class HttpExecutor : IApiExecutor<RestRequestConfig>, IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly bool _redactHeaders;

    public HttpExecutor(ILogger? logger = null, HttpClient? httpClient = null, bool redactHeaders = true)
    {
        _logger = logger ?? new ConsoleLogger();
        _redactHeaders = redactHeaders;
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _ownsHttpClient = true;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    public async Task<ApiResponse> ExecuteAsync(RestRequestConfig config, CancellationToken cancellationToken = default)
    {
        var policy = new RetryPolicy
        {
            MaxRetries = config.EffectiveRetries,
            DelayMs = config.EffectiveRetryDelayMilliseconds,
            UseExponentialBackoff = config.EffectiveUseExponentialBackoff,
            RetryOnStatusCodes = config.RetryOnStatusCodes
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse { Error = ex.Message };
        }
        catch (Exception ex)
        {
            return new ApiResponse { Error = $"{ex.GetType().Name}: {ex.Message}" };
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
                Method = config.Method,
                RequestHeaders = _redactHeaders
                    ? HeaderRedactor.Redact(RequestBuilder.GetRequestHeaders(config))
                    : RequestBuilder.GetRequestHeaders(config)
            }
        };

        using var request = RequestBuilder.Build(config);

        // Clientes con certificado salen de la cache con una referencia activa:
        // se libera al terminar para que la LRU no los deseche en vuelo.
        var client = CertHandlerFactory.Create(config.Cert);
        var isCertClient = client is not null;
        client ??= _httpClient;

        try
        {
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(config.EffectiveTimeoutInSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            var sw = Stopwatch.StartNew();
            using var httpResponse = await client.SendAsync(
                request, linkedCts.Token).ConfigureAwait(false);
            sw.Stop();

            await FillResponseAsync(response, httpResponse, sw, config.EffectiveMaxBodyBytes, _redactHeaders, linkedCts.Token).ConfigureAwait(false);
            return response;
        }
        finally
        {
            if (isCertClient)
                CertHandlerFactory.Release(client);
        }
    }

    private static async Task FillResponseAsync(ApiResponse target, HttpResponseMessage httpResponse, Stopwatch sw, long bodyLimit, bool redactHeaders, CancellationToken cancellationToken)
    {
        var (bodyBytes, truncated) = await ReadBodyAsync(httpResponse.Content, bodyLimit, cancellationToken).ConfigureAwait(false);
        var bodyText = Encoding.UTF8.GetString(bodyBytes);

        target.Response = new ResponseInfo
        {
            StatusCode = (int)httpResponse.StatusCode,
            StatusText = httpResponse.ReasonPhrase,
            Headers = redactHeaders
                ? HeaderRedactor.Redact(HeaderCollector.CollectFrom(httpResponse))
                : HeaderCollector.CollectFrom(httpResponse),
            BodyRaw = bodyText,
            TimeMs = sw.ElapsedMilliseconds,
            SizeBytes = httpResponse.Content.Headers.ContentLength ?? bodyBytes.LongLength
        };

        if (truncated)
        {
            // No parseamos un body truncado incompleto para no generar JSON corrupto.
            return;
        }

        TrySetJsonBody(bodyText, target.Response);
    }

    /// <summary>
    /// Lee el body con limite para acotar el uso de memoria en respuestas grandes.
    /// Devuelve los bytes leidos (truncados) y si hubo truncamiento.
    /// </summary>
    private static async Task<(byte[] Bytes, bool Truncated)> ReadBodyAsync(HttpContent content, long limit, CancellationToken cancellationToken)
    {
        if (limit <= 0) return (Array.Empty<byte>(), false);

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[16384];
        int total = 0;

        while (total < limit)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;

            var toWrite = (int)Math.Min(read, limit - total);
            ms.Write(buffer, 0, toWrite);
            total += toWrite;
        }

        // Si quedó contenido por leer, fue truncado.
        if (total < limit)
        {
            var trailing = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return (ms.ToArray(), trailing > 0);
        }

        // El limite se alcanzó exactamente; confirmar si hay más.
        var extra = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return (ms.ToArray(), extra > 0);
    }

    private static void TrySetJsonBody(string rawBody, ResponseInfo response)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return;
        try { response.Body = JsonNode.Parse(rawBody); }
        catch (System.Text.Json.JsonException) { }
    }
}
