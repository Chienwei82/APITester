namespace APITester.Core.Services;

public class ConsoleLogger : ILogger
{
    private readonly TextWriter _writer;
    private readonly object _lock = new();

    public ConsoleLogger(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
    }

    public void Info(string message) =>
        WriteLine($"[INFO] {message}", ConsoleColor.White);

    public void Warn(string message) =>
        WriteLine($"[ADVERTENCIA] {message}", ConsoleColor.Yellow);

    public void LogError(string message) =>
        WriteLine($"[ERROR] {message}", ConsoleColor.Red);

    public void Debug(string message) =>
        WriteLine($"[DEBUG] {message}", ConsoleColor.DarkGray);

    private void WriteLine(string text, ConsoleColor color)
    {
        // Lock MUST cover the entire color-change-write-reset sequence.
        // Minimizing the lock scope would allow other threads to change
        // Console.ForegroundColor between our SetColor and WriteLine calls,
        // causing mixed/corrupted console output.
        lock (_lock)
        {
            if (_writer == Console.Out || _writer == Console.Error)
            {
                Console.ForegroundColor = color;
                _writer.WriteLine(text);
                Console.ResetColor();
            }
            else
            {
                _writer.WriteLine(text);
            }
        }
    }
}
