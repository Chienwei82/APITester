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

    [Fact]
    public void Resolve_DictionaryWithUnknownVar_KeepsPlaceholder()
    {
        var input = new Dictionary<string, string>
        {
            ["Token"] = "${NONEXISTENT_VAR_12345}"
        };

        var result = EnvVarResolver.Resolve(input);

        // Unknown variables keep their placeholder in both string and dictionary resolution
        Assert.Equal("${NONEXISTENT_VAR_12345}", result["Token"]);
    }

    [Fact]
    public void Resolve_DictionaryWithStaticValues_PreservesKeys()
    {
        var input = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            ["key3"] = $"prefix-${{{TestVarName}}}-suffix"
        };

        var result = EnvVarResolver.Resolve(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
        Assert.Equal($"prefix-{TestVarValue}-suffix", result["key3"]);
    }

    [Fact]
    public void Resolve_VariableWithDefault_UsesDefaultWhenVarMissing()
    {
        var result = EnvVarResolver.Resolve("${MISSING_VAR:-fallback-value}");

        Assert.Equal("fallback-value", result);
    }

    [Fact]
    public void Resolve_VariableWithDefault_UsesEnvVarWhenExists()
    {
        var result = EnvVarResolver.Resolve($"${{{TestVarName}:-fallback-value}}");

        Assert.Equal(TestVarValue, result);
    }

    [Fact]
    public void Resolve_MultipleVariablesWithDefaults_AllResolved()
    {
        Environment.SetEnvironmentVariable("VAR_WITH_VALUE", "exists");

        var result = EnvVarResolver.Resolve("${VAR_WITH_VALUE:-default1}:${MISSING_VAR:-default2}");

        Assert.Equal("exists:default2", result);
    }
}
