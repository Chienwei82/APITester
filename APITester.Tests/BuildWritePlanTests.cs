using APITester.Core.Models;
using APITester.Rest;
using APITester.Rest.Models;

namespace APITester.Tests;

public class BuildWritePlanTests
{
    private static ApiResponse MakeResponse(string name) => new()
    {
        Request = new RequestInfo { Name = name }
    };

    [Fact]
    public void TwoRequests_SameOutput_AreGroupedIntoOneOverwriteEntry()
    {
        var recipes = new List<RestRequestConfig>
        {
            new() { Name = "one", Url = "https://a.com/1" },
            new() { Name = "two", Url = "https://a.com/2" }
        };
        var results = new List<ApiResponse>
        {
            MakeResponse("one"),
            MakeResponse("two")
        };

        var plan = RestOrchestrator.BuildWritePlan(results, recipes, "default.json");

        Assert.Single(plan.Overwrite);
        var pair = plan.Overwrite.First();
        Assert.Equal("default.json", pair.Key);
        Assert.Equal(2, pair.Value.Count);
        Assert.Empty(plan.Appends);
    }

    [Fact]
    public void PerRequestOutputs_ProduceSeparateGroups()
    {
        var recipes = new List<RestRequestConfig>
        {
            new() { Url = "https://a.com/1", Output = "one.json" },
            new() { Url = "https://a.com/2", Output = "two.json" }
        };
        var results = new List<ApiResponse>
        {
            MakeResponse("one"),
            MakeResponse("two")
        };

        var plan = RestOrchestrator.BuildWritePlan(results, recipes, "default.json");

        Assert.Equal(2, plan.Overwrite.Count);
        Assert.Empty(plan.Appends);
    }

    [Fact]
    public void AppendOutput_IsCollectedSeparately_NotOverwritten()
    {
        var recipes = new List<RestRequestConfig>
        {
            new() { Url = "https://a.com/1", Output = "shared.json" },
            new() { Url = "https://a.com/2", Output = "shared.json", AppendOutput = true }
        };
        var results = new List<ApiResponse>
        {
            MakeResponse("one"),
            MakeResponse("two")
        };

        var plan = RestOrchestrator.BuildWritePlan(results, recipes, "default.json");

        Assert.Single(plan.Overwrite);
        Assert.Equal("shared.json", plan.Overwrite.First().Key);
        Assert.Single(plan.Overwrite.First().Value); // only the non-append one
        Assert.Single(plan.Appends);
        Assert.Equal("shared.json", plan.Appends[0].Path);
        Assert.Equal("two", plan.Appends[0].Response.Request?.Name);
    }

    [Fact]
    public void AppendUsesItsOwnOutputWhenGiven()
    {
        var recipes = new List<RestRequestConfig>
        {
            new() { Url = "https://a.com/1", AppendOutput = true }
        };
        var results = new List<ApiResponse>
        {
            MakeResponse("one")
        };

        var plan = RestOrchestrator.BuildWritePlan(results, recipes, "default.json");

        Assert.Empty(plan.Overwrite);
        Assert.Single(plan.Appends);
        Assert.Equal("default.json", plan.Appends[0].Path);
    }
}