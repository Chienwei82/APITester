using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using Spectre.Console;

namespace APITester.Rest;

/// <summary>
/// Modo interactivo TUI (Spectre.Console): menu con navegacion por flechas que
/// va construyendo un <see cref="CliArgs"/> y delega cada ejecucion en el
/// pipeline; incluye el asistente para ejecutar un request paso a paso. La
/// consola es inyectable (<see cref="IAnsiConsole"/>) para probarla sin consola.
/// </summary>
public sealed class InteractiveSession
{
    private readonly Func<CliArgs, IAnsiConsole> _createConsole;
    private IAnsiConsole _console;
    private CliArgs _options;

    private enum MainMenu { Run, Config, Options, Wizard, Help, Exit }

    private enum OptionsMenu { Output, Format, Verbose, Strict, Quiet, NoColor, Back }

    public InteractiveSession(
        IAnsiConsole console,
        Func<CliArgs, IAnsiConsole>? createConsole = null,
        CliArgs? initialOptions = null)
    {
        _console = console;
        _createConsole = createConsole ?? (_ => console);
        _options = initialOptions ?? new CliArgs();
    }

    /// <summary>
    /// Bucle principal hasta que el usuario elige Salir. Lanza
    /// <see cref="OperationCanceledException"/> si se cancela el token. No hay EOF
    /// que tratar: el modo interactivo solo se abre con una terminal real
    /// (<c>Console.IsInputRedirected == false</c>), donde la lectura de teclas
    /// nunca devuelve fin de entrada.
    /// </summary>
    public async Task<int> RunAsync(
        Func<CliArgs, Task<int>> execute,
        Func<RestRequestConfig, CliArgs, Task<int>> runSingle,
        CancellationToken cancellationToken = default)
    {
        Tui.Banner(_console);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var option = Tui.Choose(_console, "Opciones:", FormatMainMenu,
                Enum.GetValues<MainMenu>(), cancellationToken);

            switch (option)
            {
                case MainMenu.Run:
                    await RunAndReportAsync(() => execute(_options)).ConfigureAwait(false);
                    break;
                case MainMenu.Config:
                    ChooseConfig(cancellationToken);
                    break;
                case MainMenu.Options:
                    EditOptions(cancellationToken);
                    break;
                case MainMenu.Wizard:
                    await RunWizardAsync(runSingle, cancellationToken).ConfigureAwait(false);
                    break;
                case MainMenu.Help:
                    new ConsolePresenter().PrintHelp("REST", _options.ConfigFile);
                    break;
                case MainMenu.Exit:
                    return 0;
            }
        }
    }

    private async Task RunAndReportAsync(Func<Task<int>> run)
    {
        try
        {
            var exitCode = await run().ConfigureAwait(false);
            if (exitCode != 0)
            {
                Tui.Error(_console, $"La ejecucion termino con errores (codigo {exitCode}).");
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C durante la ejecucion: se cancela solo el run y se vuelve al menu.
            Tui.Warn(_console, "Ejecucion cancelada. Volviendo al menu.");
        }
    }

    private async Task RunWizardAsync(
        Func<RestRequestConfig, CliArgs, Task<int>> runSingle,
        CancellationToken cancellationToken)
    {
        var request = new RequestWizard(_console).Build(cancellationToken);
        if (request is null)
        {
            Tui.Info(_console, "Asistente cancelado. Volviendo al menu.");
            return;
        }

        await RunAndReportAsync(() => runSingle(request, _options)).ConfigureAwait(false);
    }

    private void ChooseConfig(CancellationToken cancellationToken)
    {
        var path = Tui.AskTextOptional(_console, "Ruta del archivo", cancellationToken).Trim();
        if (path.Length == 0)
        {
            return;
        }

        if (!File.Exists(path))
        {
            Tui.Warn(_console, $"No se encuentra '{path}'.");
        }

        _options = _options with { ConfigFile = path };
    }

    private void EditOptions(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = Tui.Choose(_console, "Opciones de ejecucion:", FormatOptionsMenu,
                Enum.GetValues<OptionsMenu>(), cancellationToken);

            switch (item)
            {
                case OptionsMenu.Back:
                    return;
                case OptionsMenu.Output:
                    EditOutputFile(cancellationToken);
                    break;
                case OptionsMenu.Format:
                    var format = Tui.Choose(_console, "Formato de salida:", FormatName,
                        [OutputFormat.Json, OutputFormat.Ndjson], cancellationToken);
                    _options = _options with { OutputFormat = format };
                    break;
                case OptionsMenu.Verbose:
                    _options = _options with
                    {
                        Verbose = Tui.Confirm(_console, "Verbose", _options.Verbose, cancellationToken),
                    };
                    break;
                case OptionsMenu.Strict:
                    _options = _options with
                    {
                        StrictValidation = Tui.Confirm(_console, "Validacion estricta", _options.StrictValidation, cancellationToken),
                    };
                    break;
                case OptionsMenu.Quiet:
                    _options = _options with
                    {
                        Quiet = Tui.Confirm(_console, "Quiet (solo errores y resumen)", _options.Quiet, cancellationToken),
                    };
                    break;
                case OptionsMenu.NoColor:
                    _options = _options with
                    {
                        NoColor = Tui.Confirm(_console, "Sin colores", _options.NoColor, cancellationToken),
                    };
                    // La nueva opcion se refleja recreando la consola TUI.
                    _console = _createConsole(_options);
                    break;
            }
        }
    }

    private void EditOutputFile(CancellationToken cancellationToken)
    {
        var answer = Tui.AskTextOptional(_console,
            "Archivo de salida (Enter mantiene, '-' usa el automatico)", cancellationToken).Trim();

        if (answer == "-")
        {
            _options = _options with { OutputFile = null };
        }
        else if (answer.Length > 0)
        {
            _options = _options with { OutputFile = answer };
        }
    }

    private string FormatMainMenu(MainMenu item) => item switch
    {
        MainMenu.Run => $"[cyan]1)[/] Ejecutar requests  [grey](config: {Tui.Esc(_options.ConfigFile)})[/]",
        MainMenu.Config => "[cyan]2)[/] Elegir archivo de configuracion",
        MainMenu.Options => "[cyan]3)[/] Opciones de ejecucion",
        MainMenu.Wizard => "[cyan]4)[/] Ejecutar un request (paso a paso)",
        MainMenu.Help => "[cyan]5)[/] Ver ayuda",
        _ => "[cyan]0)[/] Salir",
    };

    private string FormatOptionsMenu(OptionsMenu item) => item switch
    {
        OptionsMenu.Output => $"[cyan]1)[/] Archivo de salida  [grey]({_options.OutputFile ?? "automatico"})[/]",
        OptionsMenu.Format => $"[cyan]2)[/] Formato  [grey]({FormatName(_options.OutputFormat)})[/]",
        OptionsMenu.Verbose => $"[cyan]3)[/] Verbose  [grey]({OnOff(_options.Verbose)})[/]",
        OptionsMenu.Strict => $"[cyan]4)[/] Strict  [grey]({OnOff(_options.StrictValidation)})[/]",
        OptionsMenu.Quiet => $"[cyan]5)[/] Quiet  [grey]({OnOff(_options.Quiet)})[/]",
        OptionsMenu.NoColor => $"[cyan]6)[/] Sin colores  [grey]({OnOff(_options.NoColor)})[/]",
        _ => "[cyan]0)[/] Volver al menu",
    };

    private static string OnOff(bool value) => value ? "si" : "no";

    private static string FormatName(OutputFormat format) =>
        format == OutputFormat.Ndjson ? "ndjson" : "json";
}
