namespace APITester.Core.Models;

public record CliArgs
{
    public string ConfigFile { get; init; } = "rest-config.json";
    public string? OutputFile { get; init; }
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
    public int MaxConcurrency { get; init; } = 4;
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Json;
    public bool StrictValidation { get; init; }
    public bool Quiet { get; init; }
    public bool NoColor { get; init; }
}
