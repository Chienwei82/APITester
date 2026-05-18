using System.Diagnostics;
using System.Text;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

Console.OutputEncoding = Encoding.UTF8;

static string? BuildQueryPreview(Dictionary<string, string>? q)
{
    if (q is null or { Count: 0 }) return null;
    return string.Join("&", q.Select(kv => $"{kv.Key}={kv.Value}"));
}

static string? BuildBodyPreview(string? body)
{
    if (string.IsNullOrEmpty(body)) return null;
    return body.Length <= 200 ? body : body[..200] + "...";
}

var cliArgs = ArgumentParser.Parse(
    Environment.GetCommandLineArgs().Skip(1).ToArray(),
    "rest-config.json");

if (cliArgs.ShowHelp)
{
    ConsolePresenter.PrintHelp("REST", "rest-config.json");
    return 0;
}

List<RestRequestConfig> requests;
try
{
    requests = await RestConfigLoader.LoadAsync(cliArgs.ConfigFile).ConfigureAwait(false);
}
catch (Exception ex)
{
    ConsolePresenter.PrintFatalError(ex.Message);
    return 1;
}

var warnings = requests
    .SelectMany((r, i) => r.Validate().Select(w => $"[{i + 1}] {w}"))
    .ToList();
if (warnings.Count > 0)
    ConsolePresenter.PrintValidationWarnings(warnings);

if (requests.Count == 0)
{
    ConsolePresenter.PrintFatalError("No se encontraron requests en el archivo de configuracion");
    return 1;
}

var totalSw = Stopwatch.StartNew();
var results = new ApiResponse[requests.Count];

var executor = new HttpExecutor();
var semaphore = new SemaphoreSlim(4, 4);

var tasks = requests.Select(async (config, i) =>
{
    await semaphore.WaitAsync().ConfigureAwait(false);
    try
    {
        var label = config.Name ?? $"{config.Method} {config.Url}";
        ConsolePresenter.PrintRequestHeader(label, i, requests.Count);

        var result = await executor.ExecuteAsync(config).ConfigureAwait(false);
        results[i] = result;

        ConsolePresenter.PrintResponseSummary(result);

        if (cliArgs.Verbose)
        {
            ConsolePresenter.PrintVerboseLine("Query", BuildQueryPreview(config.Query));
            ConsolePresenter.PrintVerboseLine("Body", BuildBodyPreview(config.Body));
            ConsolePresenter.PrintVerboseLine("Cert", config.Cert?.Path);
            ConsolePresenter.PrintVerboseLine("Retries", config.Retries > 0 ? $"{config.Retries} max" : null);
        }
    }
    finally
    {
        semaphore.Release();
    }
});

await Task.WhenAll(tasks).ConfigureAwait(false);
totalSw.Stop();

var outputFile = cliArgs.OutputFile
    ?? requests.FirstOrDefault()?.Output
    ?? "rest-response.json";

await JsonFormatter.SaveToFileAsync(outputFile, results.ToList()).ConfigureAwait(false);

var summary = new ExecutionSummary
{
    OutputFile = outputFile,
    TotalElapsedMs = totalSw.ElapsedMilliseconds,
    TotalRequests = results.Length,
    SuccessfulRequests = results.Count(r => r.Response is not null),
    FailedRequests = results.Count(r => r.Error is not null)
};

ConsolePresenter.PrintSummary(summary);

return summary.FailedRequests > 0 ? 1 : 0;
