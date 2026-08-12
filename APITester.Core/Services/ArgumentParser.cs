using APITester.Core.Models;

namespace APITester.Core.Services;

public static class ArgumentParser
{
    public static CliArgs Parse(string[] args, string defaultConfig)
    {
        var cli = new CliArgs { ConfigFile = defaultConfig };

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--config=", StringComparison.Ordinal))
            {
                cli = cli with { ConfigFile = arg["--config=".Length..] };
                continue;
            }
            if (arg.StartsWith("--output=", StringComparison.Ordinal))
            {
                cli = cli with { OutputFile = arg["--output=".Length..] };
                continue;
            }
            if (arg.StartsWith("--jobs=", StringComparison.Ordinal) || arg.StartsWith("--concurrency=", StringComparison.Ordinal))
            {
                var value = arg.Contains('=') ? arg.Split('=', 2)[1] : "";
                if (int.TryParse(value, out var jobs) && jobs > 0 && jobs <= 100)
                    cli = cli with { MaxConcurrency = jobs };
                else
                    throw new ArgumentException($"Valor invalido para {arg}: se espera un entero entre 1 y 100");
                continue;
            }
            if (arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                var value = arg["--format=".Length..];
                if (value is "json" or "ndjson")
                    cli = cli with { OutputFormat = value };
                else
                    throw new ArgumentException($"Formato invalido: {value}. Use 'json' o 'ndjson'");
                continue;
            }

            switch (arg)
            {
                case "-c" or "--config" when i + 1 < args.Length:
                    cli = cli with { ConfigFile = args[++i] };
                    break;
                case "-o" or "--output" when i + 1 < args.Length:
                    cli = cli with { OutputFile = args[++i] };
                    break;
                case "-j" or "--jobs" or "--concurrency" when i + 1 < args.Length:
                    if (int.TryParse(args[i + 1], out var jobs) && jobs > 0 && jobs <= 100)
                        cli = cli with { MaxConcurrency = jobs };
                    else
                        throw new ArgumentException($"Valor invalido para {arg}: se espera un entero entre 1 y 100");
                    i++;
                    break;
                case "-v" or "--verbose":
                    cli = cli with { Verbose = true };
                    break;
                case "-h" or "--help":
                    cli = cli with { ShowHelp = true };
                    break;
                case "--format" when i + 1 < args.Length:
                    var format = args[++i];
                    if (format is "json" or "ndjson")
                        cli = cli with { OutputFormat = format };
                    else
                        throw new ArgumentException($"Formato invalido: {format}. Use 'json' o 'ndjson'");
                    break;
                case "--strict":
                    cli = cli with { StrictValidation = true };
                    break;
                case "--quiet":
                    cli = cli with { Quiet = true };
                    break;
                case "--no-color":
                    cli = cli with { NoColor = true };
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"Argumento desconocido: {arg}. Use --help para ver la ayuda.");
                    break;
            }
        }

        return cli;
    }
}
