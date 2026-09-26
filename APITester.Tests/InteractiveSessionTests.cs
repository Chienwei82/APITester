using APITester.Core.Models;
using APITester.Rest;

namespace APITester.Tests;

public class InteractiveSessionTests
{
    private static (InteractiveSession Session, StringWriter Output) CreateSession(
        string input,
        CliArgs? initialOptions = null)
    {
        var output = new StringWriter();
        var session = new InteractiveSession(new StringReader(input), output, initialOptions);
        return (session, output);
    }

    [Fact]
    public async Task RunAsync_ExitOption_ReturnsZeroAndPrintsBanner()
    {
        var (session, output) = CreateSession("0\n");

        var exitCode = await session.RunAsync(_ => Task.FromResult(0));

        Assert.Equal(0, exitCode);
        Assert.Contains("modo interactivo", output.ToString());
    }

    [Fact]
    public async Task RunAsync_Eof_ReturnsZero()
    {
        var (session, _) = CreateSession(string.Empty);

        var exitCode = await session.RunAsync(_ => Task.FromResult(0));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnknownOption_ShowsErrorAndKeepsLooping()
    {
        var (session, output) = CreateSession("9\n0\n");

        var exitCode = await session.RunAsync(_ => Task.FromResult(0));

        Assert.Equal(0, exitCode);
        Assert.Contains("Opcion no valida", output.ToString());
    }

    [Fact]
    public async Task RunAsync_Execute_ReceivesCurrentOptions()
    {
        var (session, _) = CreateSession("2\nmy-config.json\n1\n0\n");
        CliArgs? received = null;

        var exitCode = await session.RunAsync(cli =>
        {
            received = cli;
            return Task.FromResult(0);
        });

        Assert.Equal(0, exitCode);
        Assert.Equal("my-config.json", received!.ConfigFile);
    }

    [Fact]
    public async Task RunAsync_OptionsSubMenu_UpdatesOptionsUsedOnNextRun()
    {
        // 3 (opciones) -> 2 jobs=8 -> 3 formato=ndjson -> 4 verbose -> 0 volver -> 1 ejecutar -> 0 salir
        var (session, _) = CreateSession("3\n2\n8\n3\nndjson\n4\n0\n1\n0\n");
        CliArgs? received = null;

        var exitCode = await session.RunAsync(cli =>
        {
            received = cli;
            return Task.FromResult(0);
        });

        Assert.Equal(0, exitCode);
        Assert.NotNull(received);
        Assert.Equal(8, received!.MaxConcurrency);
        Assert.Equal(OutputFormat.Ndjson, received.OutputFormat);
        Assert.True(received.Verbose);
    }

    [Fact]
    public async Task RunAsync_InvalidJobsValue_Reprompts()
    {
        var (session, output) = CreateSession("3\n2\n999\n8\n0\n1\n0\n");
        CliArgs? received = null;

        var exitCode = await session.RunAsync(cli =>
        {
            received = cli;
            return Task.FromResult(0);
        });

        Assert.Equal(0, exitCode);
        Assert.Contains("Valor invalido", output.ToString());
        Assert.Equal(8, received!.MaxConcurrency);
    }

    [Fact]
    public async Task RunAsync_RunCancelled_ReturnsToMenu()
    {
        var (session, output) = CreateSession("1\n0\n");

        var exitCode = await session.RunAsync(
            _ => throw new OperationCanceledException());

        Assert.Equal(0, exitCode);
        Assert.Contains("Ejecucion cancelada", output.ToString());
    }

    [Fact]
    public async Task RunAsync_FailedRun_ShowsMessageAndKeepsLooping()
    {
        var (session, output) = CreateSession("1\n0\n");

        var exitCode = await session.RunAsync(_ => Task.FromResult(1));

        Assert.Equal(0, exitCode);
        Assert.Contains("codigo 1", output.ToString());
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsOperationCanceled()
    {
        var (session, _) = CreateSession("0\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunAsync(_ => Task.FromResult(0), cts.Token));
    }

    [Fact]
    public async Task RunAsync_InitialOptions_AreUsedAsDefaults()
    {
        var (session, _) = CreateSession(
            "1\n0\n",
            new CliArgs { ConfigFile = "custom.json", MaxConcurrency = 9 });
        CliArgs? received = null;

        await session.RunAsync(cli =>
        {
            received = cli;
            return Task.FromResult(0);
        });

        Assert.Equal("custom.json", received!.ConfigFile);
        Assert.Equal(9, received.MaxConcurrency);
    }
}
