namespace APITester.Core.Services;

public class RetryPolicy
{
    public int MaxRetries { get; init; }
    public int DelayMs { get; init; } = 1000;
    public bool UseExponentialBackoff { get; init; }
    public List<int>? RetryOnStatusCodes { get; init; }

    private static readonly ThreadLocal<Random> _random = new(() => new Random());

    public static RetryPolicy None => new();

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        T? lastResult = default;
        var hasLastResult = false;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await operation(cancellationToken).ConfigureAwait(false);

                // If result has a status code and we should retry on it, continue
                if (attempt < MaxRetries && result is IHasStatusCode hasStatus && ShouldRetryOnStatus(hasStatus.StatusCode))
                {
                    lastResult = result;
                    hasLastResult = true;
                    var delay = CalculateDelay(attempt);
                    logger?.Warn($"Intento {attempt + 1}/{MaxRetries} fallido (status {hasStatus.StatusCode}), reintentando en {delay}ms...");
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
                    logger?.Warn($"Intento {attempt + 1}/{MaxRetries} fallido, reintentando en {delay}ms...");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (hasLastResult)
            return lastResult!;

        throw new InvalidOperationException(
            $"Operacion fallo despues de {MaxRetries + 1} intentos: {lastException?.Message}", lastException);
    }

    private bool ShouldRetryOnStatus(int statusCode)
    {
        if (RetryOnStatusCodes is null || RetryOnStatusCodes.Count == 0)
            return false;
        return RetryOnStatusCodes.Contains(statusCode);
    }

    internal int CalculateDelay(int attempt)
    {
        if (!UseExponentialBackoff)
            return DelayMs;

        // Limitar el exponente: 2^attempt desborda int con muchos intentos.
        // La suma con jitter se hace en long para evitar overflow al saturar.
        var exponential = (double)DelayMs * Math.Pow(2, Math.Min(attempt, 62));
        var delay = (int)Math.Min(exponential, int.MaxValue);
        var jitter = (int)(_random.Value!.NextDouble() * DelayMs);
        return (int)Math.Min((long)delay + jitter, 30000);
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
