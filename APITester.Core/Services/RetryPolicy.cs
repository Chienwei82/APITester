namespace APITester.Core.Services;

public class RetryPolicy
{
    public int MaxRetries { get; init; } = 0;
    public int DelayMs { get; init; } = 1000;

    public static RetryPolicy None => new();

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    logger?.Warn($"Intento {attempt + 1}/{MaxRetries} fallido, reintentando en {DelayMs}ms...");
                    await Task.Delay(DelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException(
            $"Operacion fallo despues de {MaxRetries + 1} intentos", lastException);
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            return false;

        return ex is HttpRequestException or TaskCanceledException or OperationCanceledException;
    }
}
