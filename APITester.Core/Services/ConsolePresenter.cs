using System.Text.Json.Nodes;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class ConsolePresenter
{
    /// <summary>Lock compartido para toda la salida en consola,
    /// de modo que la secuencia cambio-de-color/escritura/reset sea atomica
    /// incluso cuando se mezcla ConsoleLogger con ConsolePresenter.</summary>
    public static readonly object OutputLock = new();
    private static int _completedCount;
    private static int _totalCount;
    private static bool _showProgress = true;
    private static bool _useColors = true;

    public static void PrintRequestHeader(string label, int index, int total)
    {
        lock (OutputLock)
        {
            if (index == 0)
            {
                _completedCount = 0;
                _totalCount = total;
            }
            if (_showProgress && total > 1)
            {
                PrintProgressBar(_completedCount, total);
            }
            if (_useColors)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{index + 1}/{total}] ");
                Console.ResetColor();
            }
            else
            {
                Console.Write($"[{index + 1}/{total}] ");
            }
            Console.WriteLine(label);
        }
    }

    public static void SetShowProgress(bool show)
    {
        _showProgress = show;
    }

    public static void SetUseColors(bool useColors)
    {
        _useColors = useColors;
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

    public static void PrintProgress(int completed, int total)
    {
        lock (OutputLock)
        {
            _completedCount = completed;
            if (_showProgress && total > 1)
            {
                PrintProgressBar(completed, total);
            }
        }
    }

    private static void PrintProgressBar(int completed, int total)
    {
        if (total <= 1) return;

        var percent = (double)completed / total;
        var barWidth = 20;
        var filled = (int)(percent * barWidth);
        var bar = new string('█', filled) + new string('░', barWidth - filled);
        Console.Write($"\r  Progreso: [{bar}] {percent:P0} ({completed}/{total})   ");
        if (completed == total)
            Console.WriteLine();
    }

    public static void PrintSummary(ExecutionSummary summary)
    {
        lock (OutputLock)
        {
            WriteLineColored($"\nSalida guardada en: {summary.OutputFile}", ConsoleColor.Green);
            Console.WriteLine($"Tiempo total: {summary.TotalElapsedMs}ms");
            Console.WriteLine($"Requests: {summary.TotalRequests} ejecutados, {summary.SuccessfulRequests} exitosos, {summary.FailedRequests} con error");
        }
    }

    public static void PrintFatalError(string message)
    {
        lock (OutputLock)
        {
            WriteLineColored($"Error: {message}", ConsoleColor.Red);
            Console.WriteLine("Usa --help para ver la sintaxis.");
        }
    }

    public static void PrintHelp(string protocol, string defaultConfig)
    {
        lock (OutputLock)
        {
            Console.WriteLine($"API Tester — Cliente {protocol} portable");
            Console.WriteLine();
            Console.WriteLine("Uso:");
            Console.WriteLine($"  dotnet run -- -c archivo.json [-o salida.json] [-v] [-j N] [--format json|ndjson] [--strict] [--quiet] [--no-color]");
            Console.WriteLine();
            Console.WriteLine("Argumentos:");
            Console.WriteLine($"  -c, --config       Archivo JSON (default: {defaultConfig})");
            Console.WriteLine("  -o, --output       Archivo de salida");
            Console.WriteLine("  -j, --jobs N       Concurrencia maxima (default: 4, max: 100)");
            Console.WriteLine("  -v, --verbose      Muestra detalles adicionales");
            Console.WriteLine("  --format FORMAT    Formato salida: json o ndjson (default: json)");
            Console.WriteLine("  --strict           Fallar si hay advertencias de validacion");
            Console.WriteLine("  --quiet            Solo mostrar errores y resumen final");
            Console.WriteLine("  --no-color         Deshabilitar salida con colores");
            Console.WriteLine("  -h, --help         Muestra esta ayuda");
            Console.WriteLine();
            Console.WriteLine("Variables de entorno:");
            Console.WriteLine("  Usa ${NOMBRE_VAR} en el JSON para sustituir con variables de entorno.");
            Console.WriteLine("  Soporta default: ${VAR:-default}");
            Console.WriteLine();
        }
    }

    public static void PrintVerboseLine(string? label, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            lock (OutputLock)
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
        lock (OutputLock)
        {
            if (_useColors)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(text);
            }
        }
    }

    private static string FormatBytes(int bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1}MB"
    };
}
