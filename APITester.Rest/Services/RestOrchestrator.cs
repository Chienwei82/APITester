using System.Diagnostics;
using System.Text;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public class RestOrchestrator
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        CliArgs cliArgs;
        try
        {
            cliArgs = ArgumentParser.Parse(args, "rest-config.json");
        }
        catch (ArgumentException ex)
        {
            ConsolePresenter.PrintFatalError(ex.Message);
            return 1;
        }

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

    private static async Task<int> ExecuteRequestsAsync(
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
        var plan = BuildWritePlan(results, requests, defaultOutput);

        if (cliArgs.OutputFormat == "ndjson")
        {
            foreach (var (path, group) in plan.Overwrite)
                await JsonFormatter.SaveToFileNdjsonAsync(path, group).ConfigureAwait(false);
        }
        else
        {
            foreach (var (path, group) in plan.Overwrite)
                await JsonFormatter.SaveToFileAsync(path, group).ConfigureAwait(false);
        }

        foreach (var (path, response) in plan.Appends)
            await JsonFormatter.AppendToFileAsync(path, response).ConfigureAwait(false);

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

    /// <summary>Plan de escritura: resultados no-append agrupados por archivo y appends individuales.</summary>
    public record WritePlan
    {
        public required Dictionary<string, List<ApiResponse>> Overwrite { get; init; }
        public required List<(string Path, ApiResponse Response)> Appends { get; init; }
    }

    /// <summary>
    /// Agrupa resultados por archivo de salida para que varios requests con el mismo 'output'
    /// se escriban de una sola vez (evitando que cada escritura pise a la anterior).
    /// </summary>
    public static WritePlan BuildWritePlan(
        List<ApiResponse> results,
        List<RestRequestConfig> requests,
        string defaultOutput)
    {
        var overwriteGroups = new Dictionary<string, List<ApiResponse>>();
        var appends = new List<(string Path, ApiResponse Response)>();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var config = requests[i];
            var requestOutput = config.Output ?? defaultOutput;

            if (config.AppendOutput)
            {
                appends.Add((requestOutput, result));
                continue;
            }

            var group = overwriteGroups.TryGetValue(requestOutput, out var existing)
                ? existing
                : new List<ApiResponse>();
            group.Add(result);
            overwriteGroups[requestOutput] = group;
        }

        return new WritePlan { Overwrite = overwriteGroups, Appends = appends };
    }
}
