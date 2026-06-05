using APITester.Core.Services;

namespace APITester.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_DefaultConfig_WhenNoArgs()
    {
        var result = ArgumentParser.Parse([], "rest-config.json");

        Assert.Equal("rest-config.json", result.ConfigFile);
        Assert.False(result.Verbose);
        Assert.False(result.ShowHelp);
        Assert.Null(result.OutputFile);
    }

    [Fact]
    public void Parse_ConfigFile_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-c", "my-config.json"], "rest-config.json");

        Assert.Equal("my-config.json", result.ConfigFile);
    }

    [Fact]
    public void Parse_ConfigFile_LongFlag()
    {
        var result = ArgumentParser.Parse(["--config", "my-config.json"], "rest-config.json");

        Assert.Equal("my-config.json", result.ConfigFile);
    }

    [Fact]
    public void Parse_OutputFile_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-o", "output.json"], "rest-config.json");

        Assert.Equal("output.json", result.OutputFile);
    }

    [Fact]
    public void Parse_Verbose_ShortFlag()
    {
        var result = ArgumentParser.Parse(["-v"], "rest-config.json");

        Assert.True(result.Verbose);
    }

    [Fact]
    public void Parse_Help_LongFlag()
    {
        var result = ArgumentParser.Parse(["--help"], "rest-config.json");

        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void Parse_MultipleFlags_Combined()
    {
        var result = ArgumentParser.Parse(["-c", "test.json", "-o", "out.json", "-v"], "rest-config.json");

        Assert.Equal("test.json", result.ConfigFile);
        Assert.Equal("out.json", result.OutputFile);
        Assert.True(result.Verbose);
        Assert.False(result.ShowHelp);
    }

    [Fact]
    public void Parse_IgnoresUnknownFlags()
    {
        var result = ArgumentParser.Parse(["-c", "test.json", "--unknown", "-v"], "rest-config.json");

        Assert.Equal("test.json", result.ConfigFile);
        Assert.True(result.Verbose);
    }
}
