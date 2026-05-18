using System.Text.Json.Nodes;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class ConsolePresenter
{
    private static readonly object _lock = new();

    public static void PrintRequestHeader(string label, int index, int total)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{index + 1}/{total}] ");
            Console.ResetColor();
            Console.WriteLine(label);
        }
    }

    public static void PrintResponseSummary(ApiResponse response)
    {
        if (response.Error is not null)
        {
            WriteLineColored($"  ERROR: {response.Error}", ConsoleColor.Red);
            return;
        }

        var r = response.Response!;
        var color = r.StatusCode switch
        {
            >= 200 and < 300 => ConsoleColor.Green,
            >= 400 and < 500 => ConsoleColor.Yellow,
            >= 500 => ConsoleColor.Red,
            _ => ConsoleColor.Cyan
        };

        var size = FormatBytes(r.SizeBytes);
        WriteLineColored(
            $"  Estado: {r.StatusCode} {r.StatusText}  | {r.TimeMs}ms | {size}",
            color);
    }

    public static void PrintSummary(ExecutionSummary summary)
    {
        lock (_lock)
        {
            WriteLineColored($"\nSalida guardada en: {summary.OutputFile}", ConsoleColor.Green);
            Console.WriteLine($"Tiempo total: {summary.TotalElapsedMs}ms");
            Console.WriteLine($"Requests: {summary.TotalRequests} ejecutados, {summary.SuccessfulRequests} exitosos, {summary.FailedRequests} con error");
        }
    }

    public static void PrintFatalError(string message)
    {
        lock (_lock)
        {
            WriteLineColored($"Error: {message}", ConsoleColor.Red);
            Console.WriteLine("Usa --help para ver la sintaxis.");
        }
    }

    public static void PrintHelp(string protocol, string defaultConfig)
    {
        lock (_lock)
        {
            Console.WriteLine($"API Tester — Cliente {protocol} portable");
            Console.WriteLine();
            Console.WriteLine("Uso:");
            Console.WriteLine($"  dotnet run -- -c archivo.json [-o salida.json] [-v]");
            Console.WriteLine();
            Console.WriteLine("Argumentos:");
            Console.WriteLine($"  -c, --config   Archivo JSON (default: {defaultConfig})");
            Console.WriteLine("  -o, --output   Archivo de salida");
            Console.WriteLine("  -v, --verbose  Muestra detalles adicionales");
            Console.WriteLine("  -h, --help     Muestra esta ayuda");
            Console.WriteLine();
            Console.WriteLine("Variables de entorno:");
            Console.WriteLine("  Usa ${NOMBRE_VAR} en el JSON para sustituir con variables de entorno.");
            Console.WriteLine();
        }
    }

    public static void PrintVerboseLine(string? label, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            lock (_lock)
            {
                Console.WriteLine($"  {label}: {value}");
            }
        }
    }

    public static void PrintValidationWarnings(IEnumerable<string> warnings)
    {
        foreach (var w in warnings)
            WriteLineColored($"  ADVERTENCIA: {w}", ConsoleColor.Yellow);
    }

    private static void WriteLineColored(string text, ConsoleColor color)
    {
        lock (_lock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }

    private static string FormatBytes(int bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1}MB"
    };
}
