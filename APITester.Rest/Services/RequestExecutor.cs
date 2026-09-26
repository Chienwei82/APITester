using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public sealed class RequestExecutor
{
    private readonly HttpExecutor _executor;
    private readonly int _maxConcurrency;
    private readonly bool _verbose;
    private readonly ConsolePresenter _presenter;

    public RequestExecutor(HttpExecutor executor, ConsolePresenter presenter, int maxConcurrency, bool verbose)
    {
        _executor = executor;
        _presenter = presenter;
        _maxConcurrency = maxConcurrency;
        _verbose = verbose;
    }

    /// <summary>
    /// Ejecuta los requests con un limite de concurrencia. Cada resultado se guarda
    /// en su posicion, de modo que el orden final es el del archivo de configuracion
    /// aunque las respuestas terminen en diferente orden.
    /// </summary>
    public async Task<List<ApiResponse>> ExecuteAllAsync(
        List<RestRequestConfig> requests,
        CancellationToken cancellationToken = default)
    {
        var results = new ApiResponse[requests.Count];
        var completedCount = 0;

        using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

        await Task.WhenAll(requests.Select(async (config, index) =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                results[index] = await ExecuteOneAsync(config, index, requests.Count, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                var completed = Interlocked.Increment(ref completedCount);
                _presenter.PrintProgress(completed, requests.Count);
            }
        })).ConfigureAwait(false);

        return [.. results];
    }

    private async Task<ApiResponse> ExecuteOneAsync(
        RestRequestConfig config,
        int index,
        int total,
        CancellationToken cancellationToken)
    {
        var label = config.Name ?? $"{config.Method} {config.Url}";
        _presenter.PrintRequestHeader(label, index, total);

        var result = await _executor.ExecuteAsync(config, cancellationToken).ConfigureAwait(false);
        _presenter.PrintResponseSummary(result);

        if (_verbose)
        {
            _presenter.PrintVerboseLine("Query", BuildQueryPreview(config.Query));
            _presenter.PrintVerboseLine("Body", BuildBodyPreview(config.Body));
            _presenter.PrintVerboseLine("Cert", config.Cert?.Path);
            _presenter.PrintVerboseLine("Retries", config.EffectiveRetries > 0 ? $"{config.EffectiveRetries} max" : null);
        }

        return result;
    }

    private static string? BuildQueryPreview(Dictionary<string, string>? q)
    {
        if (q is null or { Count: 0 }) return null;
        return string.Join("&", q.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static string? BuildBodyPreview(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        return body.Length <= 200 ? body : body[..200] + "...";
    }
}
