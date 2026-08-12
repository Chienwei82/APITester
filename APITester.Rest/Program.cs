using System.Text;
using APITester.Rest;

Console.OutputEncoding = Encoding.UTF8;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    return await RestOrchestrator.RunAsync(Environment.GetCommandLineArgs().Skip(1).ToArray(), cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nEjecucion cancelada por el usuario.");
    return 130;
}
