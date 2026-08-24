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
        GC.SuppressFinalize(this);
    }

    public async Task<ApiResponse> ExecuteAsync(RestRequestConfig config, CancellationToken cancellationToken = default)
    {
        var policy = new RetryPolicy
        {
            MaxRetries = config.EffectiveRetries,
            DelayMs = config.EffectiveRetryDelayMilliseconds,
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
                RequestHeaders = RequestBuilder.GetRequestHeaders(config)
            }
        };

        using var request = RequestBuilder.Build(config);

        var client = CertHandlerFactory.Create(config.Cert) ?? _httpClient;

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.EffectiveTimeoutInSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

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
