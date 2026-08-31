namespace APITester.Core.Models;

public record CliArgs
{
    /// <summary>Archivo de configuracion por defecto (unico punto de verdad).</summary>
    public const string DefaultConfigFile = "rest-config.json";

    public string ConfigFile { get; init; } = DefaultConfigFile;
    public string? OutputFile { get; init; }
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
    public int MaxConcurrency { get; init; } = 4;
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Json;
    public bool StrictValidation { get; init; }
    public bool Quiet { get; init; }
    public bool NoColor { get; init; }

    /// <summary>Redactar headers con credenciales en la salida (default: true).</summary>
    public bool RedactSensitiveHeaders { get; init; } = true;
}
