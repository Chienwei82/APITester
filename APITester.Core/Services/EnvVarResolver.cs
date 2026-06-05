using System.Text.RegularExpressions;

namespace APITester.Core.Services;

public static partial class EnvVarResolver
{
    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex EnvVarPattern();

    [GeneratedRegex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)(?::-(.*))?\s*$")]
    private static partial Regex EnvVarWithDefaultPattern();

    public static string? Resolve(string? input)
    {
        if (input is null) return null;
        return EnvVarPattern().Replace(input, match =>
        {
            var varExpression = match.Groups[1].Value;
            return ResolveVariable(varExpression) ?? match.Value;
        });
    }

    private static string? ResolveVariable(string expression)
    {
        var varMatch = EnvVarWithDefaultPattern().Match(expression);
        if (!varMatch.Success)
            return null;

        var varName = varMatch.Groups[1].Value;
        var hasDefault = varMatch.Groups[2].Success;
        var defaultValue = hasDefault ? varMatch.Groups[2].Value : null;

        var envValue = Environment.GetEnvironmentVariable(varName);
        if (!string.IsNullOrEmpty(envValue))
            return envValue;

        return defaultValue;
    }

    public static Dictionary<string, string> Resolve(Dictionary<string, string> input)
    {
        return input.ToDictionary(
            kv => kv.Key,
            kv => Resolve(kv.Value) ?? string.Empty);
    }
}
