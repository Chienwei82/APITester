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
    var orchestrator = new RestOrchestrator();
    return await orchestrator.RunAsync(Environment.GetCommandLineArgs().Skip(1).ToArray(), cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nEjecucion cancelada por el usuario.");
    return 130;
}
catch (AggregateException ex) when (ex.InnerExceptions.Any(e => e is OperationCanceledException))
{
    Console.WriteLine("\nEjecucion cancelada por el usuario.");
    return 130;
}
