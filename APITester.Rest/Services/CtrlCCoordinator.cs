namespace APITester.Rest;

/// <summary>
/// Coordinacion de Ctrl+C en consola. Mientras hay una ejecucion en curso (CLI
/// directa o run del menu interactivo), Ctrl+C cancela solo esa ejecucion;
/// fuera de una ejecucion cancela la aplicacion completa.
/// </summary>
public sealed class CtrlCCoordinator : IDisposable
{
    private readonly CancellationTokenSource _appCts = new();
    private volatile CancellationTokenSource? _runCts;

    /// <summary>Token de la aplicacion: se cancela con Ctrl+C si no hay ejecucion en curso.</summary>
    public CancellationToken Token => _appCts.Token;

    /// <summary>
    /// True mientras hay una ejecucion en curso (CLI directa o run del menu).
    /// Program usa esta bandera para saber si Ctrl+C debe cancelarse de forma
    /// elegante (el pipeline lo observa) o debe matar el proceso (en un prompt
    /// de la sesion interactiva, que no se puede interrumpir con tokens).
    /// </summary>
    public bool HasActiveRun => _runCts is not null;

    /// <summary>
    /// Ejecuta <paramref name="run"/> con un token propio: Ctrl+C lo cancela a el
    /// sin tocar <see cref="Token"/>, de modo que la sesion interactiva sigue viva.
    /// </summary>
    public async Task<int> RunCancellableAsync(Func<CancellationToken, Task<int>> run)
    {
        using var runCts = new CancellationTokenSource();
        _runCts = runCts;
        try
        {
            return await run(runCts.Token).ConfigureAwait(false);
        }
        finally
        {
            _runCts = null;
        }
    }

    /// <summary>Cancela la ejecucion en curso o, si no la hay, la aplicacion.</summary>
    public void Cancel()
    {
        try
        {
            if (_runCts is { IsCancellationRequested: false } runCts)
            {
                runCts.Cancel();
                return;
            }
        }
        catch (ObjectDisposedException)
        {
            // La ejecucion acabo de terminar mientras se pulsaba Ctrl+C:
            // se trata como Ctrl+C en el menu, es decir, sale de la aplicacion.
        }

        _appCts.Cancel();
    }

    public void Dispose()
    {
        _appCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
