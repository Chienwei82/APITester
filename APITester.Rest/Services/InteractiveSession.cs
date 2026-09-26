using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Rest;

/// <summary>
/// Modo interactivo: un menu en bucle que va construyendo un <see cref="CliArgs"/>
/// y delega cada ejecucion en el pipeline via <c>execute</c>. La entrada y la
/// salida son inyectables para poder probar la sesion sin consola.
/// </summary>
public sealed class InteractiveSession
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private CliArgs _options;

    public InteractiveSession(TextReader input, TextWriter output, CliArgs? initialOptions = null)
    {
        _input = input;
        _output = output;
        _options = initialOptions ?? new CliArgs();
    }

    /// <summary>
    /// Bucle principal hasta que el usuario sale (0) o se acaba la entrada (EOF).
    /// Lanza <see cref="OperationCanceledException"/> si se cancela el token.
    /// </summary>
    public async Task<int> RunAsync(Func<CliArgs, Task<int>> execute, CancellationToken cancellationToken = default)
    {
        PrintBanner();

        try
        {
            while (true)
            {
                PrintMenu();

                var option = (await PromptAsync("Opcion: ", cancellationToken).ConfigureAwait(false)).Trim();
                switch (option)
                {
                    case "1":
                        await RunOnceAsync(execute).ConfigureAwait(false);
                        break;
                    case "2":
                        await ChooseConfigAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case "3":
                        await EditOptionsAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case "4":
                        new ConsolePresenter().PrintHelp("REST", _options.ConfigFile);
                        break;
                    case "0":
                        return 0;
                    default:
                        _output.WriteLine("Opcion no valida.");
                        break;
                }
            }
        }
        catch (EndOfStreamException)
        {
            // EOF: se cerro la entrada de la sesion y termina limpiamente.
            return 0;
        }
    }

    private async Task RunOnceAsync(Func<CliArgs, Task<int>> execute)
    {
        try
        {
            var exitCode = await execute(_options).ConfigureAwait(false);
            if (exitCode != 0)
            {
                _output.WriteLine($"La ejecucion termino con errores (codigo {exitCode}).");
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C durante la ejecucion: se cancela solo el run y se vuelve al menu.
            _output.WriteLine("Ejecucion cancelada. Volviendo al menu.");
        }
    }

    private async Task ChooseConfigAsync(CancellationToken cancellationToken)
    {
        var answer = await PromptAsync(
            $"Ruta del archivo [{_options.ConfigFile}]: ", cancellationToken).ConfigureAwait(false);
        var path = answer.Trim();
        if (path.Length == 0)
        {
            return;
        }

        if (!File.Exists(path))
        {
            _output.WriteLine($"Aviso: no se encuentra '{path}'.");
        }

        _options = _options with { ConfigFile = path };
    }

    private void PrintBanner()
    {
        _output.WriteLine("API Tester — modo interactivo");
        _output.WriteLine("Ctrl+C cancela la ejecucion en curso o, en el menu, sale de la aplicacion.");
    }

    private void PrintMenu()
    {
        _output.WriteLine();
        _output.WriteLine("Opciones:");
        _output.WriteLine($"  1) Ejecutar requests                 (config: {_options.ConfigFile})");
        _output.WriteLine("  2) Elegir archivo de configuracion");
        _output.WriteLine("  3) Opciones de ejecucion");
        _output.WriteLine("  4) Ver ayuda");
        _output.WriteLine("  0) Salir");
    }

    private async Task EditOptionsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            _output.WriteLine();
            _output.WriteLine("Opciones de ejecucion:");
            _output.WriteLine($"  1) Archivo de salida   [{_options.OutputFile ?? "automatico"}]");
            _output.WriteLine($"  2) Formato             [{FormatName(_options.OutputFormat)}]");
            _output.WriteLine($"  3) Verbose             [{OnOff(_options.Verbose)}]");
            _output.WriteLine($"  4) Strict              [{OnOff(_options.StrictValidation)}]");
            _output.WriteLine($"  5) Quiet               [{OnOff(_options.Quiet)}]");
            _output.WriteLine($"  6) Sin colores         [{OnOff(_options.NoColor)}]");
            _output.WriteLine("  0) Volver al menu");

            var option = (await PromptAsync("Opcion: ", cancellationToken).ConfigureAwait(false)).Trim();
            switch (option)
            {
                case "0":
                    return;
                case "1":
                    await EditOutputFileAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case "2":
                    var format = await ReadFormatAsync(cancellationToken).ConfigureAwait(false);
                    if (format is not null)
                    {
                        _options = _options with { OutputFormat = format.Value };
                    }
                    break;
                case "3":
                    _options = _options with { Verbose = !_options.Verbose };
                    break;
                case "4":
                    _options = _options with { StrictValidation = !_options.StrictValidation };
                    break;
                case "5":
                    _options = _options with { Quiet = !_options.Quiet };
                    break;
                case "6":
                    _options = _options with { NoColor = !_options.NoColor };
                    break;
                default:
                    _output.WriteLine("Opcion no valida.");
                    break;
            }
        }
    }

    private async Task EditOutputFileAsync(CancellationToken cancellationToken)
    {
        var answer = await PromptAsync(
            $"Archivo de salida (Enter mantiene, '-' usa el automatico) [{_options.OutputFile ?? "automatico"}]: ",
            cancellationToken).ConfigureAwait(false);

        var path = answer.Trim();
        if (path == "-")
        {
            _options = _options with { OutputFile = null };
        }
        else if (path.Length > 0)
        {
            _options = _options with { OutputFile = path };
        }
    }

    /// <summary>Pide el formato de salida; Enter (vacio) mantiene el valor actual.</summary>
    private async Task<OutputFormat?> ReadFormatAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var answer = (await PromptAsync("Formato (json|ndjson): ", cancellationToken).ConfigureAwait(false))
                .Trim()
                .ToLowerInvariant();

            switch (answer)
            {
                case "":
                    return null;
                case "json":
                    return OutputFormat.Json;
                case "ndjson":
                    return OutputFormat.Ndjson;
                default:
                    _output.WriteLine("Formato invalido: use 'json' o 'ndjson'.");
                    break;
            }
        }
    }

    /// <summary>
    /// Escribe el prompt y lee una linea. Lanza <see cref="OperationCanceledException"/>
    /// si se cancela y <see cref="EndOfStreamException"/> si se acaba la entrada.
    /// </summary>
    private async Task<string> PromptAsync(string label, CancellationToken cancellationToken)
    {
        _output.Write(label);

        var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is not null)
        {
            return line;
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new EndOfStreamException();
    }

    /// <summary>
    /// Lee una linea sin dejar bloqueada la cancelacion: si llega Ctrl+C mientras
    /// la consola espera input, se abandona la lectura (la tarea huerfana ya no se
    /// usa porque la aplicacion esta saliendo).
    /// </summary>
    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        // El token no se reenvia a ReadLineAsync a proposito: la lectura de la
        // consola no es cancelable, asi que la cancelacion se resuelve abajo con
        // Task.WhenAny. CancellationToken.None lo deja explicito para el analizador.
        var readTask = _input.ReadLineAsync(CancellationToken.None).AsTask();
        if (readTask.IsCompleted)
        {
            return await readTask.ConfigureAwait(false);
        }

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => cancelled.TrySetResult(true));

        if (await Task.WhenAny(readTask, cancelled.Task).ConfigureAwait(false) == readTask)
        {
            return await readTask.ConfigureAwait(false);
        }

        // Lectura abandonada por cancelacion: se observa cualquier fallo futuro
        // para no dejar una excepcion sin mirar.
        _ = readTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return null;
    }

    private static string OnOff(bool value) => value ? "si" : "no";

    private static string FormatName(OutputFormat format) =>
        format == OutputFormat.Ndjson ? "ndjson" : "json";
}

