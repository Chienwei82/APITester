using System.Text.RegularExpressions;

namespace APITester.Core.Services;

public static partial class EnvVarResolver
{
    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex EnvVarPattern();

    public static string? Resolve(string? input)
    {
        if (input is null) return null;
        return EnvVarPattern().Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }

    public static Dictionary<string, string> Resolve(Dictionary<string, string> input)
    {
        return input.ToDictionary(
            kv => kv.Key,
            kv => Resolve(kv.Value)!);
    }
}
