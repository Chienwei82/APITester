namespace APITester.Core.Models;

public record CliArgs
{
    public string ConfigFile { get; init; } = "rest-config.json";
    public string? OutputFile { get; init; }
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
}
