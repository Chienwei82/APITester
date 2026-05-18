using APITester.Core.Services;

namespace APITester.Tests;

public class EnvVarResolverTests
{
    private const string TestVarName = "APITESTER_TEST_VAR";
    private const string TestVarValue = "resolved-value";

    public EnvVarResolverTests()
    {
        Environment.SetEnvironmentVariable(TestVarName, TestVarValue);
    }

    [Fact]
    public void Resolve_SingleVariable_Replaced()
    {
        var result = EnvVarResolver.Resolve($"prefix-${{{TestVarName}}}-suffix");

        Assert.Equal($"prefix-{TestVarValue}-suffix", result);
    }

    [Fact]
    public void Resolve_MultipleVariables_AllReplaced()
    {
        Environment.SetEnvironmentVariable("VAR_A", "alpha");
        Environment.SetEnvironmentVariable("VAR_B", "beta");

        var result = EnvVarResolver.Resolve("${VAR_A}:${VAR_B}");

        Assert.Equal("alpha:beta", result);
    }

    [Fact]
    public void Resolve_UnknownVariable_KeepsPlaceholder()
    {
        var result = EnvVarResolver.Resolve("${NONEXISTENT_VAR_XYZ}");

        Assert.Equal("${NONEXISTENT_VAR_XYZ}", result);
    }

    [Fact]
    public void Resolve_NoVariables_ReturnsOriginal()
    {
        var result = EnvVarResolver.Resolve("no-variables-here");

        Assert.Equal("no-variables-here", result);
    }

    [Fact]
    public void Resolve_NullInput_ReturnsNull()
    {
        var result = EnvVarResolver.Resolve(null as string);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Dictionary_ResolvesAllValues()
    {
        var input = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer ${{{TestVarName}}}",
            ["X-Custom"] = "static-value"
        };

        var result = EnvVarResolver.Resolve(input);

        Assert.Equal($"Bearer {TestVarValue}", result["Authorization"]);
        Assert.Equal("static-value", result["X-Custom"]);
    }
}
