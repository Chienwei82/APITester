using System.Text;
using APITester.Rest;

Console.OutputEncoding = Encoding.UTF8;

using var ctrlC = new CtrlCCoordinator();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    ctrlC.Cancel();
};

try
{
    return await RestOrchestrator.RunCliAsync(args, ctrlC);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nEjecucion cancelada por el usuario.");
    return 130;
}
