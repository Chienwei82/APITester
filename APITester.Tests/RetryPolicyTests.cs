using System.Diagnostics;
using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task None_DoesNotRetry_ThrowsOnFirstFailure()
    {
        var policy = RetryPolicy.None;
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new HttpRequestException("fail");
            }));

        Assert.Equal(1, attempts);
        Assert.Contains("1 intentos", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task WithRetries_SucceedsOnSecondAttempt()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult(attempts >= 2 ? 42 : throw new HttpRequestException("fail"));
        });

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task WithRetries_ExhaustsAllAttempts()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10 };
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new HttpRequestException("fail");
            }));

        Assert.Equal(3, attempts);
        Assert.Contains("3 intentos", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task NonRetryableException_DoesNotRetry()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new ArgumentException("bad arg");
            }));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SucceedsFirstAttempt_NoRetries()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult("success");
        });

        Assert.Equal("success", result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExponentialBackoff_IncreasesDelay()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 50, UseExponentialBackoff = true };
        var attempts = 0;
        var delays = new List<long>();

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync<int>(async ct =>
            {
                attempts++;
                if (attempts > 1)
                {
                    delays.Add(sw.ElapsedMilliseconds);
                }
                throw new HttpRequestException("fail");
            }));
        sw.Stop();

        Assert.Equal(4, attempts);
        Assert.Equal(3, delays.Count);
        for (int i = 1; i < delays.Count; i++)
        {
            Assert.True(delays[i] > delays[i - 1],
                $"Delay {i} ({delays[i]}ms) should be greater than delay {i - 1} ({delays[i - 1]}ms)");
        }
    }

    [Fact]
    public async Task RetryOnStatusCodes_RetriesOnMatchingStatusCode()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10, RetryOnStatusCodes = [500] };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult(new ApiResponse
            {
                Request = new RequestInfo(),
                Response = new ResponseInfo { StatusCode = 500 }
            });
        });

        Assert.Equal(3, attempts);
        Assert.Equal(500, ((IHasStatusCode)result).StatusCode);
    }

    [Fact]
    public async Task RetryOnStatusCodes_SucceedsOnRetry()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10, RetryOnStatusCodes = [500, 503] };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            var status = attempts >= 2 ? 200 : 503;
            return Task.FromResult(new ApiResponse
            {
                Request = new RequestInfo(),
                Response = new ResponseInfo { StatusCode = status }
            });
        });

        Assert.Equal(2, attempts);
        Assert.Equal(200, ((IHasStatusCode)result).StatusCode);
    }

    [Fact]
    public async Task RetryOnStatusCodes_NonMatchingStatusCode_DoesNotRetry()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10, RetryOnStatusCodes = [500] };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult(new ApiResponse
            {
                Request = new RequestInfo(),
                Response = new ResponseInfo { StatusCode = 400 }
            });
        });

        Assert.Equal(1, attempts);
        Assert.Equal(400, ((IHasStatusCode)result).StatusCode);
    }

    [Fact]
    public async Task RetryOnStatusCodes_NullList_DoesNotRetryOnStatus()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult(new ApiResponse
            {
                Request = new RequestInfo(),
                Response = new ResponseInfo { StatusCode = 500 }
            });
        });

        Assert.Equal(1, attempts);
    }
}
