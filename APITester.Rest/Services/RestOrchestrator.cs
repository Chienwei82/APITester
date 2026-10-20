using System.Diagnostics;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using APITester.Rest.Services;
using Spectre.Console;

namespace APITester.Rest;

public static class RestOrchestrator
{
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
            cliArgs = ArgumentParser.Parse(args, CliArgs.DefaultConfigFile);
        }
        catch (ArgumentException ex)
        {
            new ConsolePresenter().PrintFatalError(ex.Message);
            return 1;
        }

        if (cliArgs.ShowHelp)
        {
            new ConsolePresenter().PrintHelp("REST", CliArgs.DefaultConfigFile);
            return 0;
        }

        // Solo la entrada de la CLI (con coordinador de Ctrl+C) abre el menu: asi
        // las llamadas programaticas nunca se bloquean esperando input de consola.
        if (ctrlC is not null && args.Length == 0 && !Console.IsInputRedirected)
        {
            return await RunInteractiveAsync(cliArgs, ctrlC).ConfigureAwait(false);
        }

        // CLI directa: se ejecuta como run cancelable para que Ctrl+C cancele el
        // pipeline de forma elegante (aqui no hay prompts de la sesion en juego).
        return ctrlC is not null
            ? await ctrlC.RunCancellableAsync(runToken => RunAsync(cliArgs, runToken)).ConfigureAwait(false)
            : await RunAsync(cliArgs, cancellationToken).ConfigureAwait(false);
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
            presenter.PrintHelp("REST", CliArgs.DefaultConfigFile);
            return 0;
        }

        List<RestRequestConfig> requests;
        try
        {
            requests = await RestConfigLoader.LoadAsync(cliArgs.ConfigFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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

        return await ExecuteRequestsAsync(requests, cliArgs, cliArgs.OutputFile ?? DefaultOutputFile, presenter, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> RunInteractiveAsync(CliArgs initialOptions, CtrlCCoordinator ctrlC)
    {
        var session = new InteractiveSession(CreateConsole(initialOptions), CreateConsole, initialOptions);

        // Ctrl+C durante una ejecucion cancela solo esa ejecucion
        // (RunCancellableAsync) y vuelve al menu; en los prompts del menu el
        // proceso sale directamente (ver Program.cs y CtrlCCoordinator).
        Task<int> Execute(CliArgs cli) =>
            ctrlC.RunCancellableAsync(runToken => RunAsync(cli, runToken));

        Task<int> RunSingle(RestRequestConfig request, CliArgs cli) =>
            ctrlC.RunCancellableAsync(runToken => RunSingleRequestAsync(request, cli, runToken));

        return session.RunAsync(Execute, RunSingle, ctrlC.Token);
    }

    /// <summary>
    /// Consola TUI de la sesion interactiva: con --no-color o la variable
    /// NO_COLOR se crea sin ANSI; en el resto de casos se usa la consola global
    /// de Spectre, que detecta sola la terminal, el CI y sus capacidades.
    /// </summary>
    private static IAnsiConsole CreateConsole(CliArgs options)
    {
        var noColor = options.NoColor || Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 };
        return noColor
            ? AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                Interactive = InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(Console.Out),
            })
            : AnsiConsole.Console;
    }

    /// <summary>
    /// Ejecuta un unico request ad-hoc (el del asistente interactivo) con el
    /// mismo pipeline que la CLI: advertencias de validacion, ejecucion,
    /// guardado (solo si el request define 'output') y resumen.
    /// </summary>
    public static Task<int> RunSingleRequestAsync(
        RestRequestConfig request,
        CliArgs cliArgs,
        CancellationToken cancellationToken = default)
    {
        var presenter = new ConsolePresenter(!cliArgs.Quiet, !cliArgs.NoColor);

        var warnings = CollectWarnings([request]);
        if (warnings.Count > 0)
        {
            presenter.PrintValidationWarnings(warnings);
        }

        if (cliArgs.StrictValidation && warnings.Count > 0)
        {
            presenter.PrintFatalError("Modo estricto: hay advertencias de validacion. La ejecucion se detiene.");
            return Task.FromResult(1);
        }

        return ExecuteRequestsAsync([request], cliArgs, request.Output, presenter, cancellationToken);
    }

    /// <summary>
    /// Ejecuta los requests en orden y guarda los resultados. Si
    /// <paramref name="defaultOutput"/> es null no se escribe ningun archivo
    /// (request ad-hoc del asistente interactivo sin 'output').
    /// </summary>
    private static async Task<int> ExecuteRequestsAsync(
        List<RestRequestConfig> requests,
        CliArgs cliArgs,
        string? defaultOutput,
        ConsolePresenter presenter,
        CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();

        using var executor = new HttpExecutor(redactHeaders: cliArgs.RedactSensitiveHeaders);
        var requestExecutor = new RequestExecutor(executor, presenter, cliArgs.Verbose);
        var results = await requestExecutor.ExecuteAllAsync(requests, cancellationToken).ConfigureAwait(false);
        totalSw.Stop();

        var writtenPaths = new List<string>();
        if (defaultOutput is not null)
        {
            writtenPaths.AddRange(await SaveResultsAsync(
                results, requests, defaultOutput, cliArgs.OutputFormat, cancellationToken).ConfigureAwait(false));
        }

        // Un request solo es exitoso si hubo respuesta y el status es < 400:
        // un 404/500 debe hacer fallar el proceso (y el CI).
        var successful = results.Count(r => r.IsSuccessful);
        var summary = new ExecutionSummary
        {
            OutputFile = string.Join(", ", writtenPaths),
            TotalElapsedMs = totalSw.ElapsedMilliseconds,
            TotalRequests = results.Count,
            SuccessfulRequests = successful,
            FailedRequests = results.Count - successful
        };

        presenter.PrintSummary(summary);

        return summary.FailedRequests > 0 ? 1 : 0;
    }

    /// <summary>
    /// Escribe los resultados siguiendo el plan de escritura y devuelve las rutas
    /// que se escribieron, para informarlas en el resumen.
    /// </summary>
    private static async Task<List<string>> SaveResultsAsync(
        List<ApiResponse> results,
        List<RestRequestConfig> requests,
        string defaultOutput,
        OutputFormat format,
        CancellationToken cancellationToken)
    {
        var plan = BuildWritePlan(results, requests, defaultOutput);
        var written = new List<string>(plan.Overwrite.Count + plan.Appends.Count);

        foreach (var (path, group) in plan.Overwrite)
        {
            await JsonFormatter.SaveAsync(path, group, format, cancellationToken).ConfigureAwait(false);
            written.Add(path);
        }

        foreach (var (path, response) in plan.Appends)
        {
            await JsonFormatter.AppendToFileAsync(path, response, cancellationToken).ConfigureAwait(false);
            written.Add(path);
        }

        return written;
    }

    private static List<string> CollectWarnings(List<RestRequestConfig> requests) =>
        requests.SelectMany((r, i) => (r?.Validate() ?? []).Select(w => $"[{i + 1}] {w}")).ToList();

    public record WritePlan
    {
        public required Dictionary<string, List<ApiResponse>> Overwrite { get; init; }
        public required List<(string Path, ApiResponse Response)> Appends { get; init; }
    }

    /// <summary>
    /// Agrupa los resultados por archivo de salida para que varios requests con el
    /// mismo 'output' se escriban de una sola vez (sin que cada escritura pise a la
    /// anterior) y deja aparte los que van en modo append.
    ///
    /// La clave interna se normaliza con <see cref="Path.GetFullPath"/>: asi
    /// 'out.json' y './out.json' (o distinta capitalizacion en Windows) no generan
    /// dos grupos que escriban el mismo archivo.
    /// </summary>
    public static WritePlan BuildWritePlan(
        List<ApiResponse> results,
        List<RestRequestConfig> requests,
        string defaultOutput)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var overwriteGroups = new Dictionary<string, (string Path, List<ApiResponse> Responses)>(comparer);
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

            var key = NormalizePath(requestOutput);
            if (!overwriteGroups.TryGetValue(key, out var group))
            {
                group = (requestOutput, []);
                overwriteGroups[key] = group;
            }

            group.Responses.Add(result);
        }

        return new WritePlan
        {
            Overwrite = overwriteGroups.Values.ToDictionary(g => g.Path, g => g.Responses),
            Appends = appends
        };
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Ruta invalida para GetFullPath: se agrupa por el texto original.
            return path;
        }
    }
}
