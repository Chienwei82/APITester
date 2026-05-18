using APITester.Core.Services;

namespace APITester.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_DefaultConfig_WhenNoArgs()
    {
        var result = ArgumentParser.Parse([], "default.yaml");

        Assert.Equal("default.yaml", result.ConfigFile);
        Assert.False(result.Verbose);
        Assert.False(result.ShowHelp);
        Assert.Null(result.OutputFile);
    }

    [Fact]
    public void Parse_ConfigFile_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-c", "my-config.yaml"], "default.yaml");

        Assert.Equal("my-config.yaml", result.ConfigFile);
    }

    [Fact]
    public void Parse_ConfigFile_LongFlag()
    {
        var result = ArgumentParser.Parse(["--config", "my-config.yaml"], "default.yaml");

        Assert.Equal("my-config.yaml", result.ConfigFile);
    }

    [Fact]
    public void Parse_OutputFile_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-o", "output.json"], "default.yaml");

        Assert.Equal("output.json", result.OutputFile);
    }

    [Fact]
    public void Parse_Verbose_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-v"], "default.yaml");

        Assert.True(result.Verbose);
    }

    [Fact]
    public void Parse_Help_LongFlag()
    {
        var result = ArgumentParser.Parse(["--help"], "default.yaml");

        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void Parse_MultipleFlags_Combined()
    {
        var result = ArgumentParser.Parse(["-c", "test.yaml", "-o", "out.json", "-v"], "default.yaml");

        Assert.Equal("test.yaml", result.ConfigFile);
        Assert.Equal("out.json", result.OutputFile);
        Assert.True(result.Verbose);
        Assert.False(result.ShowHelp);
    }

    [Fact]
    public void Parse_IgnoresUnknownFlags()
    {
        var result = ArgumentParser.Parse(["-c", "test.yaml", "--unknown", "-v"], "default.yaml");

        Assert.Equal("test.yaml", result.ConfigFile);
        Assert.True(result.Verbose);
    }
}
