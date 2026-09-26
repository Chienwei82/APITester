using System.Text;
using APITester.Rest;

Console.OutputEncoding = Encoding.UTF8;

using var ctrlC = new CtrlCCoordinator();
Console.CancelKeyPress += (_, e) =>
{
    // Con una ejecucion en curso (CLI directa o run del menu) Ctrl+C se
    // cancela de forma elegante y el proceso sigue vivo. Sin ejecucion en
    // curso la sesion esta bloqueada en un prompt de Spectre (no cancelable
    // con tokens), asi que se deja terminar el proceso directamente.
    e.Cancel = ctrlC.HasActiveRun;
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
