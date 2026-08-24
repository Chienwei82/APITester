using System.Diagnostics;
using System.Threading;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public class RequestExecutor
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

    public async Task<List<ApiResponse>> ExecuteAllAsync(
        List<RestRequestConfig> requests,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        var completedCount = 0;

        var indexedTasks = requests.Select(async (config, i) =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var label = config.Name ?? $"{config.Method} {config.Url}";
                _presenter.PrintRequestHeader(label, i, requests.Count);

                var result = await _executor.ExecuteAsync(config, cancellationToken).ConfigureAwait(false);
                _presenter.PrintResponseSummary(result);

                if (_verbose)
                {
                    _presenter.PrintVerboseLine("Query", BuildQueryPreview(config.Query));
                    _presenter.PrintVerboseLine("Body", BuildBodyPreview(config.Body));
                    _presenter.PrintVerboseLine("Cert", config.Cert?.Path);
                    _presenter.PrintVerboseLine("Retries", config.EffectiveRetries > 0 ? $"{config.EffectiveRetries} max" : null);
                }

                return (index: i, result);
            }
            finally
            {
                semaphore.Release();
                var completed = Interlocked.Increment(ref completedCount);
                _presenter.PrintProgress(completed, requests.Count);
            }
        });

        var indexedResults = await Task.WhenAll(indexedTasks).ConfigureAwait(false);

        return indexedResults
            .OrderBy(r => r.index)
            .Select(r => r.result)
            .ToList();
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