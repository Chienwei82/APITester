using System.Diagnostics;
using System.Text;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public class RestOrchestrator
{
    private readonly TextWriter _out;

    public RestOrchestrator(TextWriter? outWriter = null)
    {
        _out = outWriter ?? Console.Out;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var cliArgs = ArgumentParser.Parse(args, "rest-config.json");

        if (cliArgs.ShowHelp)
        {
            ConsolePresenter.PrintHelp("REST", "rest-config.json");
            return 0;
        }

        ConsolePresenter.SetShowProgress(!cliArgs.Quiet);
        ConsolePresenter.SetUseColors(!cliArgs.NoColor);

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

        if (cliArgs.StrictValidation && warnings.Count > 0)
        {
            ConsolePresenter.PrintFatalError("Modo estricto: hay advertencias de validacion. La ejecucion se detiene.");
            return 1;
        }

        if (requests.Count == 0)
        {
            ConsolePresenter.PrintFatalError("No se encontraron requests en el archivo de configuracion");
            return 1;
        }

        return await ExecuteRequestsAsync(requests, cliArgs, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteRequestsAsync(
        List<RestRequestConfig> requests,
        CliArgs cliArgs,
        CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();

        using var executor = new HttpExecutor();
        var requestExecutor = new RequestExecutor(executor, cliArgs.MaxConcurrency, cliArgs.Verbose);

        var results = await requestExecutor.ExecuteAllAsync(requests, cancellationToken).ConfigureAwait(false);
        totalSw.Stop();

        var defaultOutput = cliArgs.OutputFile ?? "rest-response.json";

        // Process each request with its own output file
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var config = requests[i];

            var requestOutput = config.Output ?? defaultOutput;

            if (config.AppendOutput)
            {
                await JsonFormatter.AppendToFileAsync(requestOutput, result).ConfigureAwait(false);
            }
            else if (cliArgs.OutputFormat == "ndjson")
            {
                await JsonFormatter.SaveToFileNdjsonAsync(requestOutput, new List<ApiResponse> { result }).ConfigureAwait(false);
            }
            else
            {
                await JsonFormatter.SaveToFileAsync(requestOutput, new List<ApiResponse> { result }).ConfigureAwait(false);
            }
        }

        var summary = new ExecutionSummary
        {
            OutputFile = defaultOutput,
            TotalElapsedMs = totalSw.ElapsedMilliseconds,
            TotalRequests = results.Count,
            SuccessfulRequests = results.Count(r => r.Response is not null),
            FailedRequests = results.Count(r => r.Error is not null)
        };

        ConsolePresenter.PrintSummary(summary);

        return summary.FailedRequests > 0 ? 1 : 0;
    }
}
