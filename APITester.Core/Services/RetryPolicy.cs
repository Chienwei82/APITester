namespace APITester.Core.Services;

/// <summary>
/// Politica de reintentos de una operacion. Es un struct inmutable: no se asigna
/// en cada request y no comparte estado entre ejecuciones.
/// </summary>
public readonly record struct RetryPolicy
{
    /// <summary>Tope del backoff exponencial (30s).</summary>
    public const int MaxDelayMs = 30_000;

    public int MaxRetries { get; init; }
    public int DelayMs { get; init; }
    public bool UseExponentialBackoff { get; init; }
    public IReadOnlyList<int>? RetryOnStatusCodes { get; init; }

    public static RetryPolicy None => new() { DelayMs = 1000 };

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await operation(cancellationToken).ConfigureAwait(false);

                if (attempt < MaxRetries && result is IHasStatusCode status && ShouldRetryOnStatus(status.StatusCode))
                {
                    lastException = new HttpRequestException($"La API respondio con status {status.StatusCode}");
                    var delay = CalculateDelay(attempt);
                    logger?.Warn($"Intento {attempt + 1}/{MaxRetries + 1} fallido (status {status.StatusCode}), reintentando en {delay}ms...");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return result;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    var delay = CalculateDelay(attempt);
                    logger?.Warn($"Intento {attempt + 1}/{MaxRetries + 1} fallido ({ex.Message}), reintentando en {delay}ms...");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // Se propaga la causa del ultimo intento: el usuario necesita saber por que fallo.
        throw new InvalidOperationException(DescribeFailure(lastException), lastException);
    }

    private string DescribeFailure(Exception? lastException)
    {
        var attempts = $"{MaxRetries + 1} intentos";
        return lastException is null
            ? $"Operacion fallo despues de {attempts}"
            : $"Operacion fallo despues de {attempts}: {lastException.Message}";
    }

    private bool ShouldRetryOnStatus(int statusCode) =>
        RetryOnStatusCodes is { Count: > 0 } codes && codes.Contains(statusCode);

    private int CalculateDelay(int attempt)
    {
        // Un valor negativo (config invalida) se trata como 0 para no romper Task.Delay.
        var baseDelay = Math.Max(DelayMs, 0);

        if (!UseExponentialBackoff)
            return baseDelay;

        // Shift sobre long: evita el overflow de DelayMs * 2^attempt en int.
        var exponential = (long)baseDelay << Math.Min(attempt, 16);
        var jitter = Random.Shared.Next(baseDelay + 1);
        return (int)Math.Min(exponential + jitter, MaxDelayMs);
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or TaskCanceledException or OperationCanceledException;
    }
}

public interface IHasStatusCode
{
    int StatusCode { get; }
}
