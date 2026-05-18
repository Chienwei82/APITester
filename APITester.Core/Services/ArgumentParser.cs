using APITester.Core.Models;

namespace APITester.Core.Services;

public static class ArgumentParser
{
    public static CliArgs Parse(string[] args, string defaultConfig)
    {
        var cli = new CliArgs { ConfigFile = defaultConfig };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-c" or "--config" when i + 1 < args.Length:
                    cli = cli with { ConfigFile = args[++i] };
                    break;
                case "-o" or "--output" when i + 1 < args.Length:
                    cli = cli with { OutputFile = args[++i] };
                    break;
                case "-v" or "--verbose":
                    cli = cli with { Verbose = true };
                    break;
                case "-h" or "--help":
                    cli = cli with { ShowHelp = true };
                    break;
            }
        }

        return cli;
    }
}
