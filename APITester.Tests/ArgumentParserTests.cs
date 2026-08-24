using APITester.Core.Models;
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
    public void Parse_UnknownFlag_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["-c", "test.json", "--unknown", "-v"], "rest-config.json"));

        Assert.Contains("Argumento desconocido: --unknown", ex.Message);
    }

    [Fact]
    public void Parse_EqualsSyntax_ConfigFile()
    {
        var result = ArgumentParser.Parse(["--config=my-config.json"], "rest-config.json");

        Assert.Equal("my-config.json", result.ConfigFile);
    }

    [Fact]
    public void Parse_EqualsSyntax_OutputFile()
    {
        var result = ArgumentParser.Parse(["--output=out.json"], "rest-config.json");

        Assert.Equal("out.json", result.OutputFile);
    }

    [Fact]
    public void Parse_EqualsSyntax_Jobs()
    {
        var result = ArgumentParser.Parse(["--jobs=8"], "rest-config.json");

        Assert.Equal(8, result.MaxConcurrency);
    }

    [Fact]
    public void Parse_EqualsSyntax_Format()
    {
        var result = ArgumentParser.Parse(["--format=ndjson"], "rest-config.json");

        Assert.Equal(OutputFormat.Ndjson, result.OutputFormat);
    }

    [Fact]
    public void Parse_DefaultFormat_IsJson()
    {
        var result = ArgumentParser.Parse([], "rest-config.json");

        Assert.Equal(OutputFormat.Json, result.OutputFormat);
    }

    [Fact]
    public void Parse_InvalidJobsValue_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--jobs=invalid"], "rest-config.json"));

        Assert.Contains("Valor invalido", ex.Message);
    }

    [Fact]
    public void Parse_InvalidFormatValue_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--format=xml"], "rest-config.json"));

        Assert.Contains("Formato invalido", ex.Message);
    }

    [Fact]
    public void Parse_StrictFlag()
    {
        var result = ArgumentParser.Parse(["--strict"], "rest-config.json");

        Assert.True(result.StrictValidation);
    }

    [Fact]
    public void Parse_QuietFlag()
    {
        var result = ArgumentParser.Parse(["--quiet"], "rest-config.json");

        Assert.True(result.Quiet);
    }

    [Fact]
    public void Parse_NoColorFlag()
    {
        var result = ArgumentParser.Parse(["--no-color"], "rest-config.json");

        Assert.True(result.NoColor);
    }

    [Fact]
    public void Parse_Jobs_UpperBound_ThrowsException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--jobs=200"], "rest-config.json"));

        Assert.Contains("entre 1 y 100", ex.Message);
    }

    [Fact]
    public void Parse_Jobs_Negative_ThrowsException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(["--jobs=-5"], "rest-config.json"));

        Assert.Contains("entre 1 y 100", ex.Message);
    }
}
