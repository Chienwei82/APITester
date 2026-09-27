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

            // Flags booleanos sin valor.
            switch (arg)
            {
                case "-v" or "--verbose":
                    cli = cli with { Verbose = true };
                    continue;
                case "-h" or "--help":
                    cli = cli with { ShowHelp = true };
                    continue;
                case "--strict":
                    cli = cli with { StrictValidation = true };
                    continue;
                case "--quiet":
                    cli = cli with { Quiet = true };
                    continue;
                case "--no-color":
                    cli = cli with { NoColor = true };
                    continue;
            }

            // Flags que aceptan un valor: "--opt <val>" o "--opt=<val>".
            var (name, inlineValue) = SplitOption(arg);

            switch (name)
            {
                case "-c" or "--config":
                    cli = cli with { ConfigFile = ReadValue("config", inlineValue, args, ref i) };
                    break;
                case "-o" or "--output":
                    cli = cli with { OutputFile = ReadValue("output", inlineValue, args, ref i) };
                    break;
                case "--format":
                    cli = cli with { OutputFormat = ReadFormat(inlineValue, args, ref i) };
                    break;
                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"Argumento desconocido: {arg}. Use --help para ver la ayuda.");
                    break; // valores posicionales sueltos: se ignoran
            }
        }

        return cli;
    }

    /// <summary>Separa "--opt=val" en ("--opt", "val"); si no hay '=', devuelve (arg, null).</summary>
    private static (string Name, string? InlineValue) SplitOption(string arg)
    {
        if (!arg.StartsWith('-'))
            return (arg, null);

        var eq = arg.IndexOf('=');
        if (eq < 0)
            return (arg, null);

        return (arg[..eq], arg[(eq + 1)..]);
    }

    private static string ReadValue(string flag, string? inline, string[] args, ref int i)
    {
        if (inline is not null)
            return inline;
        if (i + 1 < args.Length)
            return args[++i];
        throw new ArgumentException($"Falta un valor para '{flag}'");
    }

    private static OutputFormat ReadFormat(string? inline, string[] args, ref int i)
    {
        var raw = ReadValue("format", inline, args, ref i);
        return raw switch
        {
            "json" => OutputFormat.Json,
            "ndjson" => OutputFormat.Ndjson,
            _ => throw new ArgumentException(
                $"Formato invalido: {raw}. Use 'json' o 'ndjson'")
        };
    }
}
