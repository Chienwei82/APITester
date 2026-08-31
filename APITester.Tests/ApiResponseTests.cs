using APITester.Core.Models;

namespace APITester.Tests;

public class ApiResponseTests
{
    [Fact]
    public void Success2xx_IsSuccessful()
    {
        var response = new ApiResponse
        {
            Response = new ResponseInfo { StatusCode = 200 }
        };

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public void Status3xx_IsSuccessful()
    {
        var response = new ApiResponse
        {
            Response = new ResponseInfo { StatusCode = 304 }
        };

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public void Status400_IsFailed()
    {
        var response = new ApiResponse
        {
            Response = new ResponseInfo { StatusCode = 404 }
        };

        Assert.False(response.IsSuccessful);
    }

    [Fact]
    public void Status500_IsFailed()
    {
        var response = new ApiResponse
        {
            Response = new ResponseInfo { StatusCode = 500 }
        };

        Assert.False(response.IsSuccessful);
    }

    [Fact]
    public void TransportError_IsFailed()
    {
        var response = new ApiResponse
        {
            Response = null,
            Error = "timeout"
        };

        Assert.False(response.IsSuccessful);
    }

    [Fact]
    public void MissingResponse_IsFailed()
    {
        var response = new ApiResponse { Response = null };

        Assert.False(response.IsSuccessful);
    }

    [Fact]
    public void IsSuccessful_IsNotSerialized()
    {
        var response = new ApiResponse
        {
            Response = new ResponseInfo { StatusCode = 200 }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(response);

        Assert.DoesNotContain("IsSuccessful", json);
    }
}
