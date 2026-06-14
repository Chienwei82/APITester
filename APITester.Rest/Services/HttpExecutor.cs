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

    public HttpExecutor(ILogger? logger = null, HttpClient? httpClient = null)
    {
        _logger = logger ?? new ConsoleLogger();
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
    }

    public async Task<ApiResponse> ExecuteAsync(RestRequestConfig config, CancellationToken cancellationToken = default)
    {
        var policy = new RetryPolicy
        {
            MaxRetries = config.Retries,
            DelayMs = config.RetryDelayMilliseconds,
            UseExponentialBackoff = config.UseExponentialBackoff,
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
                Method = config.Method,
                RequestHeaders = RequestBuilder.GetRequestHeaders(config)
            }
        };

        using var request = RequestBuilder.Build(config);

        var handler = CertHandlerFactory.Create(config.Cert);
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.TimeoutInSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        var client = handler is not null
            ? new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan }
            : _httpClient;

        var sw = Stopwatch.StartNew();
        using var httpResponse = await client.SendAsync(
            request, linkedCts.Token).ConfigureAwait(false);
        sw.Stop();

        await FillResponseAsync(response, httpResponse, sw).ConfigureAwait(false);
        return response;
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
}
