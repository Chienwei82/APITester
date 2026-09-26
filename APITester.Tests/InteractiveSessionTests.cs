using APITester.Core.Models;
using APITester.Rest;
using APITester.Rest.Models;
using Spectre.Console;
using Spectre.Console.Testing;

namespace APITester.Tests;

/// <summary>
/// El menu se prueba con TestConsole emulando el teclado: flechas para moverse y
/// Enter para elegir. Indices del menu principal: 0 Ejecutar, 1 Config,
/// 2 Opciones, 3 Asistente, 4 Ayuda, 5 Salir.
/// </summary>
public class InteractiveSessionTests
{
    private const int MenuRun = 0;
    private const int MenuConfig = 1;
    private const int MenuOptions = 2;
    private const int MenuWizard = 3;
    private const int MenuExit = 5;

    private static TestConsole NewConsole()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        return console;
    }

    private static void Select(TestConsole console, int index)
    {
        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
    }

    private static Task<int> Ok(CliArgs _) => Task.FromResult(0);

    private static Task<int> NotUsed(RestRequestConfig _, CliArgs __) =>
        throw new InvalidOperationException("runSingle no deberia llamarse");

    [Fact]
    public async Task RunAsync_ExitOption_ReturnsZeroAndPrintsBanner()
    {
        var console = NewConsole();
        Select(console, MenuExit);
        var session = new InteractiveSession(console);

        var exitCode = await session.RunAsync(Ok, NotUsed);

        Assert.Equal(0, exitCode);
        Assert.Contains("modo interactivo", console.Output.ToString());
    }

    [Fact]
    public async Task RunAsync_ChooseConfigThenRun_UsesChosenFile()
    {
        var console = NewConsole();
        Select(console, MenuConfig);
        console.Input.PushTextWithEnter("my-config.json");
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(console);
        CliArgs? received = null;

        await session.RunAsync(
            cli =>
            {
                received = cli;
                return Task.FromResult(0);
            },
            NotUsed);

        Assert.Equal("my-config.json", received!.ConfigFile);
    }

    [Fact]
    public async Task RunAsync_OptionsSubMenu_UpdatesOptionsUsedOnNextRun()
    {
        var console = NewConsole();
        Select(console, MenuOptions);
        Select(console, 1);                      // Formato
        Select(console, 1);                      // ndjson
        Select(console, 2);                      // Verbose
        console.Input.PushTextWithEnter("y");
        Select(console, 6);                      // Volver
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(console);
        CliArgs? received = null;

        await session.RunAsync(
            cli =>
            {
                received = cli;
                return Task.FromResult(0);
            },
            NotUsed);

        Assert.Equal(OutputFormat.Ndjson, received!.OutputFormat);
        Assert.True(received.Verbose);
    }

    [Fact]
    public async Task RunAsync_OptionsSubMenu_OutputFile_Edit()
    {
        var console = NewConsole();
        Select(console, MenuOptions);
        Select(console, 0);                      // Archivo de salida
        console.Input.PushTextWithEnter("out.json");
        Select(console, 6);                      // Volver
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(console);
        CliArgs? received = null;

        await session.RunAsync(
            cli =>
            {
                received = cli;
                return Task.FromResult(0);
            },
            NotUsed);

        Assert.Equal("out.json", received!.OutputFile);
    }

    [Fact]
    public async Task RunAsync_OptionsSubMenu_NoColor_RecreatesConsole()
    {
        var console = NewConsole();
        var created = new List<CliArgs>();
        var session = new InteractiveSession(console, options =>
        {
            created.Add(options);
            return console;
        });

        Select(console, MenuOptions);
        Select(console, 5);                      // Sin colores
        console.Input.PushTextWithEnter("y");
        Select(console, 6);                      // Volver
        Select(console, MenuExit);

        await session.RunAsync(Ok, NotUsed);

        Assert.Contains(created, options => options.NoColor);
    }

    [Fact]
    public async Task RunAsync_RunCancelled_ReturnsToMenu()
    {
        var console = NewConsole();
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(console);

        var exitCode = await session.RunAsync(_ => throw new OperationCanceledException(), NotUsed);

        Assert.Equal(0, exitCode);
        Assert.Contains("Ejecucion cancelada", console.Output.ToString());
    }

    [Fact]
    public async Task RunAsync_FailedRun_ShowsMessageAndKeepsLooping()
    {
        var console = NewConsole();
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(console);

        var exitCode = await session.RunAsync(_ => Task.FromResult(1), NotUsed);

        Assert.Equal(0, exitCode);
        Assert.Contains("codigo 1", console.Output.ToString());
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsOperationCanceled()
    {
        var console = NewConsole();
        Select(console, MenuExit);
        var session = new InteractiveSession(console);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunAsync(Ok, NotUsed, cts.Token));
    }

    [Fact]
    public async Task RunAsync_InitialOptions_AreUsedAsDefaults()
    {
        var console = NewConsole();
        Select(console, MenuRun);
        Select(console, MenuExit);
        var session = new InteractiveSession(
            console,
            initialOptions: new CliArgs { ConfigFile = "custom.json", OutputFile = "salida.json" });
        CliArgs? received = null;

        await session.RunAsync(
            cli =>
            {
                received = cli;
                return Task.FromResult(0);
            },
            NotUsed);

        Assert.Equal("custom.json", received!.ConfigFile);
        Assert.Equal("salida.json", received.OutputFile);
    }

    [Fact]
    public async Task RunAsync_WizardDeclined_DoesNotExecute()
    {
        var console = NewConsole();
        Select(console, MenuWizard);
        PushMinimalGetRequest(console, confirmExecution: "n");
        Select(console, MenuExit);
        var session = new InteractiveSession(console);

        var exitCode = await session.RunAsync(Ok, NotUsed);

        Assert.Equal(0, exitCode);
        Assert.Contains("Asistente cancelado", console.Output.ToString());
    }

    [Fact]
    public async Task RunAsync_WizardConfirmed_ExecutesSingleRequest()
    {
        var console = NewConsole();
        Select(console, MenuWizard);
        PushMinimalGetRequest(console, confirmExecution: "y");
        Select(console, MenuExit);
        var session = new InteractiveSession(console);
        RestRequestConfig? received = null;

        var exitCode = await session.RunAsync(
            Ok,
            (request, _) =>
            {
                received = request;
                return Task.FromResult(0);
            });

        Assert.Equal(0, exitCode);
        Assert.Equal("https://api.dev/users", received!.Url);
        Assert.Equal("GET", received.Method);
        Assert.Equal("1", received.Headers!["X-Test"]);
    }

    /// <summary>Guion minimo del asistente: GET a api.dev con un header.</summary>
    private static void PushMinimalGetRequest(TestConsole console, string confirmExecution)
    {
        console.Input.PushTextWithEnter("prueba");             // Nombre
        Select(console, 0);                                     // Metodo: GET
        console.Input.PushTextWithEnter("https://api.dev/users");// URL
        console.Input.PushTextWithEnter("X-Test: 1");           // Header
        console.Input.PushTextWithEnter("");                    // Fin de headers
        console.Input.PushTextWithEnter("");                    // Fin de query
        Select(console, 0);                                     // Timeout: 30
        Select(console, 0);                                     // Reintentos: 0
        console.Input.PushTextWithEnter("");                    // Sin archivo de salida
        console.Input.PushTextWithEnter("n");                   // Sin configuracion avanzada
        console.Input.PushTextWithEnter(confirmExecution);      // Ejecutar
    }
}
