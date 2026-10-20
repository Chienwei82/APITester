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
        if (string.IsNullOrWhiteSpace(config.Url))
        {
            return new ApiResponse { Error = "La URL es requerida" };
        }

        try
        {
            var policy = new RetryPolicy
            {
                MaxRetries = config.EffectiveRetries,
                DelayMs = config.EffectiveRetryDelayMilliseconds,
                UseExponentialBackoff = config.EffectiveUseExponentialBackoff,
                RetryOnStatusCodes = config.RetryOnStatusCodes
            };

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
        catch (Exception ex) when (ex is not (OutOfMemoryException or AccessViolationException))
        {
            // Un request no puede tumbar el resto de la ejecucion: se reporta como error.
            return new ApiResponse { Error = $"{ex.GetType().Name}: {ex.Message}" };
        }
    }

    private async Task<ApiResponse> ExecuteOnceAsync(RestRequestConfig config, CancellationToken cancellationToken)
    {
        // Build resuelve y valida los headers (puede lanzar InvalidOperationException)
        // y devuelve los headers que viajan en el request: los mismos que se registran.
        var (request, sentHeaders) = RequestBuilder.Build(config);

        using (request)
        {
            var response = new ApiResponse
            {
                Request = new RequestInfo
                {
                    Name = config.Name,
                    Url = config.Url,
                    Method = config.Method,
                    RequestHeaders = _redactHeaders ? HeaderRedactor.Redact(sentHeaders) : sentHeaders
                }
            };

            var client = CertHandlerFactory.Create(config.Cert) ?? _httpClient;

            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(config.EffectiveTimeoutInSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            var sw = Stopwatch.StartNew();
            // ResponseHeadersRead: el body se lee en streaming, asi 'maxBodyBytes'
            // limita de verdad la memoria usada por respuestas grandes.
            using var httpResponse = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
            sw.Stop();

            await FillResponseAsync(
                response, httpResponse, sw, config.EffectiveMaxBodyBytes, linkedCts.Token).ConfigureAwait(false);
            return response;
        }
    }

    private async Task FillResponseAsync(
        ApiResponse target,
        HttpResponseMessage httpResponse,
        Stopwatch sw,
        long bodyLimit,
        CancellationToken cancellationToken)
    {
        var (bodyBytes, truncated) = await ReadBodyAsync(
            httpResponse.Content, bodyLimit, cancellationToken).ConfigureAwait(false);
        var bodyText = Encoding.UTF8.GetString(bodyBytes);
        var headers = HeaderCollector.CollectFrom(httpResponse);

        target.Response = new ResponseInfo
        {
            StatusCode = (int)httpResponse.StatusCode,
            StatusText = httpResponse.ReasonPhrase,
            Headers = _redactHeaders ? HeaderRedactor.Redact(headers) : headers,
            BodyRaw = bodyText,
            TimeMs = sw.ElapsedMilliseconds,
            SizeBytes = httpResponse.Content.Headers.ContentLength ?? bodyBytes.LongLength,
            Truncated = truncated ? true : null
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
    private static async Task<(byte[] Bytes, bool Truncated)> ReadBodyAsync(
        HttpContent content,
        long limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0) return (Array.Empty<byte>(), false);

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream(capacity: (int)Math.Min(limit, 64 * 1024));
        var buffer = new byte[16 * 1024];
        long total = 0;

        while (total < limit)
        {
            // Nunca se pide mas de lo que falta: si se leyera el buffer completo
            // se consumiria el resto del body y no se detectaria el truncamiento.
            var toRead = (int)Math.Min(buffer.Length, limit - total);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;

            ms.Write(buffer, 0, read);
            total += read;
        }

        // Sea cual sea el motivo por el que paro el bucle (limite o fin del body),
        // una lectura extra basta para saber si quedo contenido por leer.
        var trailing = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return (ms.ToArray(), trailing > 0);
    }

    private static void TrySetJsonBody(string rawBody, ResponseInfo response)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return;
        try { response.Body = JsonNode.Parse(rawBody); }
        catch (System.Text.Json.JsonException) { }
    }
}
