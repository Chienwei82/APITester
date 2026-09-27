using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public sealed class RequestExecutor
{
    private readonly HttpExecutor _executor;
    private readonly bool _verbose;
    private readonly ConsolePresenter _presenter;

    public RequestExecutor(HttpExecutor executor, ConsolePresenter presenter, bool verbose)
    {
        _executor = executor;
        _presenter = presenter;
        _verbose = verbose;
    }

    /// <summary>
    /// Ejecuta los requests uno por uno, en el orden del archivo de configuracion,
    /// informando del resultado de cada uno y cerrando la barra de progreso al final.
    /// </summary>
    public async Task<List<ApiResponse>> ExecuteAllAsync(
        List<RestRequestConfig> requests,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ApiResponse>(requests.Count);

        foreach (var config in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(await ExecuteOneAsync(config, results.Count, requests.Count, cancellationToken)
                .ConfigureAwait(false));
        }

        _presenter.PrintProgress(results.Count, requests.Count);

        return results;
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
