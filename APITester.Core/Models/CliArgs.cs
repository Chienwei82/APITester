namespace APITester.Core.Models;

public record CliArgs
{
    public string ConfigFile { get; init; } = "config.yaml";
    public string? OutputFile { get; init; }
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
}
