using System.Diagnostics;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Rest;

public static class RestOrchestrator
{
    private const string DefaultConfigFile = "rest-config.json";
    private const string DefaultOutputFile = "rest-response.json";

    /// <summary>
    /// Entrada de la CLI: coordina Ctrl+C y, cuando se invoca sin argumentos y con
    /// la entrada de consola disponible, abre el modo interactivo.
    /// </summary>
    public static Task<int> RunCliAsync(string[] args, CtrlCCoordinator ctrlC) =>
        RunAsync(args, ctrlC, ctrlC.Token);

    /// <summary>
    /// Ejecucion directa, sin menu interactivo (tests e integracion): parsea los
    /// argumentos y ejecuta el pipeline con el token recibido.
    /// </summary>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default) =>
        RunAsync(args, ctrlC: null, cancellationToken);

    private static async Task<int> RunAsync(string[] args, CtrlCCoordinator? ctrlC, CancellationToken cancellationToken)
    {
        CliArgs cliArgs;
        try
        {
            cliArgs = ArgumentParser.Parse(args, DefaultConfigFile);
        }
        catch (ArgumentException ex)
        {
            new ConsolePresenter().PrintFatalError(ex.Message);
            return 1;
        }

        if (cliArgs.ShowHelp)
        {
            new ConsolePresenter().PrintHelp("REST", DefaultConfigFile);
            return 0;
        }

        // Solo la entrada de la CLI (con coordinador de Ctrl+C) abre el menu: asi
        // las llamadas programaticas nunca se bloquean esperando input de consola.
        if (ctrlC is not null && args.Length == 0 && !Console.IsInputRedirected)
        {
            return await RunInteractiveAsync(cliArgs, ctrlC).ConfigureAwait(false);
        }

        return await RunAsync(cliArgs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pipeline compartido por la CLI y el modo interactivo: carga la configuracion,
    /// la valida, ejecuta los requests y guarda las respuestas.
    /// </summary>
    public static async Task<int> RunAsync(CliArgs cliArgs, CancellationToken cancellationToken = default)
    {
        var presenter = new ConsolePresenter(!cliArgs.Quiet, !cliArgs.NoColor);

        if (cliArgs.ShowHelp)
        {
            presenter.PrintHelp("REST", DefaultConfigFile);
            return 0;
        }

        List<RestRequestConfig> requests;
        try
        {
            requests = await RestConfigLoader.LoadAsync(cliArgs.ConfigFile).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            presenter.PrintFatalError(ex.Message);
            return 1;
        }

        var warnings = CollectWarnings(requests);
        if (warnings.Count > 0)
        {
            presenter.PrintValidationWarnings(warnings);
        }

        if (cliArgs.StrictValidation && warnings.Count > 0)
        {
            presenter.PrintFatalError("Modo estricto: hay advertencias de validacion. La ejecucion se detiene.");
            return 1;
        }

        if (requests.Count == 0)
        {
            presenter.PrintFatalError("No se encontraron requests en el archivo de configuracion");
            return 1;
        }

        return await ExecuteRequestsAsync(requests, cliArgs, presenter, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> RunInteractiveAsync(CliArgs initialOptions, CtrlCCoordinator ctrlC)
    {
        var session = new InteractiveSession(Console.In, Console.Out, initialOptions);

        // Ctrl+C durante una ejecucion cancela solo esa ejecucion
        // (RunCancellableAsync) y vuelve al menu; en los prompts del menu cancela
        // el token de la sesion y la sesion termina.
        Task<int> Execute(CliArgs cli) =>
            ctrlC.RunCancellableAsync(runToken => RunAsync(cli, runToken));

        return session.RunAsync(Execute, ctrlC.Token);
    }

    private static async Task<int> ExecuteRequestsAsync(
        List<RestRequestConfig> requests,
        CliArgs cliArgs,
        ConsolePresenter presenter,
        CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();

        using var executor = new HttpExecutor();
        var requestExecutor = new RequestExecutor(executor, presenter, cliArgs.MaxConcurrency, cliArgs.Verbose);
        var results = await requestExecutor.ExecuteAllAsync(requests, cancellationToken).ConfigureAwait(false);
        totalSw.Stop();

        var defaultOutput = cliArgs.OutputFile ?? DefaultOutputFile;
        await SaveResultsAsync(results, requests, defaultOutput, cliArgs.OutputFormat).ConfigureAwait(false);

        var summary = new ExecutionSummary
        {
            OutputFile = defaultOutput,
            TotalElapsedMs = totalSw.ElapsedMilliseconds,
            TotalRequests = results.Count,
            SuccessfulRequests = results.Count(r => r.Response is not null),
            FailedRequests = results.Count(r => r.Error is not null)
        };

        presenter.PrintSummary(summary);

        return summary.FailedRequests > 0 ? 1 : 0;
    }

    /// <summary>Escribe los resultados siguiendo el plan de escritura.</summary>
    private static async Task SaveResultsAsync(
        List<ApiResponse> results,
        List<RestRequestConfig> requests,
        string defaultOutput,
        OutputFormat format)
    {
        var plan = BuildWritePlan(results, requests, defaultOutput);

        foreach (var (path, group) in plan.Overwrite)
        {
            await JsonFormatter.SaveAsync(path, group, format).ConfigureAwait(false);
        }

        foreach (var (path, response) in plan.Appends)
        {
            await JsonFormatter.AppendToFileAsync(path, response).ConfigureAwait(false);
        }
    }

    private static List<string> CollectWarnings(List<RestRequestConfig> requests) =>
        requests.SelectMany((r, i) => r.Validate().Select(w => $"[{i + 1}] {w}")).ToList();

    public record WritePlan
    {
        public required Dictionary<string, List<ApiResponse>> Overwrite { get; init; }
        public required List<(string Path, ApiResponse Response)> Appends { get; init; }
    }

    /// <summary>
    /// Agrupa los resultados por archivo de salida para que varios requests con el
    /// mismo 'output' se escriban de una sola vez (sin que cada escritura pise a la
    /// anterior) y deja aparte los que van en modo append.
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

            if (!overwriteGroups.TryGetValue(requestOutput, out var group))
            {
                group = [];
                overwriteGroups[requestOutput] = group;
            }

            group.Add(result);
        }

        return new WritePlan { Overwrite = overwriteGroups, Appends = appends };
    }
}
